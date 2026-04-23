
using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Recast;
using DotRecast.Recast.Geom;
using RevitVoxelzation;
using SiteHazardIdentifier;
using System.Diagnostics;
using System.Security.Policy;

namespace ClassLibrary1
{

   
    public class MeshProvider :IRcInputGeomProvider
    {
        public readonly float[] vertices;
        public readonly int[] faces;
        public readonly float[] normals;
        private readonly RcVec3f bmin;
        private readonly RcVec3f bmax;
        private readonly RcTriMesh mesh;
        private readonly RcVec3f globalMin;//相对于世界坐标系的最小值
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
                        var vxzy = new Vec3(v.X, v.Z, v.Y);
                        lstVertices.Add((float)v.X/1000);
                        lstVertices.Add((float)v.Z/1000);
                        lstVertices.Add((float)v.Y/1000);
                        max = Vec3.Max(vxzy / 1000, max);
                        min = Vec3.Min(vxzy / 1000, min);
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
            /*
            for(int i=0;i<=lstVertices.Count -1;i+=3)
            {
                lstVertices[i] = lstVertices[i] - (float)min.X;
                lstVertices[i+1] = lstVertices[i+1] - (float)min.Y;
                lstVertices[i + 2] = lstVertices[i + 2] - (float)min.Z;
            }
            */
            this.vertices = lstVertices.ToArray();
            this.faces = lstTriangles.ToArray();
            /*
            var scale = max - min;
            this.globalMin = new RcVec3f((float)min.X, (float)min.Y, (float)min.Z);;
            this.bmax=new RcVec3f((float)scale.X, (float)scale.Y, (float)scale.Z);
            this.bmin=new RcVec3f(0f,0f,0f);
            */
            this.bmin = new RcVec3f((float)min.X, (float)min.Y, (float)min.Z);
            this.bmax = new RcVec3f((float)max.X, (float)max.Y, (float)max.Z);
            this.mesh = new RcTriMesh(this.vertices, this.faces);
        }
        public  RcVec3f GetGlobalMin()
        {
            return this.globalMin;
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
            yield return this.mesh;
        }

        public void RemoveOffMeshConnections(Predicate<RcOffMeshConnection> filter)
        {
            throw new NotImplementedException();
        }
    }
   
    public class MeshProvider2 : IRcInputGeomProvider
    {
        public readonly float[] vertices;
        public readonly int[] faces;
        public readonly float[] normals;
        private readonly RcVec3f bmin;
        private readonly RcVec3f bmax;
        private readonly List<RcTriMesh> meshes;
       

        public MeshProvider2(string meshPath)
        {
            Vec3 minGlobal = new Vec3(double.MaxValue, double.MaxValue, double.MaxValue);
            Vec3 maxGlobal = new Vec3(double.MinValue, double.MinValue, double.MinValue);
            RevitMeshDocumenetConverter converter = new RevitMeshDocumenetConverter();
            this.meshes = new List<RcTriMesh>();
            foreach (var mesh in RevitMeshDocumenetConverter.CreateMeshElement(meshPath))
            {
                Vec3 min = new Vec3(double.MaxValue, double.MaxValue, double.MaxValue);
                Vec3 max = new Vec3(double.MinValue, double.MinValue, double.MinValue);
                List<float> lstVertices = new List<float>();
                List<int> lstTriangles = new List<int>();
                foreach (var sld in mesh.Solids)
                {
                    var vs = sld.Vertices;
                    var triangles = sld.Triangles;

                    int pt = lstVertices.Count / 3;
                    foreach (var v in vs)
                    {
                        var vxzy = new Vec3(v.X, v.Z, v.Y);
                        lstVertices.Add((float)v.X );
                        lstVertices.Add((float)v.Z);
                        lstVertices.Add((float)v.Y);
                        max = Vec3.Max(vxzy, max);
                        min = Vec3.Min(vxzy, min);
                    }
                   
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
                var faces=lstTriangles.ToArray();
                var vertices = lstVertices.ToArray();
                var m = new MyMesh(vertices, faces) { ElementId = mesh.ElementId };

               m.SetMin( new RcVec3f((float)min.X, (float)min.Y, (float)min.Z));
               m.SetMax( new RcVec3f((float)max.X, (float)max.Y, (float)max.Z));
                minGlobal = Vec3.Min(min, minGlobal);
                maxGlobal = Vec3.Max(max, maxGlobal);
                this.meshes.Add(m);
            }
            this.bmin = new RcVec3f((float)minGlobal.X, (float)minGlobal.Y, (float)minGlobal.Z);
            this.bmax = new RcVec3f((float)maxGlobal.X, (float)maxGlobal.Y, (float)maxGlobal.Z);
            /*
            for(int i=0;i<=lstVertices.Count -1;i+=3)
            {
                lstVertices[i] = lstVertices[i] - (float)min.X;
                lstVertices[i+1] = lstVertices[i+1] - (float)min.Y;
                lstVertices[i + 2] = lstVertices[i + 2] - (float)min.Z;
            }
            */



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
            return this.meshes[0];
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
            foreach(var mesh in this.meshes)
            {
                yield return mesh;
            }
        }

        public void RemoveOffMeshConnections(Predicate<RcOffMeshConnection> filter)
        {
            throw new NotImplementedException();
        }
    }

    public class MyMesh : RcTriMesh
    {
        public string ElementId {  get; set; }
        public RcVec3f min;
        public RcVec3f max;
        public MyMesh(float[] vertices, int[] faces) : base(vertices, faces)
        {
        }
        public void SetMin(RcVec3f min)
        {
            this.min = min;
        }
        public void SetMax(RcVec3f max)
        {
            this.max = max;
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
