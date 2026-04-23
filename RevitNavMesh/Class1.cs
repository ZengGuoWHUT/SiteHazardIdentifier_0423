
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.DirectContext3D;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Recast;
using DotRecast.Recast;
using DotRecast.Recast.Geom;
using RevitVoxelzation;
using SiteHazardIdentifier;
using System.Security.Cryptography;
using Point = Autodesk.Revit.DB.Point;

namespace RevitNavMesh
{
    [Transaction(TransactionMode.Manual)]
    public class RevitNavMesh : IExternalCommand
    {
        private const float m_cellSize = 0.3f;
        private const float m_cellHeight = 0.2f;
        private const float m_agentHeight = 2.0f;
        private const float m_agentRadius = 0.6f;
        private const float m_agentMaxClimb = 0.9f;
        private const float m_agentMaxSlope = 45.0f;
        private const int m_regionMinSize = 8;
        private const int m_regionMergeSize = 20;
        private const float m_edgeMaxLen = 12.0f;
        private const float m_edgeMaxError = 1.3f;
        private const int m_vertsPerPoly = 6;
        private const float m_detailSampleDist = 6.0f;
        private const float m_detailSampleMaxError = 1.0f;
        private RcPartition m_partitionType = RcPartition.WATERSHED;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {            //获取当前活动文档
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            var doc=uiDoc.Document;
            //获得活动视图
            var view = uiDoc.ActiveView;

            //open mesh file
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "mesh text|*.txt";
            if (ofd.ShowDialog() != DialogResult.OK)
            {
                return Result.Cancelled;
            }
            var scene = new MeshProvider(ofd.FileName);
            var bmin = scene.GetMeshBoundsMin();
            var bmax= scene.GetMeshBoundsMax();
            RcConfig cfg = new RcConfig(
            RcPartition.MONOTONE,
            m_cellSize, m_cellHeight,
            m_agentMaxSlope, m_agentHeight, m_agentRadius, m_agentMaxClimb,
             m_regionMinSize, m_regionMergeSize,
            m_edgeMaxLen, m_edgeMaxError,
            m_vertsPerPoly,
            m_detailSampleDist, m_detailSampleMaxError,
            true, true, true,
            SampleAreaModifications.SAMPLE_AREAMOD_GROUND, true);

            RcBuilderConfig bcfg = new RcBuilderConfig(cfg, bmin, bmax);
            RcContext m_ctx = new RcContext();
            //
            // Step 2. Rasterize input polygon soup.
            //

            // Allocate voxel heightfield where we rasterize our input data to.
            RcHeightfield m_solid = new RcHeightfield(bcfg.width, bcfg.height, bcfg.bmin, bcfg.bmax, cfg.Cs, cfg.Ch, cfg.BorderSize);
            foreach (RcTriMesh geom in scene.Meshes())
            {
                float[] verts = geom.GetVerts();
                int[] tris = geom.GetTris();
                int ntris = tris.Length / 3;

                // Allocate array that can hold triangle area types.
                // If you have multiple meshes you need to process, allocate
                // and array which can hold the max number of triangles you need to process.

                // Find triangles which are walkable based on their slope and rasterize them.
                // If your input data is multiple meshes, you can transform them here, calculate
                // the are type for each of the meshes and rasterize them.
                int[] m_triareas = RcRecast.MarkWalkableTriangles(m_ctx, cfg.WalkableSlopeAngle, verts, tris, ntris, cfg.WalkableAreaMod);
                RcRasterizations.RasterizeTriangles(m_ctx, verts, tris, m_triareas, ntris, m_solid, cfg.WalkableClimb);
            }
            //
            // Step 3. Filter walkable surfaces.
            //

            // Once all geometry is rasterized, we do initial pass of filtering to
            // remove unwanted overhangs caused by the conservative rasterization
            // as well as filter spans where the character cannot possibly stand.
            RcFilters.FilterLowHangingWalkableObstacles(m_ctx, cfg.WalkableClimb, m_solid);
            RcFilters.FilterLedgeSpans(m_ctx, cfg.WalkableHeight, cfg.WalkableClimb, m_solid);
            RcFilters.FilterWalkableLowHeightSpans(m_ctx, cfg.WalkableHeight, m_solid);
            //
            // Step 4. Partition walkable surface to simple regions.
            //

            // Compact the heightfield so that it is faster to handle from now on.
            // This will result more cache coherent data as well as the neighbours
            // between walkable cells will be calculated.
            RcCompactHeightfield m_chf = RcCompacts.BuildCompactHeightfield(m_ctx, cfg.WalkableHeight, cfg.WalkableClimb, m_solid);

            // Erode the walkable area by agent radius.
            RcAreas.ErodeWalkableArea(m_ctx, cfg.WalkableRadius, m_chf);

            if (m_partitionType == RcPartition.WATERSHED)
            {
                // Prepare for region partitioning, by calculating distance field
                // along the walkable surface.
                RcRegions.BuildDistanceField(m_ctx, m_chf);
                // Partition the walkable surface into simple regions without holes.
                RcRegions.BuildRegions(m_ctx, m_chf, cfg.MinRegionArea, cfg.MergeRegionArea);
            }
            else if (m_partitionType == RcPartition.MONOTONE)
            {
                // Partition the walkable surface into simple regions without holes.
                // Monotone partitioning does not need distancefield.
                RcRegions.BuildRegionsMonotone(m_ctx, m_chf, cfg.MinRegionArea, cfg.MergeRegionArea);
            }
            else
            {
                // Partition the walkable surface into simple regions without holes.
                RcRegions.BuildLayerRegions(m_ctx, m_chf, cfg.MinRegionArea);
            }

            //
            // Step 5. Trace and simplify region contours.
            //

            // Create contours.
            RcContourSet m_cset = RcContours.BuildContours(m_ctx, m_chf, cfg.MaxSimplificationError, cfg.MaxEdgeLen, RcBuildContoursFlags.RC_CONTOUR_TESS_WALL_EDGES);

            //
            // Step 6. Build polygons mesh from contours.
            //

            // Build polygon navmesh from the contours.
            RcPolyMesh m_pmesh = RcMeshs.BuildPolyMesh(m_ctx, m_cset, cfg.MaxVertsPerPoly);
            // Step 7. Create detail mesh which allows to access approximate height
            // on each polygon.
            //

            RcPolyMeshDetail m_dmesh = RcMeshDetails.BuildPolyMeshDetail(m_ctx, m_pmesh, m_chf, cfg.DetailSampleDist,
                cfg.DetailSampleMaxError);

           //geneerate meshes
           using(Transaction t=new Transaction(doc))
           {
                t.Start("Gen Nav Meshes");
                WireframeBuilder wfBuilder = new WireframeBuilder();
                var numVertices = m_dmesh.nverts;
                List<XYZ> vertices = new List<XYZ>();
                for(int i=0;i<numVertices*3;i+=3)
                {
                    var x = m_dmesh.verts[i]/304.8d;
                    var z=m_dmesh.verts[(i+1)]/304.8d;
                    var y=m_dmesh.verts[i+2]/304.8d; 
                    XYZ pt = new XYZ(x, y, z);
                    vertices.Add(pt);
                    wfBuilder.AddPoint(Point.Create(pt));
                }
                /*
                 meshes 数组是一个一维数组，但它在逻辑上被看作是一个二维数组或结构体数组。
                对于每一个子网格（索引为 i），
                它占用 4 个连续的 int 位置（即 i*4 到 i*4+3），分别存储以下信息：
                表格
                数组索引偏移	对应含义	描述
                0	顶点基址 (Vert Base)	该子网格的顶点在 verts 数组中的起始索引。
                1	顶点数量 (Vert Count)	该子网格包含多少个顶点。
                2	三角形基址 (Tri Base)	该子网格的三角形数据在 tris 数组中的起始索引。
                3	三角形数量 (Tri Count)	该子网格包含多少个三角形。
                 */
                for (int m = 0; m < m_dmesh.nmeshes; m++)
                {
                    int vfirst = m_dmesh.meshes[m * 4];//当前网格的顶点基址
                    int vCount = m_dmesh.meshes[m * 4 + 1];//网格的顶点数量
                    int tfirst = m_dmesh.meshes[m * 4 + 2];//当前网格的三角形基地址
                    int triCount = m_dmesh.meshes[m*4+3];//当前网格的三角形数量
                    for (int f = 0; f < triCount; f++)
                    {
                        var triindexLocal0 = (tfirst + f) * 4;
                        var triIndexLocal1 = (tfirst + f) * 4 + 1;
                        var triIndexLocal2 = (tfirst + f) * 4 + 2;
                        var triIndexGlobal0 = vfirst + m_dmesh.tris[triindexLocal0];
                        var triIndexGlobal1 = vfirst + m_dmesh.tris[triIndexLocal1];
                        var triIndexGlobal2 = vfirst + m_dmesh.tris[triIndexLocal2];
                        var viGlobal = new int[3] { triIndexGlobal0, triIndexGlobal1, triIndexGlobal2 };
                        for(int i=0; i<viGlobal.Length; i++)
                        {
                            var pt0=vertices[i%3];
                            var pt1=vertices[i%3+1];
                            wfBuilder.AddCurve(Line.CreateBound(pt0,pt1));
                        }
                    }
                }
                DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                ds.AppendShape(wfBuilder);
                t.Commit();


           }
            return Result.Succeeded;
        }


    }
    public class MeshProvider : IRcInputGeomProvider
    {
        public readonly float[] vertices;
        public readonly int[] faces;
        public readonly float[] normals;
        private readonly RcVec3f bmin;
        private readonly RcVec3f bmax;
        private readonly RcTriMesh mesh;
        public MeshProvider(string meshPath)
        {
            RevitMeshDocumenetConverter converter = new RevitMeshDocumenetConverter();
            List<float> lstVertices = new List<float>();
            List<int> lstTriangles = new List<int>();
            Vec3 min = new Vec3(double.MaxValue, double.MaxValue, double.MaxValue);
            Vec3 max = new Vec3(double.MinValue, double.MinValue, double.MinValue);

            foreach (var mesh in RevitMeshDocumenetConverter.CreateMeshElement(meshPath))
            {
                foreach (var sld in mesh.Solids)
                {
                    var vs = sld.Vertices;
                    var triangles = sld.Triangles;

                    int pt = lstVertices.Count / 3;
                    foreach (var v in vs)
                    {
                        lstVertices.Add((float)v.X);
                        lstVertices.Add((float)v.Z);
                        lstVertices.Add((float)v.Y);
                        max = Vec3.Max(v, max);
                        min = Vec3.Min(v, min);
                    }
                    faces = new int[triangles.Count * 3];

                    foreach (var tri in triangles)
                    {
                        var tri0 = pt + tri.VerticesIndex[0];
                        var tri1 = pt + tri.VerticesIndex[1];
                        var tri2 = pt + tri.VerticesIndex[2];
                        lstTriangles.Add(tri0);
                        lstTriangles.Add(tri1);
                        lstTriangles.Add(tri2);
                    }
                }
            }
            this.vertices = lstVertices.ToArray();
            this.faces = lstTriangles.ToArray();
            this.bmin = new RcVec3f((float)min.X, (float)min.Z, (float)min.Y);
            this.bmax = new RcVec3f((float)max.X, (float)max.Y, (float)max.Z);
            this.mesh = new RcTriMesh(this.vertices, this.faces);
        }

