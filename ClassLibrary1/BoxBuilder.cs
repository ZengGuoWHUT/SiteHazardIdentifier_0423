
using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Recast;
using DotRecast.Recast.Geom;
using RevitVoxelzation;
using SiteHazardIdentifier;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary1
{
    public  class BoxBuilder
    {
        // 1. 声明一个线程安全的计数器
        private static int _processedCount = 0;
        public MeshLoader Loader { get;}
        public int NumElements { get;}
        public int NumTriangles { get; private set; }
        public BoxBuilder(string strPath)
        {
           this.Loader= new MeshLoader(strPath);
            NumElements = this.Loader.meshes.Count;
            foreach (var mesh in this.Loader.meshes)
            {
                NumTriangles += mesh.GetTris().Length/3;
            }
        }
        public  IEnumerable<VoxelElement> GenerateVoxelElements( float voxSize,float cellHeight)
        {
            var meshes = this.Loader.Meshes().ToList();
            foreach (MyMesh geom in Loader.Meshes().Cast<MyMesh>())
            {
                yield return Mesh2VoxElem(geom,voxSize,cellHeight);
            }
        }

        public  IEnumerable<LightWeightVoxelElement> GenerateBoxElemns( float voxSize,float cellHeight)
        {
            foreach (var ve in GenerateVoxelElements(voxSize,cellHeight))
            {
                var boxElem = new LightWeightVoxelElement(ve);
                yield return boxElem;
            }
        }

        public async IAsyncEnumerable<LightWeightVoxelElement> GenerateBoxElemnsAsync(float voxSize, float cellHeight,IProgress<int> progress)
        {
            int numProcessed = 0;
            int numLastUpdate = 0;
            int updateThreshold = this.NumElements / 100;
            await foreach (var ve in GenerateVoxelElementsAsync(voxSize, cellHeight))
            {
                LightWeightVoxelElement boxElem= null;
                Task tsk = Task.Run(() =>
                {
                    boxElem = new LightWeightVoxelElement(ve);
                    numProcessed += 1;
                    numLastUpdate += 1;
                    if(numLastUpdate>= updateThreshold || numProcessed==this.NumElements)
                    {
                        progress.Report(numProcessed);
                        numLastUpdate = 0;
                    }
                });
                await tsk;
                boxElem.ElementId = ve.ElementId;
                yield return boxElem;
            }
        }
        public ConcurrentBag<LightWeightVoxelElement>GenerateBoxElementParallel(float voxSize,float cellHeight,IProgress<int> progress)
        {
            _processedCount = 0;
            int updateThreshold = this.NumElements / 100;
            int totalCount=this.NumElements;
            ConcurrentBag<LightWeightVoxelElement> result = new ConcurrentBag<LightWeightVoxelElement>();
            var parallelOptions = new ParallelOptions
            {
                // 建议设置为性能核的数量，对于你的CPU，可以尝试 6
                // 你也可以尝试 Environment.ProcessorCount / 2 或其他值进行测试
                MaxDegreeOfParallelism = 6
            };
            Parallel.ForEach(this.Loader.meshes.Cast<MyMesh>(), parallelOptions, geom =>
            {
                var voxElem=Mesh2VoxElem(geom,voxSize,cellHeight);
                result.Add(new LightWeightVoxelElement(voxElem));
                int currentCount = Interlocked.Increment(ref _processedCount);
                // 5. 计算完成的百分比并报告进度
                if(currentCount%updateThreshold==0 || currentCount ==totalCount)
                    progress.Report(currentCount);

            });
            return result;
        }

        public async IAsyncEnumerable<VoxelElement> GenerateVoxelElementsAsync(float voxSize, float cellHeight)
        {
            var meshes = this.Loader.Meshes().ToList();
            foreach (MyMesh geom in this.Loader.Meshes())
            {
                yield return Mesh2VoxElem(geom,voxSize,cellHeight);
            }
        }
        
        private VoxelElement Mesh2VoxElem(MyMesh geom, float voxSize, float cellHeight)
        {
            int colSt = (int)Math.Ceiling(Math.Round(geom.min.X / voxSize, 4)) - 1;
            int rowSt = (int)Math.Ceiling(Math.Round(geom.min.Z / voxSize, 4)) - 1;
            int colEd = (int)Math.Floor(Math.Round(geom.max.X / voxSize, 4)) + 1;
            int rowEd = (int)Math.Floor(Math.Round(geom.max.Z / voxSize, 4)) + 1;
            var min = new RcVec3f((float)colSt * voxSize, geom.min.Y, rowSt * voxSize);
            var max = new RcVec3f((float)colEd * voxSize, geom.max.Y, rowEd * voxSize);
            var widthScale = colEd - colSt;
            var heightScale = rowEd - rowSt;
            var ve = new VoxelElement();
            ve.Voxels = new List<Voxel>();
            float[] verts = geom.GetVerts();
            RcHeightfield solid = new RcHeightfield(widthScale, heightScale, min, max, voxSize, cellHeight, 1);
            //RcHeightfield solid = new RcHeightfield(builderCfg.width, builderCfg.height, builderCfg.bmin, builderCfg.bmax, cfg.Cs, cfg.Ch, cfg.BorderSize);
            int[] tris = geom.GetTris();
            int ntris = tris.Length / 3;
            int[] m_triareas = new int[ntris];
            var ctx = new RcContext();
            RcRasterizations.RasterizeTriangles(ctx, verts, tris, m_triareas, ntris, solid, 0);
            var height = solid.height;
            var width = solid.width;
            List<VoxSortHelper>[,] voxData = new List<VoxSortHelper>[width, height];
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 1. 获取该列的索引
                    int idx = z * width + x;

                    // 2. 直接获取 RcSpan 对象 (而不是索引)

                    var span = solid.spans[idx];


                    // 3. 判断是否为空
                    // 在新版 DotRecast 中，如果该位置没有体素，span 通常是一个默认的结构体
                    // 我们可以通过判断 span.next 是否为 0，或者 span.smin 是否为极值来判断
                    // 最稳妥的方法是检查 span 是否等于默认值 (default(RcSpan))
                    // 或者简单地检查 smin 是否有效 (例如 smin < smax)

                    // 注意：这里有一个陷阱。如果 smin/smax 是坐标，它们可能是负数。
                    // 通常空的 span 其 next 指针为 0。
                    if (span == null || span == default(RcSpan))
                    {
                        // 这是一个空的 Cell，跳过
                        // 注意：有些版本空的 span 可能 smin/smax 也是 0，这取决于初始化逻辑
                        // 如果上面的判断不准，可以尝试判断 span 是否等于 default(RcSpan)
                        continue;
                    }
                    //遍历当前Span
                    // 4. 遍历链表 (处理同一列的多个层)
                    var currentSpan = span;

                    // 我们需要一个循环来遍历链表，但要注意第一个 span 已经在 currentSpan 里了
                    // 并且要防止死循环 (虽然 Recast 的链表通常很规范)
                    int safetyCounter = 0;

                    while (currentSpan != null)
                    {
                        // 再次检查有效性
                        // 如果 smin >= smax，说明这个 span 是无效的或者是空的
                        if (currentSpan.smin >= currentSpan.smax || currentSpan == default(RcSpan))
                        {
                            break;
                        }

                        // 5. 坐标转换
                        float bottomY = currentSpan.smin * cellHeight + min.Y;
                        float topY = currentSpan.smax * cellHeight + min.Y;
                        var vData = new VoxSortHelper()
                        {
                            ColIndex = x + colSt,
                            RowIndex = z + rowSt,
                            BottomElevation = bottomY,
                            TopElevation = topY,

                        };
                        if (voxData[x, z] == null)
                        {
                            voxData[x, z] = new List<VoxSortHelper>() { vData };
                        }
                        else
                        {
                            voxData[x, z].Add(vData);
                        }


                        /*
                        ve.Voxels.Add(new Voxel
                        {
                            ColIndex = x + colSt,
                            RowIndex = z + rowSt,
                            BottomElevation = bottomY,
                            TopElevation = topY
                        });
                        */
                        // 6. 移动到下一个 Span
                        // 在新版中，span.next 存储的是 spans 数组中的索引
                        if (currentSpan.next == null)
                        {
                            break; // 链表结束
                        }

                        // 通过 next 索引去数组里取下一个 span
                        currentSpan = currentSpan.next;
                        safetyCounter++;
                    }
                }
            }
            //merge voxels
            MergeVoxelsByGaps(ref voxData, 0, out var arr);
            //gen voxels
            foreach (var vData in voxData)
            {
                if (vData != null && vData.Count > 0)
                {
                    foreach (var vd in vData)
                    {

                        ve.Voxels.Add(new Voxel()
                        {
                            ColIndex = vd.ColIndex,
                            RowIndex = vd.RowIndex,
                            BottomElevation = vd.BottomElevation,
                            TopElevation = vd.TopElevation,

                        });

                    }
                }
            }
            ve.ElementId =geom.ElementId;
            return ve;
        }
        private void MergeVoxelsByGaps(ref List<VoxSortHelper>[,] mergedVoxels, double offset, out List<VoxelRangeData>[,] GapArray)
        {
            //Obtain the gap Range 
            int colRng = mergedVoxels.GetUpperBound(0) + 1;
            int rowRng = mergedVoxels.GetUpperBound(1) + 1;
            int gapColRng = colRng + 2;
            int gapRowRng = rowRng + 2;
            //scan mergedVoxels for finding the minimum and maximum elev of 
            double maxTopElev = double.MinValue;
            double minBtmElev = double.MaxValue;
            foreach (var voxDataCol in mergedVoxels)
            {
                if (voxDataCol != null)
                {
                    foreach (var v in voxDataCol)
                    {
                        maxTopElev = Math.Max(v.TopElevation, maxTopElev);
                        minBtmElev = Math.Min(minBtmElev, v.BottomElevation);
                    }
                }
            }
            //init the gap rng
            GapArray = new List<VoxelRangeData>[gapColRng, gapRowRng];
            //add gap
            for (int col = 0; col <= gapColRng - 1; col++)
            {
                for (int row = 0; row <= gapRowRng - 1; row++)
                {
                    List<VoxelRangeData> gaps = new List<VoxelRangeData>();

                    //try get the voxSortHelper
                    if (col == 0 || row == 0 || col == gapColRng - 1 || row == gapRowRng - 1 || mergedVoxels[col - 1, row - 1] == null)
                    {
                        //create a dummy gap
                        VoxelRangeData dummyGap = new VoxelRangeData()
                        {
                            StartElevation = double.MinValue,
                            EndElevation = double.MaxValue,
                            Index = -1,
                            ColIndex = col,
                            RowIndex = row,
                            IsOutside = true
                        };
                        gaps.Add(dummyGap);
                    }
                    else //obtain gaps between voxels
                    {
                        var voxData = mergedVoxels[col - 1, row - 1];
                        int numGaps = voxData.Count + 1;
                        //add first gap
                        if (Math.Round(voxData[0].BottomElevation - minBtmElev, 4) != 0)
                        {
                            VoxelRangeData dummyGapLower = new VoxelRangeData()
                            {
                                StartElevation = double.MinValue,
                                EndElevation = voxData[0].BottomElevation,
                                Index = -1,
                                ColIndex = col,
                                RowIndex = row,
                                IsOutside = true
                            };
                            gaps.Add(dummyGapLower);
                        }
                        else
                        {
                            gaps.Add(null);
                        }

                        //add other gap
                        double voxUpperElev = voxData[0].TopElevation;
                        for (int j = 0; j <= voxData.Count - 2; j++)
                        {
                            var vox0 = voxData[j];
                            var vox1 = voxData[j + 1];
                            voxUpperElev = vox1.TopElevation;
                            VoxelRangeData gap = new VoxelRangeData()
                            {
                                StartElevation = vox0.TopElevation + offset / 2,
                                EndElevation = vox1.BottomElevation - offset / 2,
                                Index = j,
                                ColIndex = col,
                                RowIndex = row,
                            };
                            gaps.Add(gap);
                        }
                        if (Math.Round(voxUpperElev - maxTopElev, 4) != 0)
                        {
                            //add last
                            var dummyGapUpper = new VoxelRangeData()
                            {
                                StartElevation = voxUpperElev,
                                EndElevation = double.MaxValue,
                                Index = -1,
                                IsOutside = true,
                                ColIndex = col,
                                RowIndex = row
                            };
                            gaps.Add(dummyGapUpper);
                        }
                        else
                        {
                            gaps.Add(null);
                        }


                    }
                    GapArray[col, row] = gaps;
                }
            }
            //find the connection component of the gap
            Stack<VoxelRangeData> outerRanges = new Stack<VoxelRangeData>();
            foreach (var items in GapArray)
            {
                foreach (var gap in items)
                {
                    if (gap != null && gap.IsOutside == true)//gap is outside
                    {
                        outerRanges.Push(gap);
                    }
                }
            }
            //flood fill
            while (outerRanges.Count > 0)
            {
                var gapOut = outerRanges.Pop();
                var colOut = gapOut.ColIndex;
                var rowOut = gapOut.RowIndex;

                //sacn neighbors
                for (int colOff = -1; colOff <= 1; colOff++)
                {
                    for (int rowOff = -1; rowOff <= 1; rowOff++)
                    {
                        if (colOff != 0 && rowOff != 0)
                            continue;
                        var colNear = colOut + colOff;
                        var rowNear = rowOut + rowOff;
                        if (colNear >= 0 && colNear < gapColRng && rowNear >= 0 && rowNear < gapRowRng)
                        {
                            var gapsNear = GapArray[colNear, rowNear];
                            if (gapsNear != null)
                            {
                                //find the gap intersecting gapOut
                                foreach (var gap2Check in gapsNear)
                                {
                                    if (gap2Check != null && gap2Check.IsOutside == false && gapOut.Intersect(gap2Check))
                                    {
                                        gap2Check.IsOutside = true;
                                        outerRanges.Push(gap2Check);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            //merge voxels based on gap
            for (int col = 0; col < colRng; col++)
            {
                for (int row = 0; row < rowRng; row++)
                {
                    var voxColumn = mergedVoxels[col, row];
                    if (voxColumn != null)
                    {
                        List<VoxSortHelper> snakeVoxels = new List<VoxSortHelper>();
                        var snakePt = 0;
                        var foodPt = 1;
                        var snakeVox = voxColumn[snakePt];
                        while (foodPt < voxColumn.Count)
                        {
                            var food = voxColumn[foodPt];
                            //get gap between snake and food
                            var gap = GapArray[col + 1, row + 1][foodPt];
                            if (gap != null && gap.IsOutside == false)
                            {
                                snakeVox.TopElevation = food.TopElevation;
                                foodPt += 1;
                            }
                            else
                            {
                                snakeVoxels.Add(snakeVox);
                                snakePt = foodPt;
                                snakeVox = food;
                                foodPt += 1;
                            }
                        }
                        snakeVoxels.Add(snakeVox);
                        mergedVoxels[col, row] = snakeVoxels;
                    }
                }
            }
        }
    }

    public class MeshLoader : IRcInputGeomProvider
    {
        public readonly float[] vertices;
        public readonly int[] faces;
        public readonly float[] normals;
        private readonly RcVec3f bmin;
        private readonly RcVec3f bmax;
        public readonly List<RcTriMesh> meshes;


        public MeshLoader(string meshPath)
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
                        lstVertices.Add((float)v.X);
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
                var faces = lstTriangles.ToArray();
                var vertices = lstVertices.ToArray();
                var m = new MyMesh(vertices, faces) { ElementId=mesh.ElementId};

                m.SetMin(new RcVec3f((float)min.X, (float)min.Y, (float)min.Z ));
                m.SetMax(new RcVec3f((float)max.X, (float)max.Y, (float)max.Z));
                minGlobal = Vec3.Min(min, minGlobal);
                maxGlobal = Vec3.Max(max, maxGlobal);
                this.meshes.Add(m);
            }
            this.bmin = new RcVec3f((float)minGlobal.X, (float)minGlobal.Y, (float)minGlobal.Z);
            this.bmax = new RcVec3f((float)maxGlobal.X, (float)maxGlobal.Y, (float)maxGlobal.Z);
        }

        public static IEnumerable<MeshElement> CreateMeshElement(string path)
        {
            Stack<MeshElement> elements = new Stack<MeshElement>();
            using (StreamReader readeer = new StreamReader(path, Encoding.Default))
            {
                string strContent = string.Empty;
                while (!readeer.EndOfStream)
                {
                    string content = readeer.ReadLine();
                    switch (content)
                    {
                        case "":
                            if (elements.Count != 0)
                            {
                                var elem = elements.Pop();
                                if (elem.Solids.Count > 0)
                                {
                                    yield return elem;
                                }
                            }
                            break;
                        default:
                            if (!content.Contains(","))
                            {
                                MeshElement me = new MeshElement(content, new List<MeshSolid>());
                                elements.Push(me);
                            }
                            else
                            {
                                var curElem = elements.Peek();
                                if (curElem.Solids.Count == 0)
                                {
                                    curElem.Solids.Add(new MeshSolid(curElem, new List<Vec3>(), new List<int>()));
                                }
                                var sld = curElem.Solids[0];
                                string[] strSplit = content.Split(';');
                                string[] strVertices = strSplit[0].Split(',');
                                string[] strTris = strSplit[1].Split(',');
                                var verticesOffset = curElem.Solids[0].Vertices.Count;
                                for (int i = 0; i < strVertices.Length; i += 3)
                                {
                                    double dblX = double.Parse(strVertices[i]);
                                    double dblY = double.Parse(strVertices[i + 1]);
                                    double dblZ = double.Parse(strVertices[i + 2]);
                                    Vec3 point = new Vec3(dblX, dblY, dblZ);
                                    sld.Vertices.Add(point);
                                }
                                //add tri
                                for (int i = 0; i < strTris.Length; i += 3)
                                {
                                    int vi0 = verticesOffset + int.Parse(strTris[i]);
                                    int vi1 = verticesOffset + int.Parse(strTris[i + 1]);
                                    int vi2 = verticesOffset + int.Parse(strTris[i + 2]);
                                    var tri = new RevitVoxelzation.MeshTriangle(sld, new int[3] { vi0, vi1, vi2 });
                                    sld.Triangles.Add(tri);
                                }
                            }
                            break;
                    }

                }
                readeer.Close();
            }

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
            foreach (var mesh in this.meshes)
            {
                yield return mesh;
            }
        }

        public void RemoveOffMeshConnections(Predicate<RcOffMeshConnection> filter)
        {
            throw new NotImplementedException();
        }
    }
}