        public void AddConvexVolume(RcConvexVolume convexVolume)
        {
            throw new NotImplementedException();
        }

        public void AddOffMeshConnection(RcVec3f start, RcVec3f end, float radius, bool bidir, int area, int flags)
        {
            throw new NotImplementedException();
        }

        public IList<RcConvexVolume> ConvexVolumes()
        {
            throw new NotImplementedException();
        }

        public RcTriMesh GetMesh()
        {
            return this.mesh;
        }

        public RcVec3f GetMeshBoundsMax()
        {
            return this.bmax;
        }

        public RcVec3f GetMeshBoundsMin()
        {
            return this.bmin;
        }

        public List<RcOffMeshConnection> GetOffMeshConnections()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<RcTriMesh> Meshes()
        {
            throw new NotImplementedException();
        }

        public void RemoveOffMeshConnections(Predicate<RcOffMeshConnection> filter)
        {
            throw new NotImplementedException();
        }
    }

    public class SampleAreaModifications
    {
        public const int SAMPLE_POLYAREA_TYPE_MASK = 0x07;

        /// Value for the kind of ceil "ground"
        public const int SAMPLE_POLYAREA_TYPE_GROUND = 0x1;

        /// Value for the kind of ceil "water"
        public const int SAMPLE_POLYAREA_TYPE_WATER = 0x2;

        /// Value for the kind of ceil "road"
        public const int SAMPLE_POLYAREA_TYPE_ROAD = 0x3;

        /// Value for the kind of ceil "grass"
        public const int SAMPLE_POLYAREA_TYPE_GRASS = 0x4;

        /// Flag for door area. Can be combined with area types and jump flag.
        public const int SAMPLE_POLYAREA_FLAG_DOOR = 0x08;

        /// Flag for jump area. Can be combined with area types and door flag.
        public const int SAMPLE_POLYAREA_FLAG_JUMP = 0x10;

        public static readonly RcAreaModification SAMPLE_AREAMOD_GROUND = new RcAreaModification(SAMPLE_POLYAREA_TYPE_GROUND, SAMPLE_POLYAREA_TYPE_MASK);
        public static readonly RcAreaModification SAMPLE_AREAMOD_WATER = new RcAreaModification(SAMPLE_POLYAREA_TYPE_WATER, SAMPLE_POLYAREA_TYPE_MASK);
        public static readonly RcAreaModification SAMPLE_AREAMOD_ROAD = new RcAreaModification(SAMPLE_POLYAREA_TYPE_ROAD, SAMPLE_POLYAREA_TYPE_MASK);
        public static readonly RcAreaModification SAMPLE_AREAMOD_GRASS = new RcAreaModification(SAMPLE_POLYAREA_TYPE_GRASS, SAMPLE_POLYAREA_TYPE_MASK);
        public static readonly RcAreaModification SAMPLE_AREAMOD_DOOR = new RcAreaModification(SAMPLE_POLYAREA_FLAG_DOOR, SAMPLE_POLYAREA_FLAG_DOOR);
        public static readonly RcAreaModification SAMPLE_AREAMOD_JUMP = new RcAreaModification(SAMPLE_POLYAREA_FLAG_JUMP, SAMPLE_POLYAREA_FLAG_JUMP);
    }
}
