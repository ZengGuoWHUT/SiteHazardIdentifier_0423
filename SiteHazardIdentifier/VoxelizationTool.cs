using Autodesk.Revit.DB.Architecture;
using SiteHazardIdentifier;
using System;
using System.CodeDom;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace RevitVoxelzation
{
    public class VoxelizationTool
    {
    }
    #region Geometry Primitive
    
    
    public class MeshDocument
    {
        public MeshDocument Paremt { get; set; } = null;
        public string Name { get; set; }
        public List<MeshElement> Elements { get; set; } = new List<MeshElement>();
        public List<MeshDocument> LinkDocuments { get; set; } = new List<MeshDocument>();
        
        public Transform Transform { get; set; }
        public MeshDocument(string name, List<MeshElement> elements, Transform transform)
        {
            Name = name;
            Elements = elements;
            Transform = transform;
        }
        
        public IEnumerable<MeshElement> GetAllElementsInDocumentAndLink(bool excludeSymbol)
        {
            //export elem in current model
            foreach (var elem in this.Elements)
            {
                if(excludeSymbol && elem.IsSymbol)
                {
                    continue;
                }
                else
                {
                    yield return elem;
                }
            }
            //export linked element
            foreach (var doc in this.LinkDocuments)
            {
                foreach (var elem in doc.GetAllElementsInDocumentAndLink(excludeSymbol))
                {
                    yield return elem;
                }
            }
        }
    }
    public struct CellIndex:IEquatable<CellIndex>
    {
        public int Col { get; set; }
        public int Row { get; set; }
        public bool IsInitialized { get;  }
        public CellIndex (int col, int row)
        {
            this.Col = col;
            this.Row = row;
            IsInitialized = true;
        }
        
        
        public static CellIndex operator +(CellIndex x,CellIndex y)
        {
            return new CellIndex(x.Col+y.Col, x.Row+y.Row);
        }
        public static CellIndex operator -(CellIndex x, CellIndex y)
        {
            return new CellIndex(x.Col -y.Col, x.Row - y.Row);
        }
        
        public static bool operator ==(CellIndex x,CellIndex y)
        {
            return x.Equals(y);
        }
        public static bool operator !=(CellIndex x, CellIndex y)
        {
            return !(x.Equals(y));
        }
        public override int GetHashCode()
        {
            //return this.IndexString.GetHashCode();
            unchecked
            {
                return (Col * 397) ^ Row;
            }
            //return base.GetHashCode();
        }
       
        public override string ToString()
        {
            return String.Format("{0},{1}", this.Col, this.Row);
            //return base.ToString();
        }

        public bool Equals(CellIndex other)
        {
           
            if (other.Col == this.Col && other.Row == this.Row)
            {
                return true;
            }
            else
            {
                return false;
            }
            //throw new NotImplementedException();
        }
    }

    public class CellIndex3D : IEquatable<CellIndex3D>
    {
        public int Col { get; set; }
        public int Row { get; set; }
        public int Layer { get; set; }
        public CellIndex3D(int col, int row, int layer)
        {
            Col = col;
            Row = row;
            Layer = layer;
        }
        public bool Equals(CellIndex3D other)
        {
            return (Col == other.Col && Row == other.Row && Layer == other.Layer);
            //throw new NotImplementedException();
        }
        public static CellIndex3D operator +(CellIndex3D x, CellIndex3D y)
        {
            return new CellIndex3D(x.Col + y.Col, x.Row + y.Row, x.Layer + y.Layer);
        }
        public static CellIndex3D operator -(CellIndex3D x, CellIndex3D y)
        {
            return new CellIndex3D(x.Col - y.Col, x.Row - y.Row, x.Layer - y.Layer);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Col; // 23 和 31 都是很好的素数
                hash = hash * 23 + Row;
                hash = hash * 23 + Layer;
                return hash;
            }
            //return base.GetHashCode();
        }
        public double GetHorizontalLen()
        {
            return Math.Sqrt(Math.Pow(this.Col, 2) + Math.Pow(this.Row, 2));
        }
        public override string ToString()
        {
            return string.Format("{0}_{1}_{2}", Col, Row, Layer);
            
        }
    }

    
    /// <summary>
    /// Raw mesh element
    /// </summary>
    public class MeshElement
    {
        public MeshDocument Document { get; set; }
        public string ElementId { get; set; }
        public string Name { get; set; } = "NoName";
        public string Category { get; set; } = "Empty";
        public List<MeshSolid> Solids { get; set; } = new List<MeshSolid>();
       
        public bool IsSymbol { get; internal set; }=false;
        public bool IsSupportElem { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public bool isTransport { get; internal set; }
        public bool IsElementGeometry { get; internal set; } = true;
        

        public MeshElement ()
        {
            
        }
        public MeshElement(MeshDocument document, string elementId, List<MeshSolid> solids)
        {
            this.Document = document;
            ElementId = elementId;
            Solids = solids;
            
        }
        public MeshElement(string elementId, List<MeshSolid> solids)
        {
           
            ElementId = elementId;
            Solids = solids;

        }
        public double GetBoxArea()
        {
            double area = 0;
            List<Vec3> vertices = new List<Vec3>();
            foreach (var sld in this.Solids)
            {
                vertices.AddRange(sld.Vertices);
            }
            if(vertices.Count !=0)
            {
                double dblXMax = double.MinValue;
                double dblYMax = double.MinValue;
                double dblZMax = double.MinValue;
                double dblXMin = double.MaxValue;
                double dblYMin = double.MaxValue;
                double dblZMin = double.MaxValue;

                foreach (var v in vertices)
                {
                    dblXMax = Math.Max(dblXMax, v.X);
                    dblYMax = Math.Max(dblYMax, v.Y);
                    dblZMax = Math.Max(dblZMax, v.Z);
                    dblXMin = Math.Min(dblXMin, v.X);
                    dblYMin = Math.Min(dblYMin, v.Y);
                    dblZMin = Math.Min(dblZMin, v.Z);
                }
                
                var xScale = dblXMax - dblXMin;
                var yScale = dblYMax - dblYMin;
                area= xScale * yScale*2;
                
            }
            return area;
        }

        public bool TryGetAABB(out Vec3 min,out Vec3 max)
        {
            
            List<Vec3> vertices = new List<Vec3>();
            foreach (var sld in this.Solids)
            {
                vertices.AddRange(sld.Vertices);
            }
            if (vertices.Count != 0)
            {
                double dblXMax = double.MinValue;
                double dblYMax = double.MinValue;
                double dblZMax = double.MinValue;
                double dblXMin = double.MaxValue;
                double dblYMin = double.MaxValue;
                double dblZMin = double.MaxValue;

                foreach (var v in vertices)
                {
                    dblXMax = Math.Max(dblXMax, v.X);
                    dblYMax = Math.Max(dblYMax, v.Y);
                    dblZMax = Math.Max(dblZMax, v.Z);
                    dblXMin = Math.Min(dblXMin, v.X);
                    dblYMin = Math.Min(dblYMin, v.Y);
                    dblZMin = Math.Min(dblZMin, v.Z);
                }
                min = new Vec3(dblXMin, dblYMin, dblZMin);
                max=new Vec3(dblXMax,dblYMax,dblZMax);
                return true;
            }
            else
            {
                min = null;
                max = null;
                return false;
            }
        }
        public double GetTriangleLength()
        {
            var len = 0d;
            foreach (var sld in this.GetSolids())
            {
                foreach (var tri in sld.Triangles)
                {
                    for(int i=0;i<=2;i++)
                    {
                        var v0=tri.Get_Vertex(i);
                        var v1=tri.Get_Vertex((i+1)%3);
                        Vec3 edge = v1 - v0;
                        len += edge.GetLength();
                    }
                    len += tri.Get_ProjectionArea();
                }
            }
            return len;
        }


        public int GetTriangleNumber()
        {
            int numTri = 0;
            foreach (var sld in this.Solids)
            {
                numTri += sld.Triangles.Count;
            }
            return numTri;
        }
        private Transform GetTotalTransform()
        {
            var doc = this.Document;
            Transform t = Transform.Idnentity;
            while (doc!=null)
            {
                t=(doc.Transform).Multiply (t);
                doc = doc.Paremt;
            }
            return t;
        }
        public IEnumerable<MeshSolid> GetSolids()
        {
            List<MeshSolid> solids = new List<MeshSolid>();
            var trf = GetTotalTransform();
            foreach (var sld in this.Solids)
            {
                yield return (sld.CopyByTransform(trf));
            }
        }
    }
    
    public class MeshSolid
    {
        public MeshElement Owner { get; set; }
        public List<MeshTriangle> Triangles { get; set; }
        public List<Vec3> Vertices { get; set; }
        public List<GridPoint> GridPoints { get; set; }
        public MeshSolid()
        {

        }
        public MeshSolid(MeshElement owner, List<Vec3> vertices, List<int> triangles)
        {
            this.Owner = owner;
            this.Vertices = vertices;
            this.Triangles = new List<MeshTriangle>();
            this.GridPoints = new List<GridPoint>();
            //Create an array for creating edges
            this.Triangles = new List<MeshTriangle>();
           
            //use an array to store the edge index
            Dictionary<int,Dictionary<int,int>> arrEdge = new Dictionary<int, Dictionary<int, int>>();
            for (int i=0;i<=triangles.Count -1;i+=3)
            {
                int triIndex=(int)Math.Floor((double)i/3);
                int vi0=triangles[i];
                int vi1 = triangles[i + 1];
                int vi2= triangles[i + 2];
                int[] visInCurTri=new int[3] { vi0,vi1,vi2};
                //create triangles
                MeshTriangle tri = new MeshTriangle(this, visInCurTri);
                this.Triangles.Add(tri);
            }
            
        }

        public void GetBoundingBox(out Vec3 min,out Vec3 max)
        {
            min = Vec3.Zero;
            max=Vec3.Zero;
            if (this.Vertices.Count != 0)
            {
                double dblXMax = double.MinValue;
                double dblYMax = double.MinValue;
                double dblZMax = double.MinValue;
                double dblXMin = double.MaxValue;
                double dblYMin = double.MaxValue;
                double dblZMin = double.MaxValue;
                foreach (var v in this.Vertices)
                {
                    dblXMax = Math.Max(dblXMax, v.X);
                    dblYMax = Math.Max(dblYMax, v.Y);
                    dblZMax = Math.Max(dblZMax, v.Z);
                    dblXMin = Math.Min(dblXMin, v.X);
                    dblYMin = Math.Min(dblYMin, v.Y);
                    dblZMin = Math.Min(dblZMin, v.Z);
                }
                min = new Vec3(dblXMin, dblYMin, dblZMin);
                max = new Vec3(dblXMax, dblYMax, dblZMax);
            }
            
        }

        public void GenerateGridPoints(Vec3 origin, double voxelSize)
        {
            this.GridPoints = new List<GridPoint>();
            foreach (var v in this.Vertices)
            {
                var v2Origin = (v - origin) / voxelSize;
                double colRnd = Math.Round (v2Origin.X,3);
                double rowRnd =Math.Round (v2Origin.Y,3);
                int col =(int) Math.Floor(colRnd);
                int row =(int) Math.Floor(rowRnd);
                GridPointType gpt = GridPointType.VGP;
                if(col==colRnd && row!=rowRnd) //CGP
                {
                    gpt = GridPointType.CGP;
                }
                else if(col!=colRnd && row==rowRnd)//rgp
                {
                    gpt = GridPointType.RGP;
                }
                else if(col==colRnd && row==rowRnd) //igp
                {
                    gpt = GridPointType.IGP;
                }
                GridPoint gp = new GridPoint(v.X,v.Y, col, row, v.Z, gpt);
                //result.Add(gp);
                this.GridPoints.Add(gp);
            }
        }
        public List<GridPoint> OutputGridPoints(Vec3 origin,double voxelSize)
        {
            var result = new List<GridPoint>();
            foreach (var v in this.Vertices)
            {
                var v2Origin = (v - origin) / voxelSize;
                double colRnd = Math.Round(v2Origin.X, 3);
                double rowRnd = Math.Round(v2Origin.Y, 3);
                int col = (int)Math.Floor(colRnd);
                int row = (int)Math.Floor(rowRnd);
                GridPointType gpt = GridPointType.VGP;
                if (col == colRnd && row != rowRnd) //CGP
                {
                    gpt = GridPointType.CGP;
                }
                else if (col != colRnd && row == rowRnd)//rgp
                {
                    gpt = GridPointType.RGP;
                }
                else if (col == colRnd && row == rowRnd) //igp
                {
                    gpt = GridPointType.IGP;
                }
                GridPoint gp = new GridPoint(v.X, v.Y, col, row, v.Z, gpt);
                //result.Add(gp);
                result.Add(gp);
            }
            return result;
        }
        public List<Voxel> GenerateVoxelByRawData(Vec3 origin,double voxSize, bool fillAfterMerge, double minGapHeight)
        {
           
            if (this.Triangles.Count == 0)
            {
               
                return new List<Voxel>();
            }
               
            // get voxel boundary
            this.GetBoundingBox(out var sldMin, out var sldMax);
            var colMin = (int)Math.Ceiling(Math.Round((sldMin.X - origin.X) / voxSize,4)) - 1;
            var colMax= (int)Math.Floor(Math.Round((sldMax.X - origin.X) / voxSize, 4)) + 1;
            var rowMin = (int)Math.Ceiling(Math.Round((sldMin.Y - origin.Y) / voxSize, 4)) - 1;
            var rowMax =(int) Math.Floor(Math.Round((sldMax.Y - origin.Y) / voxSize, 4)) + 1;
            //Group voxel by col and Rows
            int colRng = colMax - colMin + 1;
            int rowRng = rowMax - rowMin + 1;
            List<VoxSortHelper>[,] voxInSld = new List<VoxSortHelper>[colRng, rowRng];
            int voxIndex = 0;
            //use an array to collect solid vertices GPs
            GridPoint[] solidVerticeGPs = new GridPoint[this.Vertices.Count];
            for(int i=0;i<this.Vertices.Count;i++)
            {
                solidVerticeGPs[i]=new GridPoint(this.Vertices[i],origin,voxSize);
            }
            foreach (var tri in this.Triangles)
            {
                foreach(var voxel in tri.GenerateVoxelData(solidVerticeGPs,origin, voxSize, voxIndex))
                {
                    var colLoc = voxel.ColIndex - colMin;
                    var rowLoc = voxel.RowIndex - rowMin;
                    var voxColRaw = voxInSld[colLoc, rowLoc];
                    var vh = new VoxSortHelper(voxel);
                    if (voxInSld[colLoc, rowLoc] == null)
                    {
                        voxInSld[colLoc, rowLoc] = new List<VoxSortHelper>() { vh };
                    }
                    else
                    {
                        voxColRaw.Add(vh);
                    }
                    voxIndex += 1;
                }
            }
           
            
            //merge voxels
            MergeIntersectedVoxelData(ref voxInSld, minGapHeight);
            //fill voxels
            MergeVoxelsByGaps(ref voxInSld, minGapHeight,out var gaps);
            //Generate Voxels
            List<Voxel> result = new List<Voxel>();
            for(int col=0;col<=voxInSld.GetUpperBound(0);col++)
            {
                for(int row=0;row<=voxInSld.GetUpperBound(1);row++)
                {
                    var voxData=voxInSld[col, row];
                    if(voxData !=null && voxData.Count > 0)
                    {
                        foreach (var vd in voxData)
                        {
                            result.Add(new Voxel()
                            {
                                ColIndex = col+colMin,
                                RowIndex = row+rowMin,
                                BottomElevation = vd.BottomElevation,
                                TopElevation = vd.TopElevation,
                                
                            });
                        }
                    }
                }
            }
            return result;
        }
        public List<Voxel> GenerateVoxelByRawDataAndRecordTime(Vec3 origin, double voxSize, bool fillAfterMerge, double minGapHeight,ref double timeGenGrid,ref double tiemGenVox,ref double timeMergeVox,ref double timeFillVox)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            if (this.Triangles.Count == 0)
            {
                sw.Stop();
                return new List<Voxel>();
            }

            // get voxel boundary
            this.GetBoundingBox(out var sldMin, out var sldMax);
            var colMin = (int)Math.Ceiling(Math.Round((sldMin.X - origin.X) / voxSize, 4)) - 1;
            var colMax = (int)Math.Floor(Math.Round((sldMax.X - origin.X) / voxSize, 4)) + 1;
            var rowMin = (int)Math.Ceiling(Math.Round((sldMin.Y - origin.Y) / voxSize, 4)) - 1;
            var rowMax = (int)Math.Floor(Math.Round((sldMax.Y - origin.Y) / voxSize, 4)) + 1;
            //Group voxel by col and Rows
            int colRng = colMax - colMin + 1;
            int rowRng = rowMax - rowMin + 1;
            List<VoxSortHelper>[,] voxInSld = new List<VoxSortHelper>[colRng, rowRng];
            int voxIndex = 0;
            //use an array to collect solid vertices GPs
            GridPoint[] solidVerticeGPs = new GridPoint[this.Vertices.Count];
            for (int i = 0; i < this.Vertices.Count; i++)
            {
                solidVerticeGPs[i] = new GridPoint(this.Vertices[i], origin, voxSize);
            }
            foreach (var tri in this.Triangles)
            {
                foreach (var voxel in tri.GenerateVoxelDataByRecordTime(solidVerticeGPs, origin, voxSize, voxIndex,ref timeGenGrid,ref tiemGenVox))
                {
                    var colLoc = voxel.ColIndex - colMin;
                    var rowLoc = voxel.RowIndex - rowMin;
                    var voxColRaw = voxInSld[colLoc, rowLoc];
                    var vh = new VoxSortHelper(voxel);
                    if (voxInSld[colLoc, rowLoc] == null)
                    {
                        voxInSld[colLoc, rowLoc] = new List<VoxSortHelper>() { vh };
                    }
                    else
                    {
                        voxColRaw.Add(vh);
                    }
                    voxIndex += 1;
                }
            }
            sw.Stop();
            sw.Restart();
            //merge voxels
            MergeIntersectedVoxelData(ref voxInSld, minGapHeight);
            sw.Stop();
            timeMergeVox += sw.ElapsedMilliseconds;
            sw.Restart();
            //fill voxels
            MergeVoxelsByGaps(ref voxInSld, minGapHeight, out var gaps);
            sw.Stop();
            timeFillVox += sw.ElapsedMilliseconds;
            //Generate Voxels
            List<Voxel> result = new List<Voxel>();
            for (int col = 0; col <= voxInSld.GetUpperBound(0); col++)
            {
                for (int row = 0; row <= voxInSld.GetUpperBound(1); row++)
                {
                    var voxData = voxInSld[col, row];
                    if (voxData != null && voxData.Count > 0)
                    {
                        foreach (var vd in voxData)
                        {
                            result.Add(new Voxel()
                            {
                                ColIndex = col + colMin,
                                RowIndex = row + rowMin,
                                BottomElevation = vd.BottomElevation,
                                TopElevation = vd.TopElevation,

                            });
                        }
                    }
                }
            }
            return result;
        }
        private void MergeIntersectedVoxelData(ref List<VoxSortHelper>[,] voxel2Merge, double minGapWidth)
        {
            var colRng = voxel2Merge.GetUpperBound(0) + 1;
            var rowRng = voxel2Merge.GetUpperBound(1) + 1;
            int numVoxMerged = 0;
            for (int colLoc = 0; colLoc < colRng; colLoc++)
            {
                for (int rowLoc = 0; rowLoc < rowRng; rowLoc++)
                {
                    var voxelOriginal = voxel2Merge[colLoc, rowLoc];
                    if (voxelOriginal != null)
                    {
                        var voxOdd = voxelOriginal.Where(c => c.IsOdd).ToList();
                        var mergedVoxel = MergeVoxelColumnData(voxelOriginal, numVoxMerged, true, minGapWidth);
                        voxel2Merge[colLoc, rowLoc] = mergedVoxel;
                        numVoxMerged += mergedVoxel.Count;
                    }
                }
            }
           
        }

        
        private List<VoxSortHelper> MergeVoxelColumnData(List<VoxSortHelper> voxelOriginal,int indexStart, bool needSort, double minGapHeight)
        {
            var faceOffset = minGapHeight / 2;
            
            List<VoxSortHelper> mergedVoxels = new List<VoxSortHelper>() { Capacity = voxelOriginal.Count };
            
            var voxes2Merge = voxelOriginal.ToArray();
            if (needSort)
            {
                var btmElevArr = voxelOriginal.Select(c => c.BottomElevation).ToArray();
                Array.Sort(btmElevArr, voxes2Merge);
                
               // voxes2Merge = voxelOriginal.OrderBy(c => c.BottomElevation).ToList();
            }
           
            int snakePointer = 0;
            int foodPointer = 1;
            var snakeVoxel = voxes2Merge[snakePointer];
            //if there is only one voxel, add it directly
            while (foodPointer < voxes2Merge.Length)
            {
                var foodVoxel = voxes2Merge[foodPointer];
                //check if snake voxels intersects food voxels
                if (Math.Round(snakeVoxel.BottomElevation - foodVoxel.TopElevation - minGapHeight, 4) <= 0 &&
                    Math.Round(snakeVoxel.TopElevation - foodVoxel.BottomElevation + minGapHeight, 4) >= 0)
                {
                    snakeVoxel.TopElevation = Math.Max(snakeVoxel.TopElevation, foodVoxel.TopElevation);
                    //if the snaek voxel is odd and the food is common, modify the type of the voxel as common
                    if (snakeVoxel.IsOdd  && !(foodVoxel.IsOdd))
                    {
                        snakeVoxel.IsOdd = false ;
                    }
                    foodPointer += 1;
                }
                else //snake voxel and food voxel do not intersect
                {
                    mergedVoxels.Add(snakeVoxel);
                    snakePointer = foodPointer;
                    snakeVoxel = voxes2Merge[snakePointer];
                    foodPointer += 1;
                }
            }
            //add last snake voxel
            mergedVoxels.Add(snakeVoxel);
            //Modify the index of the merged voxel
            for (int i=0;i<=mergedVoxels.Count -1;i++)
            {
                var mergedVoxel = mergedVoxels[i];
                mergedVoxel.Index = indexStart + i;
                mergedVoxels[i]= mergedVoxel;
            }
            return mergedVoxels; 
        }


        private void MergeVoxelsByGaps(ref List<VoxSortHelper>[,] mergedVoxels, double offset,out List<VoxelRangeData>[,] GapArray)
        {
            //Obtain the gap Range 
            int colRng = mergedVoxels.GetUpperBound(0) + 1;
            int rowRng=mergedVoxels.GetUpperBound(1) + 1;
            int gapColRng = colRng + 2;
            int gapRowRng = rowRng + 2;
            //scan mergedVoxels for finding the minimum and maximum elev of 
            double maxTopElev = double.MinValue;
            double minBtmElev = double.MaxValue;
            foreach (var voxDataCol in mergedVoxels)
            {
                if(voxDataCol!=null)
                {
                    foreach (var v in voxDataCol)
                    {
                        maxTopElev =Math.Max (v.TopElevation , maxTopElev);
                        minBtmElev = Math.Min(minBtmElev, v.BottomElevation);
                    }
                }
            }
            //init the gap rng
            GapArray = new List<VoxelRangeData>[gapColRng,gapRowRng];
            //add gap
            for(int col=0;col<=gapColRng-1;col++)
            {
                for(int row=0;row<=gapRowRng-1;row++)
                {
                    List<VoxelRangeData> gaps = new List<VoxelRangeData>();
                   
                    //try get the voxSortHelper
                    if (col == 0 || row == 0 || col == gapColRng - 1 || row == gapRowRng - 1 || mergedVoxels[col - 1, row- 1] == null)
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
                        var voxData = mergedVoxels[col - 1, row- 1];
                        int numGaps = voxData.Count + 1;
                        //add first gap
                        if (Math.Round ( voxData[0].BottomElevation-minBtmElev,4)!=0)
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
                        if(Math.Round ( voxUpperElev -maxTopElev,4)!=0)
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
            Stack<VoxelRangeData> outerRanges=new Stack<VoxelRangeData>();
            foreach (var items in GapArray)
            {
                foreach (var gap in items)
                {
                    if (gap!=null && gap.IsOutside == true)//gap is outside
                    {
                        outerRanges.Push(gap);
                    }
                }
            }
            //flood fill
            while(outerRanges.Count > 0)
            {
                var gapOut=outerRanges.Pop();
                var colOut = gapOut.ColIndex;
                var rowOut = gapOut.RowIndex;
              
                //sacn neighbors
                for(int colOff=-1;colOff<=1;colOff++)
                {
                    for (int rowOff=-1;rowOff<=1;rowOff++)
                    {
                        if (colOff != 0 && rowOff != 0)
                            continue;
                        var colNear = colOut + colOff;
                        var rowNear = rowOut + rowOff;
                        if(colNear >=0 && colNear <gapColRng && rowNear >=0 && rowNear <gapRowRng)
                        {
                            var gapsNear = GapArray[colNear, rowNear];
                            if (gapsNear != null)
                            {
                                //find the gap intersecting gapOut
                                foreach (var gap2Check in gapsNear)
                                {
                                    if (gap2Check !=null && gap2Check.IsOutside == false && gapOut.Intersect(gap2Check))
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
            for(int col=0;col<colRng;col++)
            {
                for(int row=0;row<rowRng;row++)
                {
                    var voxColumn = mergedVoxels[col,row];
                    if(voxColumn!=null)
                    {
                        List<VoxSortHelper> snakeVoxels = new List<VoxSortHelper>();
                        var snakePt = 0;
                        var foodPt = 1;
                        var snakeVox = voxColumn[snakePt];
                        while (foodPt<voxColumn.Count)
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
                                snakeVoxels.Add (snakeVox);
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
      
        public MeshSolid CopyByTransform(Transform trf)
        {
            var solidCopy= new MeshSolid()
            {
                Owner = this.Owner,
                Vertices = trf.OfPoints(this.Vertices)
            };
            List<MeshTriangle> meshTriangles = new List<MeshTriangle>();
            foreach (var tri in this.Triangles)
            {
                MeshTriangle newTri = new MeshTriangle(solidCopy, tri.VerticesIndex) ;
                meshTriangles.Add(newTri);
            }
            solidCopy.Triangles = meshTriangles;
            return solidCopy;
        }
    }
    public  class MeshInstance
    {
        public Transform Transform { get; set; } = Transform.Idnentity;

    }
    public enum TriangleType
    {
        Common=0,
        ColOverlap=1,
        RowOverlap=2,
    }
    public class MeshTriangle 
    {
        public List<GridPoint> GridPoints { get; set; }
        public MeshSolid Host { get; set; }
        public int[] VerticesIndex { get; set; }
        
        public TriangleInstance Instance { get; set; }

        public TriangleType TriangleType { get; private set; } = TriangleType.Common;

        public MeshTriangle(MeshSolid host, int[] verticesIndex)
        {
            this.Host = host;
            this.VerticesIndex = verticesIndex;
            this.GridPoints = new List<GridPoint>() ;
           
        }

        public Vec3 Get_Vertex(int index)
        {
            return this.Host.Vertices[this.VerticesIndex[index]];
        }
       
        public double Get_ProjectionArea()
        {
            var v0 = this.Get_Vertex(0);
            var v1 = this.Get_Vertex(1);
            var v2 = this.Get_Vertex(2);
            var v01 = v1 - v0;
            var v02 = v2 - v0;
            var v01New = new Vec3(v01.X, v01.Y, 0);
            var v02New=new Vec3(v02.X ,v02.Y,0);
            return v01New.CrossProduct(v02New).GetLength();
        }
        public IEnumerable<GridPoint> ObtainGridPoints(Vec3 origin, double voxelSize)
        {
            //Get triangle type
            this.TriangleType = GetTriangleType(origin, voxelSize);
            this.GridPoints = new List<GridPoint>();
            //GetTriangleNormal n
            int[] triVertices = this.VerticesIndex;
            int vi0 = triVertices[0];
            int vi1 = triVertices[1];
            int vi2 = triVertices[2];

            Vec3 v0 = this.Host.Vertices[vi0];
            Vec3 v1 = this.Host.Vertices[vi1];
            Vec3 v2 = this.Host.Vertices[vi2];
            //get triangle boundary
            int colMin = int.MaxValue;
            int colMax = int.MinValue;
            int rowMin = int.MaxValue;
            int rowMax = int.MinValue;
            //compute triNorm
            var vec01 = v1 - v0;
            var vec12 = v2 - v1;
            var norm = (vec01.CrossProduct(vec12)).Normalize();
            for (int i = 0; i <= 2; i++)
            {
                var p0 = this.Get_Vertex(i);
                var dist = p0 - origin;
                var colMinTemp = (int)Math.Ceiling(dist.X / voxelSize) - 1;
                var colMaxTemp = (int)Math.Floor(dist.X / voxelSize) + 1;
                var rowMinTemp = (int)Math.Ceiling(dist.Y / voxelSize) - 1;
                var rowMaxTemp = (int)Math.Floor(dist.Y / voxelSize) + 1;
                colMin = Math.Min(colMinTemp, colMin);
                colMax = Math.Max(colMaxTemp, colMax);
                rowMin = Math.Min(rowMinTemp, rowMin);
                rowMax = Math.Max(rowMaxTemp, rowMax);
            }
            //determine if the innerGps(if any) is generated by col or rows
            int colRng = colMax - colMin + 1;
            int rowRng = rowMax - rowMin + 1;
            GridPoint[] colBdryElev_Max = new GridPoint[colRng];
            GridPoint[] colBdryElev_Min = new GridPoint[colRng];
            for (int i = 0; i <= 2; i++)
            {
                //scan vertices
                var p0 = this.Get_Vertex(i % 3);
                var p1 = this.Get_Vertex((i + 1) % 3);
                var gpP0 = new GridPoint(p0, origin, voxelSize);
                
                if (gpP0.GridType == GridPointType.CGP || gpP0.GridType == GridPointType.IGP)
                {
                    UpdateGridPointRowRange(gpP0, colMin, ref colBdryElev_Min, ref colBdryElev_Max);
                }
                //scan col
                //List<GridPoint> gpScanCol = GetGridPointAlongXAxis(p0, p1, origin, voxelSize, true);
                //this.GridPoints.AddRange(gpScanCol);
                foreach (var gp in GetGridPointAlongXAxis(p0, p1, origin, voxelSize, true))
                {
                    UpdateGridPointRowRange(gp, colMin, ref colBdryElev_Min, ref colBdryElev_Max);
                    yield return gp;
                }
                //scan row
                bool addIGPWhenScanRow = false;
                if (this.TriangleType == TriangleType.ColOverlap)
                {
                    addIGPWhenScanRow = true;
                }
                foreach (var gp in GetGridPointAlongYAxis(p0, p1, origin, voxelSize, addIGPWhenScanRow))
                {
                    yield return gp;
                }
            }
            if (Math.Round(norm.Z, 4) != 0) ////generate inner gp, only for non-vertical voxels
            {
                for (int col = 0; col <= colRng - 1; col++)
                {
                    GridPoint p0_Min = colBdryElev_Min[col];
                    GridPoint p1_Max = colBdryElev_Max[col];
                    if (p0_Min != null && p1_Max != null)
                    {
                        var p0 = (GridPoint)(p0_Min);
                        var p1 = (GridPoint)(p1_Max);
                        if (p0.Row != p1.Row)
                        {
                            var col2Scan = p0.Column;
                            var rowSt = p0.Row + 1;
                            var rowEd = p1.Row;
                            if (p1.GridType == GridPointType.IGP)// rowed minus 1
                            {
                                rowEd -= 1;
                            }
                            foreach (var igp in  GetInnerPtByCol(col2Scan, rowSt, rowEd, v0, norm, origin, voxelSize))
                            {
                                yield return igp;
                            }
                        }
                    }
                }
            }
        }

        public IEnumerable<GridPoint> ObtainGridPoints(Vec3 origin, double voxelSize, GridPoint[] solidVerticesGPs)
        {
            //Get triangle type
            this.TriangleType = GetTriangleType(origin, voxelSize);
            this.GridPoints = new List<GridPoint>();
            //GetTriangleNormal n
            int[] triVertices = this.VerticesIndex;
            int vi0 = triVertices[0];
            int vi1 = triVertices[1];
            int vi2 = triVertices[2];

            Vec3 v0 = this.Host.Vertices[vi0];
            Vec3 v1 = this.Host.Vertices[vi1];
            Vec3 v2 = this.Host.Vertices[vi2];
            //get triangle boundary
            int colMin = int.MaxValue;
            int colMax = int.MinValue;
            int rowMin = int.MaxValue;
            int rowMax = int.MinValue;
            //compute triNorm
            var vec01 = v1 - v0;
            var vec12 = v2 - v1;
            var norm = (vec01.CrossProduct(vec12)).Normalize();
            //var norm = Vec3.BasisZ;
           
            for (int i = 0; i <= 2; i++)
            {
                var p0 = this.Get_Vertex(i);
                var dist = p0 - origin;
                var colMinTemp = (int)Math.Ceiling(dist.X / voxelSize) - 1;
                var colMaxTemp = (int)Math.Floor(dist.X / voxelSize) + 1;
                var rowMinTemp = (int)Math.Ceiling(dist.Y / voxelSize) - 1;
                var rowMaxTemp = (int)Math.Floor(dist.Y / voxelSize) + 1;
                colMin = Math.Min(colMinTemp, colMin);
                colMax = Math.Max(colMaxTemp, colMax);
                rowMin = Math.Min(rowMinTemp, rowMin);
                rowMax = Math.Max(rowMaxTemp, rowMax);
            }
            //determine if the innerGps(if any) is generated by col or rows
            int colRng = colMax - colMin + 1;
            int rowRng = rowMax - rowMin + 1;
            GridPoint[] colBdryElev_Max = new GridPoint[colRng];
            GridPoint[] colBdryElev_Min = new GridPoint[colRng];
            for (int i = 0; i <= 2; i++)
            {
                int curPtIdx = i % 3;
                int nextPtIdx=(i+1)%3;  
                //scan vertices
                var p0 = this.Get_Vertex(curPtIdx);
                var p1 = this.Get_Vertex(nextPtIdx);
                int curPtIdxGlobal = VerticesIndex[curPtIdx];
                int nextPtIdxGlobal=VerticesIndex[nextPtIdx];
                //check if the verticeIndex has been generated
                if (solidVerticesGPs[curPtIdxGlobal]==null)
                {
                    solidVerticesGPs[curPtIdxGlobal] = new GridPoint(p0, origin, voxelSize);
                }
                var gpP0 = solidVerticesGPs[curPtIdxGlobal];
                var gpP1 = solidVerticesGPs[nextPtIdxGlobal];
                yield return gpP0;
               
                //this.GridPoints.Add(gpP0);

                if (gpP0.GridType == GridPointType.CGP || gpP0.GridType == GridPointType.IGP)
                {
                    UpdateGridPointRowRange(gpP0, colMin, ref colBdryElev_Min, ref colBdryElev_Max);
                }
               
                //scan col
                //List<GridPoint> gpScanCol = GetGridPointAlongXAxis(p0, p1, origin, voxelSize, true);
                //this.GridPoints.AddRange(gpScanCol);
               
                foreach (var gp in GetGridPointAlongXAxis(p0, p1, origin, voxelSize, true))
                {
                    UpdateGridPointRowRange(gp, colMin, ref colBdryElev_Min, ref colBdryElev_Max);
                    yield return gp;
                }
                
                //scan row
                bool addIGPWhenScanRow = false;
                if (this.TriangleType == TriangleType.ColOverlap)
                {
                    addIGPWhenScanRow = true;
                }
                foreach (var gp in GetGridPointAlongYAxis( p0, p1, origin, voxelSize, addIGPWhenScanRow))
                {
                    yield return gp;
                }
                
            }
            
            if (Math.Round(norm.Z, 4) != 0) ////generate inner gp, only for non-vertical voxels
            {
                for (int col = 0; col <= colRng - 1; col++)
                {
                    GridPoint p0_Min = colBdryElev_Min[col];
                    GridPoint p1_Max = colBdryElev_Max[col];
                    if (p0_Min != null && p1_Max != null)
                    {
                        var p0 = (GridPoint)(p0_Min);
                        var p1 = (GridPoint)(p1_Max);
                        if (p0.Row != p1.Row)
                        {
                            var col2Scan = p0.Column;
                            var rowSt = p0.Row + 1;
                            var rowEd = p1.Row;
                            if (p1.GridType == GridPointType.IGP)// rowed minus 1
                            {
                                rowEd -= 1;
                            }
                            foreach (var igp in GetInnerPtByCol(col2Scan, rowSt, rowEd, v0, norm, origin, voxelSize))
                            {
                                yield return igp;
                            }
                        }
                    }
                }
            }
            
        }
        public void UpdateGridPointRowRange(GridPoint pt, int colMin,  ref GridPoint[] min,ref GridPoint[] max)
        {
            var colLoc = pt.Column - colMin;
            //update max
            GridPoint maxPtLoc = max[colLoc];
            if (maxPtLoc== null)
            {
                max[colLoc] = (GridPoint)pt;
            }
            else
            {
                var gpExist = max[colLoc];
                if (gpExist.Row < pt.Row)
                {
                    max[colLoc] = pt;
                }
            }
            //update min
            GridPoint minPtLoc = min[colLoc];
            if (minPtLoc == null)
            {
                min[colLoc] = (GridPoint)pt;
            }
            else
            {
                var gpExist = min[colLoc];
                if (gpExist.Row > pt.Row)
                {
                    min[colLoc] = pt;
                }
            }
        }
        private IEnumerable<GridPoint> GetGridPointAlongXAxis(Vec3 pt0,Vec3 pt1,Vec3 origin,double voxSize,bool includIGP)
        {
            double voxSizeInv = 1 / voxSize;
            //Get col range excluding the edge vertices
            // List<GridPoint> result = new List<GridPoint>();
            double xSt = Math.Min(pt0.X, pt1.X);
            double xEd = Math.Max(pt0.X, pt1.X);
            double dblCol0 = Math.Round((xSt - origin.X) / voxSize, 4);
            double dblCol1 = Math.Round((xEd- origin.X) / voxSize, 4);
            int colSt = (int)Math.Floor(dblCol0)+1;
            int colEd = (int)Math.Ceiling(dblCol1)-1;
            if (colSt>colEd) //if so, ignore the col range
            {
                yield break;
            }
            else
            {
                Vec3 edgeDir = (pt1 - pt0);
                double paramY_X = edgeDir.Y / edgeDir.X;
                double paramZ_X = edgeDir.Z / edgeDir.X;
                for (int col = colSt; col <=colEd; col++)
                {
                    double x = origin.X + col * voxSize;
                    double y = paramY_X * (x - pt0.X) + pt0.Y;
                    double z = paramZ_X * (x - pt0.X) + pt0.Z;
                    double dblY = Math.Round((y - origin.Y) * voxSizeInv, 4);
                    int intRow= (int)Math.Floor(dblY);
                    GridPointType gpType = GridPointType.CGP;
                    if(intRow ==dblY)
                    {
                        gpType = GridPointType.IGP;
                        if(includIGP)
                        {
                            yield  return new GridPoint(x, y, col, intRow, z, gpType);
                        }
                    }
                    else
                    {
                        yield return new GridPoint(x, y, col, intRow, z, gpType);
                    }

                }
            }
            //return result;
        }
        private IEnumerable<GridPoint> GetGridPointAlongYAxis(Vec3 pt0, Vec3 pt1, Vec3 origin, double voxSize, bool includIGP)
        {
            //Get col range excluding the edge vertices
            //List<GridPoint> result = new List<GridPoint>();
            double voxSizeInv = 1 / voxSize;
            double ySt = Math.Min(pt0.Y, pt1.Y);
            double yEd = Math.Max(pt0.Y, pt1.Y);
            double dblRow0 = Math.Round((ySt - origin.Y) / voxSize, 4);
            double dblRow1 = Math.Round((yEd - origin.Y) / voxSize, 4);
            int rowSt = (int)Math.Floor(dblRow0) + 1;
            int rowEd = (int)Math.Ceiling(dblRow1) - 1;
            if (rowSt > rowEd) //if so, ignore the rowl range
            {
                yield break;
            }
            else
            {
                Vec3 edgeDir = (pt1 - pt0);
                double paramX_Y = edgeDir.X / edgeDir.Y;
                double paramZ_Y = edgeDir.Z / edgeDir.Y;
                
                for (int row = rowSt; row <= rowEd; row++)
                {
                    double y = origin.Y + row * voxSize;
                    double deltaY = (y - pt0.Y);
                    double x = paramX_Y * deltaY + pt0.X;
                    double z = paramZ_Y * deltaY + pt0.Z;
                    double dblCol = Math.Round ((x - origin.X) *voxSizeInv,4);
                    int intCol = (int) Math.Floor( dblCol);
                    if(dblCol ==intCol && includIGP) //IGP
                    {
                        yield  return new GridPoint(x, y, intCol, row, z, GridPointType.IGP);
                    }
                    else if(dblCol!=intCol) 
                    {
                        yield return new GridPoint(x, y, intCol, row, z, GridPointType.RGP);
                    }
                }
            }
            //return result;
        }
        private IEnumerable<GridPoint> GetInnerPtByCol(int col, int rowSt,int rowEd,Vec3 triOrigin, Vec3 triNorm,Vec3 origin, double voxelSize)
        {
           
            var v0 = triOrigin;
            double paramXZ = triNorm.X / triNorm.Z;
            double paramYZ = triNorm.Y / triNorm.Z;
            for (int row = rowSt; row <= rowEd; row++)
            {
                double x = col * voxelSize + origin.X;
                double y = row * voxelSize + origin.Y;
                double z = v0.Z - paramXZ * (x - v0.X) - paramYZ * (y - v0.Y);
                GridPoint IGP = new GridPoint(x, y, col, row, z, GridPointType.IGP);
                yield return IGP;
            }
            
        }
        
        /// <summary>
        /// Determine the triangle type
        /// the type is :normal,col,row
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="voxelSize"></param>
        public TriangleType GetTriangleType(Vec3 origin,double voxelSize)
        {
            //return TriangleType.Common;
            //check if the triangle overlaps to the row or col plane
            //if a triangle parallel to  and only contains CGP/RGP and IGP, 
            var pt0 = this.Host.Vertices[this.VerticesIndex[0]];
            var pt1 = this.Host.Vertices[this.VerticesIndex[1]];
            var pt2 = this.Host.Vertices[this.VerticesIndex[2]];
            var vec01 = pt1 - pt0;
            var vec02 = pt2 - pt0;
            
            if (Math.Round( vec01.DotProduct(Vec3.BasisX),4) == 0 && Math.Round ( vec02.DotProduct(Vec3.BasisX),4) == 0) //the triangle overlaps with the col plane
            {
                var dblCol = Math.Round((pt0.X - origin.X) / voxelSize, 4);
                var colMin = Math.Ceiling(dblCol);
                var colMax = Math.Floor(dblCol);
                if(colMin ==colMax)
                {
                    return TriangleType.ColOverlap;
                }
                else
                {
                    return TriangleType.Common;
                }
            }
            else if (Math.Round (  vec01.DotProduct(Vec3.BasisY),4) == 0 && Math.Round ( vec02.DotProduct(Vec3.BasisY),4) == 0) //the triangle overlaps with the row plane
            {
                var dblRow = Math.Round((pt0.Y - origin.Y) / voxelSize, 4);
                var rowMin = Math.Ceiling(dblRow);
                var rowMax = Math.Floor(dblRow);
                if(rowMax ==rowMin )
                {
                    return TriangleType.RowOverlap;
                }
                else
                {
                    return TriangleType.Common;
                }
            }
            else
            {
                return TriangleType.Common;
            }
        }
        public IEnumerable<VoxelRawData> GenerateVoxelData(GridPoint[] solidVerticeGPs, Vec3 origin,double voxSize,int startIndex)
        {
            //modify row and col max
            int colMax = int.MinValue;
            int colMin = int.MaxValue;
            int rowMax = int.MinValue;
            int rowMin = int.MaxValue;
            for(int i=0;i<=2;i++)
            {
                var pt = this.Get_Vertex(i);
                var dblCol = Math.Round((pt.X - origin.X) / voxSize, 4);
                var dblRow=Math.Round ((pt.Y -origin.Y ) / voxSize, 4); 
                var colLower=(int) Math.Ceiling(dblCol) -1;
                var colUpper=(int) Math.Floor(dblCol) +1;
                var rowLower = (int)Math.Ceiling(dblRow) - 1;
                var rowUpper=(int)Math.Floor(dblRow) + 1;
                rowMax =Math.Max (rowUpper,rowMax);
                rowMin=Math.Min (rowMin ,rowLower);
                colMax=Math.Max (colUpper,colMax);
                colMin = Math.Min(colLower, colMin);
            }
            int colRng = colMax - colMin + 1;
            int rowRng = rowMax - rowMin + 1;
            //Create an array arrarrGPAffRng(colRng,rowRng) to group each gps
            //VGP: can only affect the range[col,row]
            //CGP: can only affect the range[col-1,row],[col,row];
            //RGP:can only affect the range[col,row-1],[col,row];
            //IGP: can affect the range[col-1,row-1], [col,row-1],[col-1,row],[col,row]
            int[,] arrGPCount =new int[colRng, rowRng];
            Tuple<double,double>[,] arrGP=new Tuple<double, double>[colRng, rowRng];
            //create an array to collect grid point at solid vertices to avoid duplication
            
            foreach(var gp in ObtainGridPoints(origin, voxSize,solidVerticeGPs))
            {
                int colLoc = gp.Column - colMin;
                int rowLoc = gp.Row - rowMin;
                arrGPCount[colLoc, rowLoc] += 1;
                UpdateGPRange(gp.Z, arrGP, colLoc, rowLoc);
                switch (gp.GridType)
                {
                    case GridPointType.VGP:
                        break;
                    case GridPointType.CGP:
                        if (colLoc - 1 >= 0)
                        {
                            arrGPCount[colLoc - 1, rowLoc] += 1;
                            UpdateGPRange(gp.Z, arrGP, colLoc-1, rowLoc);
                        }  
                        break;
                    case GridPointType.RGP:
                        if (rowLoc - 1 >= 0)
                        {
                            arrGPCount[colLoc, rowLoc - 1] += 1;
                            UpdateGPRange(gp.Z, arrGP, colLoc, rowLoc - 1);
                        } 
                        break;
                    case GridPointType.IGP:
                        
                        if (rowLoc > 0 && colLoc > 0)
                        {
                            arrGPCount[colLoc, rowLoc - 1] += 1;
                            arrGPCount[colLoc - 1, rowLoc] +=1 ;
                            arrGPCount[colLoc - 1, rowLoc - 1] += 1;
                            UpdateGPRange(gp.Z, arrGP, colLoc, rowLoc - 1);
                            UpdateGPRange(gp.Z, arrGP, colLoc-1, rowLoc );
                            UpdateGPRange(gp.Z, arrGP, colLoc-1, rowLoc - 1);
                        }
                        else if (rowLoc > 0)
                        {
                            arrGPCount[colLoc, rowLoc - 1] += 1;
                            UpdateGPRange(gp.Z, arrGP, colLoc, rowLoc - 1);
                        }
                        else if (colLoc > 0)
                        {
                            arrGPCount[colLoc - 1, rowLoc] += 1;
                            UpdateGPRange(gp.Z, arrGP, colLoc - 1, rowLoc);
                        }
                        break;
                }
            }
            //get triangle type
            TriangleType triType = this.TriangleType;
            //Generate voxel data
            for (int colLoc = 0; colLoc < colRng; colLoc++)
            {
                for (int rowLoc = 0; rowLoc < rowRng; rowLoc++)
                {
                    var item = arrGPCount[colLoc, rowLoc];
                    if (item >= 3)
                    {
                        //create an voxel 
                        double voxUpperElev = arrGP[colLoc,rowLoc].Item2;
                        double voxLowerElev = arrGP[colLoc, rowLoc].Item1;
                        VoxelRawData vox;
                        switch (triType)
                        {
                            case TriangleType.Common:
                                vox = new VoxelRawData() {Index =startIndex, ColIndex = colLoc + colMin,RowIndex= rowLoc + rowMin,BottomElevation= voxLowerElev, TopElevation= voxUpperElev };
                                
                                yield return vox;
                                break;
                            case TriangleType.ColOverlap:
                                vox = new VoxelRawData() { Index =startIndex, ColIndex = colLoc + colMin,RowIndex= rowLoc + rowMin,BottomElevation= voxLowerElev,TopElevation= voxUpperElev };
                                vox.VoxType = VoxelType.Odd;
                                yield return vox;
                                break;
                            case TriangleType.RowOverlap:
                                vox = new VoxelRawData() {Index = startIndex, ColIndex= colLoc + colMin,RowIndex= rowLoc + rowMin,BottomElevation= voxLowerElev,TopElevation= voxUpperElev };
                                vox.VoxType = VoxelType.Odd;
                                yield return vox;
                                break;
                        }
                        startIndex += 1;
                        //result.Add(vox);
                    }
                }
            }
            //return result;
        }

        public List<VoxelRawData> GenerateVoxelDataByRecordTime(GridPoint[] solidVerticeGPs, Vec3 origin, double voxSize, int startIndex,ref double timeGenGridPt,ref double timeVox)
        {
            Stopwatch sw = Stopwatch.StartNew();
            List<VoxelRawData> result = new List<VoxelRawData>();
            //modify row and col max
            int colMax = int.MinValue;
            int colMin = int.MaxValue;
            int rowMax = int.MinValue;
            int rowMin = int.MaxValue;
            for (int i = 0; i <= 2; i++)
            {
                var pt = this.Get_Vertex(i);
                var dblCol = Math.Round((pt.X - origin.X) / voxSize, 4);
                var dblRow = Math.Round((pt.Y - origin.Y) / voxSize, 4);
                var colLower = (int)Math.Ceiling(dblCol) - 1;
                var colUpper = (int)Math.Floor(dblCol) + 1;
                var rowLower = (int)Math.Ceiling(dblRow) - 1;
                var rowUpper = (int)Math.Floor(dblRow) + 1;
                rowMax = Math.Max(rowUpper, rowMax);
                rowMin = Math.Min(rowMin, rowLower);
                colMax = Math.Max(colUpper, colMax);
                colMin = Math.Min(colLower, colMin);
            }
            int colRng = colMax - colMin + 1;
            int rowRng = rowMax - rowMin + 1;
            //Create an array arrarrGPAffRng(colRng,rowRng) to group each gps
            //VGP: can only affect the range[col,row]
            //CGP: can only affect the range[col-1,row],[col,row];
            //RGP:can only affect the range[col,row-1],[col,row];
            //IGP: can affect the range[col-1,row-1], [col,row-1],[col-1,row],[col,row]
            int[,] arrGPCount = new int[colRng, rowRng];
            Tuple<double, double>[,] arrGP = new Tuple<double, double>[colRng, rowRng];
            //create an array to collect grid point at solid vertices to avoid duplication
            
            var gps = ObtainGridPoints(origin, voxSize, solidVerticeGPs).ToList();
            sw.Stop();
            timeGenGridPt += sw.ElapsedMilliseconds;
            sw.Restart();
            foreach (var gp in gps)
            {
                
                int colLoc = gp.Column - colMin;
                int rowLoc = gp.Row - rowMin;
                arrGPCount[colLoc, rowLoc] += 1;
                UpdateGPRange(gp.Z, arrGP, colLoc, rowLoc);
                switch (gp.GridType)
                {
                    case GridPointType.VGP:
                        break;
                    case GridPointType.CGP:
                        if (colLoc - 1 >= 0)
                        {
                            arrGPCount[colLoc - 1, rowLoc] += 1;
                            UpdateGPRange(gp.Z, arrGP, colLoc - 1, rowLoc);
                        }
                        break;
                    case GridPointType.RGP:
                        if (rowLoc - 1 >= 0)
                        {
                            arrGPCount[colLoc, rowLoc - 1] += 1;
                            UpdateGPRange(gp.Z, arrGP, colLoc, rowLoc - 1);
                        }
                        break;
                    case GridPointType.IGP:

                        if (rowLoc > 0 && colLoc > 0)
                        {
                            arrGPCount[colLoc, rowLoc - 1] += 1;
                            arrGPCount[colLoc - 1, rowLoc] += 1;
                            arrGPCount[colLoc - 1, rowLoc - 1] += 1;
                            UpdateGPRange(gp.Z, arrGP, colLoc, rowLoc - 1);
                            UpdateGPRange(gp.Z, arrGP, colLoc - 1, rowLoc);
                            UpdateGPRange(gp.Z, arrGP, colLoc - 1, rowLoc - 1);
                        }
                        else if (rowLoc > 0)
                        {
                            arrGPCount[colLoc, rowLoc - 1] += 1;
                            UpdateGPRange(gp.Z, arrGP, colLoc, rowLoc - 1);
                        }
                        else if (colLoc > 0)
                        {
                            arrGPCount[colLoc - 1, rowLoc] += 1;
                            UpdateGPRange(gp.Z, arrGP, colLoc - 1, rowLoc);
                        }
                        break;
                }
            }
            //get triangle type
            TriangleType triType = this.TriangleType;
            //Generate voxel data
            for (int colLoc = 0; colLoc < colRng; colLoc++)
            {
                for (int rowLoc = 0; rowLoc < rowRng; rowLoc++)
                {
                    var item = arrGPCount[colLoc, rowLoc];
                    if (item >= 3)
                    {
                        //create an voxel 
                        double voxUpperElev = arrGP[colLoc, rowLoc].Item2;
                        double voxLowerElev = arrGP[colLoc, rowLoc].Item1;
                        VoxelRawData vox;
                        switch (triType)
                        {
                            case TriangleType.Common:
                                vox = new VoxelRawData() { Index = startIndex, ColIndex = colLoc + colMin, RowIndex = rowLoc + rowMin, BottomElevation = voxLowerElev, TopElevation = voxUpperElev };

                                result.Add( vox);
                                break;
                            case TriangleType.ColOverlap:
                                vox = new VoxelRawData() { Index = startIndex, ColIndex = colLoc + colMin, RowIndex = rowLoc + rowMin, BottomElevation = voxLowerElev, TopElevation = voxUpperElev };
                                vox.VoxType = VoxelType.Odd;
                                result.Add( vox);
                                break;
                            case TriangleType.RowOverlap:
                                vox = new VoxelRawData() { Index = startIndex, ColIndex = colLoc + colMin, RowIndex = rowLoc + rowMin, BottomElevation = voxLowerElev, TopElevation = voxUpperElev };
                                vox.VoxType = VoxelType.Odd;
                                result.Add( vox);
                                break;
                        }
                        startIndex += 1;
                        
                    }
                }
            }
            sw.Stop();
            timeVox += sw.ElapsedMilliseconds;
            return result;
        }
        public void UpdateGPRange(double z,Tuple<double,double>[,] gpRng,int col,int row)
        {
            if (gpRng[col, row] == null)
            {
                gpRng[col, row] = new Tuple<double, double>(z, z);
            }
            else
            {
                var zMin = gpRng[col, row].Item1;
                var zMax = gpRng[col, row].Item2;
                var zMinNew = Math.Min(zMin, z);
                var zMaxNew = Math.Max(zMax, z);
                gpRng[col, row] = new Tuple<double, double>(zMinNew, zMaxNew);
            }
        }

       
    }
    public class TriangleInstance
    {
        public MeshTriangle BaseTriangle { get; set; }
       
        public int ColOffset { get; set; }
        public int RowOffset { get; set; }
        public double ElevationOffset { get; set; }
        public List<VoxelRawData> VoxelData { get; set; } = new List<VoxelRawData>();
    }
    /// <summary>
    /// Voxel data used for generatig voxels
    /// </summary>
    public struct VoxelRawData
    {
        public int Index { get; set; }
        public int ColIndex { get; set; }
        public int RowIndex { get; set; }
        public double BottomElevation { get; set; }
        public double TopElevation { get; set; }
        public VoxelType VoxType { get; set; }
    }

    public struct VoxSortHelper
    {
        public float BottomElevation { get; set; }
        public float TopElevation { get; set; }
        public int Index { get; set; }
        public bool IsBoundaryVoxel { get; set; }
        public bool IsOdd { get; set; }
        public bool BottomOutside { get; internal set; }
        public bool TopOutside { get; internal set; }
        public int ColIndex { get; set; }
        public int RowIndex { get; set; }

        public VoxSortHelper(VoxelRawData data)
        {
            this.BottomElevation = (float)data.BottomElevation;
            this.TopElevation = (float)data.TopElevation;
            this.Index = data.Index;
            IsBoundaryVoxel = false;
            IsOdd = (data.VoxType == VoxelType.Odd);
            BottomOutside = false;
            TopOutside = false;
            ColIndex = data.ColIndex;
            RowIndex = data.RowIndex;
        }
    }
    public class Vec3:IEquatable<Vec3>
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public Vec3()
        {
        }
        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vec3 operator +(Vec3 v1,Vec3 v2)
        {
            return new Vec3(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
        }
        public static Vec3 operator -(Vec3 v1, Vec3 v2)
        {
            return new Vec3(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
        }
        public static Vec3 operator *(double num,Vec3 v)
        {
            return new Vec3(num * v.X, num * v.Y, num * v.Z);
        }
        public static Vec3 operator *(Vec3 v, double num)
        {
            return new Vec3(num * v.X, num * v.Y, num * v.Z);
        }
        public static Vec3 operator /(Vec3 v, double num)
        {
            return v*(1/num);
        }

        public double DotProduct(Vec3 other)
        {
            return this.X * other.X + this.Y * other.Y + this.Z * other.Z;
        }

        public Vec3 CrossProduct(Vec3 other)
        {
            double x= this.Y * other.Z - this.Z * other.Y;
            double y = -this.X * other.Z +this.Z * other.X;
            double z= this.X * other.Y - this.Y * other.X;
            return new Vec3(x, y, z);
        }

        public double GetSquareLen()
        {
            return this.DotProduct(this);
        }
        public double GetLength()
        {
            return Math.Sqrt(GetSquareLen());
        }
        public double GetHorizontalLen()
        {
            return Math.Sqrt(Math.Pow(this.X, 2) + Math.Pow(this.Y, 2));
        }
        public Vec3 Normalize()
        {
            double dblLen = this.GetLength();
            return new Vec3(this.X / dblLen, this.Y / dblLen, this.Z / dblLen);
        }
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
            //return base.GetHashCode();
        }
        public string ToMMString()
        {
            return $"{Math.Round(X * 304.8)},{Math.Round(Y * 304.8)},{Math.Round(Z * 304.8)}";
        }
        public static Vec3 BasisX
        {
            get
            {
               return new Vec3(1, 0, 0);
            }  
        }
        public static Vec3 BasisY
        {
            get
            {
                return new Vec3(0, 1, 0);
            }
        }
        public static Vec3 BasisZ
        {
            get
            {
                return new Vec3(0, 0, 1);
            }
        }
        public static Vec3 Zero
        {
            get
            {
                return new Vec3(0, 0, 0);
            }
        }
        public override string ToString()
        {
            string strX = this.X.ToString("0.0000");
            string strY = this.Y.ToString("0.0000");
            string strZ = this.Z.ToString("0.0000");
            return string.Format("{0},{1},{2}", strX, strY, strZ);
        }
        public string ToString(string saparater)
        {
            string strX = this.X.ToString("0.0000");
            string strY = this.Y.ToString("0.0000");
            string strZ = this.Z.ToString("0.0000");
            return string.Join(saparater, strX, strY, strZ);
        }
        
        public bool Equals(Vec3 other)
        {
            return (this.ToString() == other.ToString());
        }

        public static Vec3 Max(Vec3 vxzy, Vec3 max)
        {
            return new Vec3(Math.Max(vxzy.X, max.X), Math.Max(vxzy.Y, max.Y), Math.Max(vxzy.Z, max.Z));
        }

        public static Vec3 Min(Vec3 vxzy, Vec3 min)
        {
            return new Vec3(Math.Min(vxzy.X, min.X), Math.Min(vxzy.Y, min.Y), Math.Min(vxzy.Z, min.Z));
        }
    }

    public class Vec2
    {
        public double U { get; set; }
        public double V { get; set; }
        public Vec2(double u,double v)
        {
            this.U = u;
            this.V = v;
        }
        public static Vec2 operator +(Vec2 a,Vec2 b)
        {
            return new Vec2(a.U + b.U, a.V + b.V);
        }
        public static Vec2 operator -(Vec2 a, Vec2 b)
        {
            return new Vec2(a.U - b.U, a.V - b.V);
        }
        public static Vec2 operator *(double num, Vec2 a)
        {
            return new Vec2(num*a.U ,num*a.V);
        }
        public static Vec2 operator *( Vec2 a, double num)
        {
            return new Vec2(num * a.U, num * a.V);
        }
        public double DotProcuct(Vec2 other)
        {
            return this.U * other.U + this.V * other.V;
        }
    }
   

    public class Transform
    {
        public Vec3 BasisX { get; set; }
        public Vec3 BasisY { get; set; }
        public Vec3 BasisZ { get; set; }
        public Vec3 Origin { get; set; }

        public Transform(Vec3 bassiX,Vec3 basisY,Vec3 basisZ,Vec3 origin)
        {
            BasisX = bassiX;
            BasisY = basisY;
            BasisZ = basisZ;
            Origin = origin;
        }
        public static Transform Idnentity { get; } = new Transform(Vec3.BasisX, Vec3.BasisY, Vec3.BasisZ, Vec3.Zero);
        public Vec3 OfPoint(Vec3 point)
        {
            var x = point.X * BasisX.X + point.Y * BasisY.X + point.Z * BasisZ.X + Origin.X;
            var y = point.X * BasisX.Y + point.Y * BasisY.Y + point.Z * BasisZ.Y + Origin.Y;
            var z = point.X * BasisX.Z + point.Y * BasisY.Z + point.Z * BasisZ.Z + Origin.Z;
            return new Vec3(x, y, z);
        }
        public Vec3 OfVector(Vec3 vector)
        {
            var x = vector.X * BasisX.X + vector.Y * BasisY.X + vector.Z * BasisZ.X ;
            var y = vector.X * BasisX.Y + vector.Y * BasisY.Y + vector.Z * BasisZ.Y ;
            var z = vector.X * BasisX.Z + vector.Y * BasisY.Z + vector.Z * BasisZ.Z ;
            return new Vec3(x, y, z);
        }
        public Transform Multiply(Transform right)
        {
            var newBasisX = this.OfVector(right.BasisX);
            var newBasisY = this.OfVector(right.BasisY);
            var newBasisZ = this.OfVector(right.BasisZ);
            var newOrigin = this.OfPoint(right.Origin);
            Transform newTransform = new Transform(newBasisX, newBasisY, newBasisZ, newOrigin);
            return new Transform(newBasisX, newBasisY, newBasisZ, newOrigin);
        }

        public List<Vec3> OfPoints(List<Vec3> pts2Transform)
        {
            List<Vec3> result = new List<Vec3>();
            pts2Transform.ForEach(c => result.Add(this.OfPoint(c)));
            return result;
        }
    }
    #endregion
    #region Voxel
    public interface IGridPointHost
    {
        List<GridPoint> GridPoints { get; set; }
        void GenerateGridPoints(Vec3 origin, double voxelSize);
        
    }
    public static class VoxelDocumentConverter
    {
        public static void SaveVoxelDocument(VoxelDocument vdoc, string saveFilePath)
        {
            //file format:
            //1. Origin:24bytes;
            List<byte> data = new List<byte>() { Capacity =24};
            data.AddRange(BitConverter.GetBytes(vdoc.Origin.X));
            data.AddRange(BitConverter.GetBytes(vdoc.Origin.Y));
            data.AddRange(BitConverter.GetBytes(vdoc.Origin.Z));
            //2. VoxelSize: 8 bytes
            data.AddRange(BitConverter.GetBytes(vdoc.VoxelSize));
            //3. voxels, per including  Col(4 bytes) Row (4 bytes) bottom Elevation (4 bytes) top elevation (4 bytes),
            Dictionary<Voxel, int> dicVox_Index = new Dictionary<Voxel, int>();
            foreach(var elem in vdoc.Elements)
            {
                foreach (var v in elem.Voxels)
                {
                    dicVox_Index.Add(v,dicVox_Index.Count);
                }
            }
            data.AddRange(BitConverter.GetBytes(dicVox_Index.Keys.Count));
            foreach (var vox_idx in dicVox_Index)
            {
                var vox = vox_idx.Key;
                List<byte> voxBytes = new List<byte>() { Capacity =40};
                voxBytes.AddRange(BitConverter.GetBytes(vox.ColIndex));
                voxBytes.AddRange(BitConverter.GetBytes(vox.RowIndex));
                voxBytes.AddRange (BitConverter.GetBytes(vox.BottomElevation));
                voxBytes.AddRange(BitConverter.GetBytes(vox.TopElevation));
               
                data.AddRange(voxBytes);
            }
            //4. voxELem， per include：ID，Name,Category,VoxelIndexes,isSupport, isActive，isTransport
            List<byte> docData = new List<byte>();
            docData.AddRange(BitConverter.GetBytes(vdoc.Elements.Count));
            foreach (var ve in vdoc.Elements)
            {
                //elemId
                string strId = ve.ElementId;
                var strByte = Encoding.Default.GetBytes(strId);
                var idLen = (byte)strByte.Length;
                docData.Add (idLen);
                docData.AddRange (strByte);
                //name
                string strNa = ve.Name;
                strByte = Encoding.Default.GetBytes(strNa);
                idLen = (byte)strByte.Length;
                docData.Add(idLen);
                docData.AddRange(strByte);
                //category
                string strCat = ve.Category;
                strByte = Encoding.Default.GetBytes(strCat);
                idLen = (byte)strByte.Length;
                docData.Add(idLen);
                docData.AddRange(strByte);
                
                //add voxIndex
                docData.AddRange(BitConverter.GetBytes(ve.Voxels.Count));
                foreach (var v in ve.Voxels)
                {
                    var index = dicVox_Index[v];
                    docData.AddRange (BitConverter.GetBytes(index));
                }
                docData.AddRange(BitConverter.GetBytes(ve.IsSupportElement));
                docData.AddRange(BitConverter.GetBytes(ve.IsActive));
                docData.AddRange(BitConverter.GetBytes(ve.IsTransportElement));
            }
            data.AddRange(docData);
            
            //save file
            FileStream fs = new FileStream(saveFilePath,FileMode.OpenOrCreate);
            fs.Write(data.ToArray(), 0, data.Count);
            fs.Flush();
            fs.Close();
        }
        
        public static void SaveAsCSV(VoxelDocument vdoc,string saveFilePath)
        {
            StreamWriter sw = new StreamWriter(saveFilePath, false, Encoding.Default);
            try
            {
                //export element id, voxel col,voxel row,voxel bottom elevation, voxel top elevation
                string strHeader = "ElementId,ColumnIndex,RowIndex,BottomElevation(mm),TopElevation,voxelSize(mm)";
                sw.WriteLine(strHeader);
                foreach (var ve in vdoc.Elements)
                {
                    string strId = ve.ElementId;
                    string voxelSize = Math.Round(vdoc.VoxelSize * 304.8, 0).ToString();
                    foreach (var v in ve.Voxels)
                    {
                        string strCol = v.ColIndex.ToString();
                        string strRow = v.RowIndex.ToString();
                        string strBtmElev = Math.Round(v.BottomElevation * 304.8, 0).ToString();
                        string strTopElev = Math.Round(v.TopElevation * 304.8, 0).ToString();
                        sw.WriteLine(string.Format("{0},{1},{2},{3},{4},{5}", strId, strCol, strRow, strBtmElev, strTopElev,voxelSize));
                    }
                }
            }
            
            finally
            {
                sw.Flush();
                sw.Close();
            }
        }
        public static VoxelDocument LoadVoxelDocuments(ICollection<string> filePaths)
        {
            VoxelDocument voxDocMerge = new VoxelDocument();
            foreach (var fileName in filePaths)
            {
                var doc= LoadVoxelDocument(fileName);
                voxDocMerge.Origin = doc.Origin;
                voxDocMerge.VoxelSize = doc.VoxelSize;
                voxDocMerge.Elements.Capacity = voxDocMerge.Elements.Count + doc.Elements.Count;
                voxDocMerge.Elements.AddRange(doc.Elements);
            }
            return voxDocMerge;
        }
        public static VoxelDocument LoadVoxelDocument(string filePath)
        {
            //1. Origin:24bytes;
            VoxelDocument result = new VoxelDocument();
            FileStream fs = new FileStream(filePath, FileMode.Open);
            BinaryReader br = new BinaryReader(fs, Encoding.Default);
            double[] dblVec3 = new double[3];
            for(int i=0;i<=2;i++)
            {
                dblVec3[i]=  BitConverter.ToDouble(br.ReadBytes(8), 0);
            }
            Vec3 origin=new Vec3(dblVec3[0], dblVec3[1],dblVec3[2]);
            result.Origin = origin;
            //vox size
            double dblVoxSize=BitConverter.ToDouble(br.ReadBytes(8), 0);
            result.VoxelSize = dblVoxSize;
            //Read voxels
            //3.voxels, per including  Col(4 bytes) Row(4 bytes) bottom Elevation(4 bytes) top elevation(4 bytes)
            int numVoxels = BitConverter.ToInt32(br.ReadBytes(4), 0);
            List<Voxel> voxels = new List<Voxel>() { Capacity=numVoxels};
            //List<int[]> voxAdjRel = new List<int[]>();
            for(int i=0;i<=numVoxels-1;i++)
            {
                var vox = new Voxel();
                //read col
                vox.ColIndex=BitConverter.ToInt32(br.ReadBytes(4), 0);
                //read row
                vox.RowIndex= BitConverter.ToInt32(br.ReadBytes(4), 0);
                //read btmElev
                vox.BottomElevation = BitConverter.ToDouble(br.ReadBytes(8), 0);
                //read topElev
                vox.TopElevation= BitConverter.ToDouble(br.ReadBytes(8), 0);
                vox.TopAdjVoxels = new Voxel[4];
                
                voxels.Add(vox);
            }
            //4. voxELem， per include：ID，Category, Name,VoxelIndexes,issppuot, isActive,isTransport
            int numElem = BitConverter.ToInt32(br.ReadBytes(4), 0);
            for(int i=0;i<=numElem-1;i++)
            {
                var voxElem = new VoxelElement();
                voxElem.Voxels = new List<Voxel>();
                result.Elements.Add(voxElem);
                //Id
                byte strLen = br.ReadByte();
                var stringByte=new byte[strLen];
                for(int j=0;j<strLen;j++)
                {
                    stringByte[j] = br.ReadByte();
                }
                var elemId = Encoding.Default.GetString(stringByte);
                voxElem.ElementId = elemId;
                //Name
                strLen = br.ReadByte();
                stringByte = new byte[strLen];
                for (int j = 0; j < strLen; j++)
                {
                    stringByte[j] = br.ReadByte();
                }
                var name = Encoding.Default.GetString(stringByte);
                voxElem.Name = name;
                //Category
                strLen = br.ReadByte();
                stringByte = new byte[strLen];
                for (int j = 0; j < strLen; j++)
                {
                    stringByte[j] = br.ReadByte();
                }
                var cat = Encoding.Default.GetString(stringByte);
                voxElem.Category = cat;
                //vox
                int voxCount = BitConverter.ToInt32(br.ReadBytes(4), 0);
                for(int j=0;j<voxCount;j++)
                {
                    int voxIdx=BitConverter.ToInt32(br.ReadBytes(4), 0);
                    voxElem.Voxels.Add(voxels[voxIdx]);
                }
                voxElem.IsSupportElement = br.ReadBoolean();
                voxElem.IsActive = br.ReadBoolean();
                voxElem.IsTransportElement = br.ReadBoolean();
            }
            return result;
        }
    }
    /// <summary>
    /// This class is used for temporarily store voxels
    /// </summary>
    public class TempFileSaver
    {
        private List<byte> buffer;
        private FileStream fs;
        private int bufferSize;
        private string tempPath; //file recording element raw data
       
        public bool DeleteAfterRead { get; set; } = true;
        public int ElementCount { get; set; } = 0;
        //this is used to store the box areas of each element
        public List<double> MeshBoxAreas { get; set; } = new List<double>();
        public VoxelDocument VoxDoc { get; private set; }
        
        public string GetPath()
        {
            return this.tempPath;
        }
        
        public void SetPath(string value,bool deleteAfterRead)
        {
            this.tempPath = value;
            this.DeleteAfterRead = deleteAfterRead;
        }
        public MeshElement MeshElement { get;  set; }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="doc">Voxel documnet, which assigned the voxel origin and voxel size</param>
        /// <param name="bufferSize">the buffer size in bytes used for temporarily store the voxel element
        /// the data will be transfered to temp files after when the buffer is full, meanwhile the buffer is cleared for
        /// new data to add</param>
        public TempFileSaver(VoxelDocument doc,int bufferSize)
        {
            this.VoxDoc = doc;
            string elementRawFile= Path.GetTempFileName();
            tempPath = elementRawFile;
            this. bufferSize = bufferSize;
            fs=new FileStream (elementRawFile, FileMode.Create);
            buffer = new List<byte>() { Capacity = bufferSize };
            //file format:
            //1. Origin:24bytes;
            buffer.AddRange(BitConverter.GetBytes(doc.Origin.X));
            buffer.AddRange(BitConverter.GetBytes(doc.Origin.Y));
            buffer.AddRange(BitConverter.GetBytes(doc.Origin.Z));
            //2. VoxelSize: 8 bytes
            buffer.AddRange(BitConverter.GetBytes(doc.VoxelSize));
        }
        /// <summary>
        /// try add voxel to buffer or temp files depending if the data in the buffer exceeds the
        /// Given buffer size
        /// </summary>
        /// <param name="voxElem">the voxeled elemets to add</param>
        public void WriteVoxelElement(VoxelElement voxElem)
        {
            var elemData = ConvertElement2Bytes(voxElem, out var dataSize);
            buffer.AddRange(elemData);
            BinaryWriter bw = new BinaryWriter(fs);
            if(buffer.Count >bufferSize) //write file
            {
                fs.Position = fs.Length;
                fs.Write(buffer.ToArray(), 0, buffer.Count);
                fs.Flush();
                //clear buffer
                buffer.Clear();
            }
        }
        /// <summary>
        /// Write the remaining data(if any) in the buffer to the temp file
        /// And terminate the writting process
        /// </summary>
        public void Finish()
        {
            if(buffer.Count !=0)
            {
                fs.Write(buffer.ToArray(), 0, buffer.Count);
            }
            fs.Flush();
            fs.Close();
        }
        
        public TempFileSaver(int bufferSize,int elementCount)
        {
            this.buffer = new List<byte>() { Capacity = bufferSize };
            this.bufferSize = bufferSize;
            this.tempPath = Path.GetTempFileName();
            fs = new FileStream(this.tempPath, FileMode.Create);
            this.buffer.AddRange(BitConverter.GetBytes(elementCount));
        }


        public TempFileSaver()
        {

        }
        private void WriteMeshElement(MeshElement elem)
        {
            var elemData = ConvertElement2Bytes(elem, out var size);
            this.buffer.AddRange(elemData);
            if(this.buffer .Count >this.bufferSize)
            {
                fs.Write (this.buffer.ToArray(),0,this.buffer.Count);
                fs.Flush();
                this.buffer.Clear();
                this.buffer.Capacity = this.bufferSize;
            }
            
        }
        /// <summary>
        /// Write mesh element to temp file
        /// </summary>
        /// <param name="elem">the element to write</param>
        /// <param name="calculateElementSurfaceArea">
        /// Whether or not calculate Element Box Area
        /// </param>
        public void WriteMeshElement(MeshElement elem,bool calculateElementSurfaceArea)
        {
            if(calculateElementSurfaceArea)
            {
                //get element box area
                double boxArea = elem.GetTriangleLength();
                this.MeshBoxAreas.Add(boxArea);
            }
            WriteMeshElement(elem);
        }


        /// <summary>
        /// Convert elem as bytes
        /// </summary>
        /// <param name="elem"></param>
        /// <param name="dataSize"></param>
        /// <returns></returns>
        public static byte[] ConvertElement2Bytes(MeshElement elem,out int dataSize)
        {
            List<byte> elemData = new List<byte>();
            var ve = elem;
            //elemId
            //elemId
            string strId = ve.ElementId;
            var strByte = Encoding.Default.GetBytes(strId);
            var idLen = (byte)strByte.Length;
            elemData.Add(idLen);
            elemData.AddRange(strByte);
            //name
            string strNa = ve.Name;
            strByte = Encoding.Default.GetBytes(strNa);
            idLen = (byte)strByte.Length;
            elemData.Add(idLen);
            elemData.AddRange(strByte);
            //category
            string strCat = ve.Category;
            strByte = Encoding.Default.GetBytes(strCat);
            idLen = (byte)strByte.Length;
            elemData.Add(idLen);
            elemData.AddRange(strByte);
            //add solid data
            elemData.AddRange(BitConverter.GetBytes(ve.Solids.Count));
            foreach (var sld in ve.Solids)
            {
                //get vertices Count
                elemData.AddRange(BitConverter.GetBytes(sld.Vertices.Count));
                //get vertices
                foreach (var v in sld.Vertices)
                {
                    elemData.AddRange(BitConverter.GetBytes(v.X));
                    elemData.AddRange(BitConverter.GetBytes(v.Y));
                    elemData.AddRange(BitConverter.GetBytes(v.Z));
                }
                //get triangles
                elemData.AddRange(BitConverter.GetBytes(sld.Triangles.Count));
                foreach (var tri in sld.Triangles)
                {
                    for(int i=0;i<=2;i++)
                    {
                        elemData.AddRange(BitConverter.GetBytes(tri.VerticesIndex[i]));
                    }
                }
            }
            //get function
            elemData.AddRange(BitConverter.GetBytes(ve.IsSupportElem));
            elemData.AddRange(BitConverter.GetBytes(ve.IsActive));
            elemData.AddRange(BitConverter.GetBytes(ve.isTransport));
            elemData.TrimExcess();
            dataSize = elemData.Count;
            return elemData.ToArray(); ;
        }
        private  byte[] ConvertElement2Bytes(VoxelElement elem, out int dataSize)
        {
            List<byte> elemData = new List<byte>();
            var ve = elem;
            //elemId
            string strId = ve.ElementId;
            var strByte = Encoding.Default.GetBytes(strId);
            var idLen = (byte)strByte.Length;
            elemData.Add(idLen);
            elemData.AddRange(strByte);
            //name
            string strNa = ve.Name;
            strByte = Encoding.Default.GetBytes(strNa);
            idLen = (byte)strByte.Length;
            elemData.Add(idLen);
            elemData.AddRange(strByte);
            //category
            string strCat = ve.Category;
            strByte = Encoding.Default.GetBytes(strCat);
            idLen = (byte)strByte.Length;
            elemData.Add(idLen);
            elemData.AddRange(strByte);
            //add voxCount
            elemData.AddRange(BitConverter.GetBytes(ve.Voxels.Count));
            //add voxContent
            foreach (var vox in ve.Voxels)
            {
                List<byte> voxBytes = new List<byte>() { Capacity = 40 };
                voxBytes.AddRange(BitConverter.GetBytes(vox.ColIndex));
                voxBytes.AddRange(BitConverter.GetBytes(vox.RowIndex));
                voxBytes.AddRange(BitConverter.GetBytes(vox.BottomElevation));
                voxBytes.AddRange(BitConverter.GetBytes(vox.TopElevation));
                elemData.AddRange(voxBytes);
            }
            elemData.AddRange(BitConverter.GetBytes(ve.IsSupportElement));
            elemData.AddRange(BitConverter.GetBytes(ve.IsActive));
            elemData.AddRange(BitConverter.GetBytes(ve.IsTransportElement));
            elemData.TrimExcess();
            //insert elemDataLength
            dataSize = elemData.Count;
            return elemData.ToArray();
        }

        public  VoxelDocument ReadVoxelsFromTempFiles()
        {
            //1. Origin:24bytes;
            VoxelDocument result = new VoxelDocument();
            this.fs= new FileStream(tempPath, FileMode.Open);
            BinaryReader br = new BinaryReader(fs, Encoding.Default);
            try
            {
                long dataSizeRead = 0;
                double[] dblVec3 = new double[3];
                for (int i = 0; i <= 2; i++)
                {
                    dblVec3[i] = BitConverter.ToDouble(br.ReadBytes(8), 0);
                    dataSizeRead += 8;
                }
                Vec3 origin = new Vec3(dblVec3[0], dblVec3[1], dblVec3[2]);
                result.Origin = origin;
                //read voxel size
                result.VoxelSize = br.ReadDouble();
                dataSizeRead += 8;
                //read elements
                while(dataSizeRead<fs.Length)
                {
                    var voxElem = new VoxelElement();
                    voxElem.Voxels = new List<Voxel>();
                    result.Elements.Add(voxElem);
                    //Id
                    byte strLen = br.ReadByte();
                    var stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    dataSizeRead += strLen+1;
                    var elemId = Encoding.Default.GetString(stringByte);
                    voxElem.ElementId = elemId;
                    //Name
                    strLen = br.ReadByte();
                    stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    var name = Encoding.Default.GetString(stringByte);
                    voxElem.Name = name;
                    dataSizeRead += strLen + 1;
                    //Category
                    strLen = br.ReadByte();
                    stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    var cat = Encoding.Default.GetString(stringByte);
                    voxElem.Category = cat;
                    dataSizeRead += strLen + 1;
                    //vox
                    int voxCount = BitConverter.ToInt32(br.ReadBytes(4), 0);
                    dataSizeRead += 4;
                    List<Voxel> voxels = new List<Voxel>() { Capacity = voxCount };
                    for (int j = 0; j < voxCount; j++)
                    {
                        //3.voxels, per including  Col(4 bytes) Row(4 bytes) bottom Elevation(4 bytes) top elevation(4 bytes)
                        var vox = new Voxel();
                        //read col
                        vox.ColIndex = BitConverter.ToInt32(br.ReadBytes(4), 0);
                        dataSizeRead += 4;
                        //read row
                        vox.RowIndex = BitConverter.ToInt32(br.ReadBytes(4), 0);
                        dataSizeRead += 4;
                        //read btmElev
                        vox.BottomElevation = BitConverter.ToDouble(br.ReadBytes(8), 0);
                        dataSizeRead += 8;
                        //read topElev
                        vox.TopElevation = BitConverter.ToDouble(br.ReadBytes(8), 0);
                        dataSizeRead += 8;
                        vox.TopAdjVoxels = new Voxel[4];
                        voxels.Add(vox);
                    }
                    voxElem.Voxels = voxels;
                    voxElem.IsSupportElement = br.ReadBoolean();
                    voxElem.IsActive = br.ReadBoolean();
                    voxElem.IsTransportElement = br.ReadBoolean();
                    dataSizeRead += 3;
                }
                return result;
            }
            catch(Exception ex)
            {
                return null;
            }
            finally
            {
                br.Close();
                fs.Dispose();
                File.Delete(tempPath);
            }
        }

        public void SaveAs(string newPath,bool replaceTempFile)
        {

            File.Copy(this.tempPath, newPath, true);
            if (replaceTempFile)
            {
                File.Delete(this.tempPath);
                this.tempPath = newPath;
            }

        }
        /// <summary>
        /// Read mesh element from temp files
        /// </summary>
        /// <returns></returns>
        public IEnumerable<MeshElement> ReadMeshElementFromTempFile()
        {
            foreach (var me in ReadMeshElement(this.tempPath,this.DeleteAfterRead))
            {
                yield return me;
            }
        }
        /// <summary>
        /// Test parallel output
        /// </summary>
        /// <param name="groupNumber"></param>
        /// <returns></returns>
       
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int GetApproximateElementNumber()
        {
            this.fs = new FileStream(this.tempPath, FileMode.Open);
            BinaryReader br = new BinaryReader(fs, Encoding.Default);
            try
            {
                //get element count
                int numElems = br.ReadInt32();
                return numElems;
            }
            finally
            {
                br.Close();
                fs.Close();
            }
        }
        
        public IEnumerable<MeshElement> ReadMeshElement(string filePath,bool deleteAfterRead)
        {
            this.fs = new FileStream(filePath, FileMode.Open);
            BinaryReader br = new BinaryReader(fs, Encoding.Default);
            FileInfo fInfo = new FileInfo(filePath);
            var fileSize = fInfo.Length;
            long byteLoaded = 0;
            try
            {
                //get element count
                int numElems = br.ReadInt32();
                byteLoaded += 4;
                //for (int i = 0; i < numElems; i++)
                while (byteLoaded < fileSize)
                {
                    
                    MeshElement me = new MeshElement();
                    //read element
                    //Id
                    byte strLen = br.ReadByte();
                    var stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    byteLoaded += 1 + stringByte.Length;
                   
                    var elemId = Encoding.Default.GetString(stringByte);
                    me.ElementId = elemId;
                    //Name
                    strLen = br.ReadByte();
                    stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    var name = Encoding.Default.GetString(stringByte);
                    me.Name = name;
                    byteLoaded += 1 + stringByte.Length;
                    //Category
                    strLen = br.ReadByte();
                    stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    var cat = Encoding.Default.GetString(stringByte);
                    me.Category = cat;
                    byteLoaded += 1 + stringByte.Length;
                    //solid
                    int numSlds = br.ReadInt32();
                    byteLoaded += 4;
                    for (int j = 0; j < numSlds; j++)
                    {
                        MeshSolid sld = new MeshSolid(me, new List<Vec3>(), new List<int>());
                        me.Solids.Add(sld);
                        int numVertices = br.ReadInt32();
                        byteLoaded += 4;
                       
                        sld.Vertices.Capacity = numVertices;
                        for (int k = 0; k < numVertices; k++)
                        {
                            double x = br.ReadDouble();
                            double y = br.ReadDouble();
                            double z = br.ReadDouble();
                            byteLoaded += 24;
                           
                            Vec3 v3 = new Vec3(x, y, z);
                            sld.Vertices.Add(v3);
                        }
                        //restore triangle
                        int numTriangles = br.ReadInt32();
                        byteLoaded += 4;
                        
                        sld.Triangles.Capacity = numTriangles;
                        for (int k = 0; k < numTriangles; k++)
                        {
                            MeshTriangle tri = new MeshTriangle(sld, new int[3]);
                            sld.Triangles.Add(tri);
                            for (int t = 0; t <= 2; t++)
                            {
                                tri.VerticesIndex[t] = br.ReadInt32();
                                byteLoaded += 4;
                               
                            }

                        }
                    }
                    me.IsSupportElem = br.ReadBoolean();
                    me.IsActive = br.ReadBoolean();
                    me.isTransport = br.ReadBoolean();
                    byteLoaded += 3;
                    yield return me;
                }
            }
           
            finally
            {
                
                br.Close();
                fs.Close();
                if (deleteAfterRead)
                    Terminate(filePath);
            }

        }
        


        public void Terminate()
        {
            
            if (!string.IsNullOrEmpty (tempPath) && File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        public void Terminate( string filePath)
        {
           
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
        
    public class VoxelDocument
    {
        public Vec3 Origin { get; set; }
        public double VoxelSize { get; set; }
        public List<VoxelElement> Elements { get; set; } = new List<VoxelElement>();
       
        public VoxelDataManager Manager { get; set; }
        public void GenereateIndoorVoxelMaps(double strideHeight)
        {
            
            //generate accessible region

        }
    }
    public class VoxelElement
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public List<Voxel> Voxels { get; set; } = new List<Voxel>();
        public bool IsSupportElement { get; set; } = false;
        public bool IsObstructElement { get; set; } = true;
        public bool IsTransportElement { get; set; } = false;
        public bool IsActive { get; set; } = true;

        public double timeGridPtGen = 0;
        public double timeVoxGen  = 0;

        public double timeVoxMerge  = 0;
        public double timeVoxFill  = 0;

        private List<HazardMaterial> Materials { get; set; }=new List<HazardMaterial>();
        public VoxelElement() { }
        public VoxelElement(VoxelDocument doc, MeshElement meshElement,bool fillVoxels,double minGapHeight)
        {
            List<Voxel> vox = new List<Voxel>();
            var solids = meshElement.Solids;
            var origin = doc.Origin;
            var voxSize = doc.VoxelSize;
            //vox.Capacity = gpNumberAll / 3;
            foreach (var sld in solids)
            {
               vox.AddRange(sld.GenerateVoxelByRawData( origin,voxSize, fillVoxels, minGapHeight));
               //vox.AddRange(sld.GenerateVoxelByRawDataAndRecordTime(origin, voxSize, fillVoxels, minGapHeight,ref timeGridPtGen,ref timeVoxGen,ref timeVoxMerge,ref timeVoxFill));
            }
            vox.TrimExcess();
            this.IsActive = meshElement.IsActive;
            this.IsSupportElement = meshElement.IsSupportElem;
            this.IsTransportElement = meshElement.isTransport;
            this.Voxels = vox;
            this.ElementId = meshElement.ElementId;
            this.Category = meshElement.Category;
            this.Name = meshElement.Name;
            this.IsTransportElement = meshElement.isTransport;
        }

        public VoxelElement(VoxelDocument doc, MeshElement meshElement, bool fillVoxels, double minGapHeight,bool reportTime)
        {
            List<Voxel> vox = new List<Voxel>();
            var solids = meshElement.Solids;
            var origin = doc.Origin;
            var voxSize = doc.VoxelSize;
            //vox.Capacity = gpNumberAll / 3;
            foreach (var sld in solids)
            {
                //vox.AddRange(sld.GenerateVoxelByRawData(origin, voxSize, fillVoxels, minGapHeight));
                vox.AddRange(sld.GenerateVoxelByRawDataAndRecordTime(origin, voxSize, fillVoxels, minGapHeight,ref timeGridPtGen,ref timeVoxGen,ref timeVoxMerge,ref timeVoxFill));
            }
            vox.TrimExcess();
            this.IsActive = meshElement.IsActive;
            this.IsSupportElement = meshElement.IsSupportElem;
            this.IsTransportElement = meshElement.isTransport;
            this.Voxels = vox;
            this.ElementId = meshElement.ElementId;
            this.Category = meshElement.Category;
            this.Name = meshElement.Name;
            this.IsTransportElement = meshElement.isTransport;
        }
        public static void GenerateGridPoint(VoxelDocument doc, MeshElement meshElement,out double timeElapsed)
        {
            var solids = meshElement.Solids;
            var origin = doc.Origin;
            var voxSize = doc.VoxelSize;
            Stopwatch sw=Stopwatch.StartNew();
            foreach (var sld in solids)
            {
                foreach (var tri in sld.Triangles)
                {
                    foreach (var data in tri.ObtainGridPoints(origin,voxSize))
                    {

                    }
                }
            }
            sw.Stop();
            timeElapsed = sw.Elapsed.TotalMilliseconds;
        }
       
        /// <summary>
        /// Load voxels, the origin is at (0,0,0)
        /// </summary>
        /// <param name="voxelSize"></param>
        /// <param name="meshElement"></param>
        /// <param name="fillVoxels"></param>
    }

    public class AABBElement
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public Vec3 Min { get; set; } = null;
        public Vec3 Max { get; set; } = null;
        public bool IsSupportElement { get; set; } = false;
        public bool IsObstructElement { get; set; } = true;
        public bool IsTransportElement { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public AABBElement()
        {

        }
        public AABBElement(MeshElement meshElement)
        {
            this.ElementId = meshElement.ElementId;
            this.Name = meshElement.Name;
            this.Category = meshElement.Category;
            this.IsActive = meshElement.IsActive;
            this.IsSupportElement= meshElement.IsSupportElem;
            this.IsTransportElement = meshElement.isTransport;
            if(meshElement.TryGetAABB(out var min,out var max))
            {
                this.Max = max;
                this.Min = min;
            }
           
        }
    }
    public class CompressedVoxelDocument
    {
        public Dictionary<int,int> VoxelHight { get; set; }
        public List<CellIndex3D> VoxelScale { get; set; }
        public double VoxelSize { get; set; }
        public Vec3 Origin { get; set; }
        public List<CompressedVoxelElement> Elements { get; set; }

        public CompressedVoxelDocument()
        {

        }
        public CompressedVoxelDocument( ref Dictionary <CellIndex3D,int> scales, VoxelDocument voxDoc)
        {
            this.VoxelSize = voxDoc.VoxelSize;
            this.Origin = voxDoc.Origin;
            Dictionary<int, int> voxHeigtMM_VoxelIndex = new Dictionary<int, int>();
            this.Elements = new List<CompressedVoxelElement>();
            foreach (var ve in voxDoc.Elements)
            {
                var rects= LEGOVoxelTool.CompressVoxels(ref scales, ve);
                var compElem = new CompressedVoxelElement(ve, rects);
                Elements.Add(compElem);
            }
            this.VoxelHight = voxHeigtMM_VoxelIndex;
        }
        
    }


    public class CompressedVoxelElement
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public List<VoxelRectangle> VoxelRectangles { get; set; }
        public bool IsSupportElement { get; set; } = false;
        public bool IsObstructElement { get; set; } = true;
        public bool IsTransportElement { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public int VoxelIndex { get; set; } = -1;
        public CompressedVoxelElement()
        {

        }
        public CompressedVoxelElement(VoxelElement elem,List<VoxelRectangle> rect)
        {
            ElementId = elem.ElementId;
            Name = elem.Name;
            Category = elem.Category;
            IsActive = elem.IsActive;
            IsSupportElement = elem.IsSupportElement;
            IsTransportElement=elem.IsTransportElement;
            IsObstructElement = elem.IsObstructElement;
            VoxelRectangles = rect;
        }

        public VoxelElement ToVoxelElement(CompressedVoxelDocument doc,double unitConvertRatio)
        {
            VoxelElement ve = new VoxelElement();
            ve.IsSupportElement = IsSupportElement;
            ve.IsTransportElement = IsTransportElement;
            ve.IsActive = IsActive;
            ve.IsObstructElement = IsObstructElement;
            ve.Category = Category;
            ve.ElementId = ElementId;
            ve.Voxels = new List<Voxel>();
            foreach (var rect in this.VoxelRectangles)
            {
                ve.Voxels.AddRange ( rect.ToVoxels(doc,unitConvertRatio));
            }
            return ve;
        }

        internal void Get_BoundingBox(CompressedVoxelDocument vdoc,  out CellIndex min, out CellIndex max, out double bottomElev, out double topElev)
        {
            int colMax = int.MinValue;
            int rowMax = int.MinValue;
            int colMin = int.MaxValue;
            int rowMin = int.MaxValue;
            int zMin = int.MaxValue;
            int zMax = int.MinValue;
            foreach (var rect in this.VoxelRectangles)
            {
               var cix3D= rect.Get_Scale(vdoc.VoxelScale);
                var colMaxTemp =rect.Start.Col+ cix3D.Col;
                var rowMaxTemp =rect.Start .Row + cix3D.Row;
                var zMaxTemp =rect.BottomElevation+ cix3D.Layer;

                var colMinTemp = rect.Start.Col;
                var rowMinTemp = rect.Start.Row;
                var zMinTemp = rect.BottomElevation;
                colMax = Math.Max(colMaxTemp, colMax);
                rowMax = Math.Max(rowMaxTemp, rowMax);
                colMin = Math.Min(colMinTemp, colMin);
                rowMin = Math.Min(rowMinTemp, rowMin);
                zMin = Math.Min(zMinTemp, zMin);
                zMax = Math.Max(zMaxTemp, zMax);
            }
            min = new CellIndex(colMin, rowMin);
            max = new CellIndex(colMax, rowMax);
            bottomElev = zMin / 304.8;
            topElev = zMax / 304.8;
            //throw new NotImplementedException();
        }
    }
    public class Voxel
    {
        public MeshElement Host { get; set; }
        public int ColIndex { get; set; }
        public int RowIndex { get; set; }
        public double BottomElevation { get; set; }
        public double TopElevation { get; set; } 
        public Voxel TopVoxel { get; set; }
        public Voxel BottomVoxel { get; set; }
        public Voxel[] BottomAdjVoxels { get; set; }
        public Voxel[] TopAdjVoxels { get; set; }
        // This property is valid for odd voxels
        public VoxelType VoxType { get; set; } = VoxelType.Common;
        public List<Voxel> LinkedSupportVoxels { get; set; }=new List<Voxel>();
        public List<Voxel> LinkedTransportVoxels { get; set; } = new List<Voxel>();
        public Voxel Parent { get; set; }
        public bool IsBoundaryVoxel { get; set; } = false;
        public bool IsSupportVoxel { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public bool Navigable { get; set; } = true;


        
        //this property is used for debug
        //remove after release
        public int Index { get; set; }

        public int BottomActivater { get; set; } = -1;
        public int TopActivater { get; set; } = -1;
        public int BoundaryActivater { get; set; } = -1;
        public Voxel(MeshElement host, int col, int row, double btmElev,double topElev)
        {
            Host = host;
            ColIndex = col;
            RowIndex = row;
            BottomElevation = btmElev;
            TopElevation = topElev;
            BottomVoxel = null;
            TopVoxel = null;
            BottomAdjVoxels=new Voxel[4];
            TopAdjVoxels = new Voxel[4];
            Parent = this;
        }
        public Voxel()
        {
            BottomAdjVoxels = new Voxel[4];
            TopAdjVoxels = new Voxel[4];
        }
        public bool BottomOutside { get; set; } = false;
        public bool TopOutside { get; set; } = false;
        public AccessibleRegion OwnerRegion { get; internal set; }
        /// <summary>
        /// Get Lower Gap range
        /// </summary>
        /// <returns>Voxel Gap</returns>
        public VoxelRange GetLowerGapRange()
        {
            //obtain vox Bottom Gap
            double dblBottomGapSt = double.MinValue;
            double dblBottomGapEd = this.BottomElevation;
            if(BottomVoxel!=null)
            {
                dblBottomGapSt = BottomVoxel.TopElevation;
            }
            return new VoxelRange(dblBottomGapSt, dblBottomGapEd);
            
        }
        /// <summary>
        /// Get lower gap consider minimum passing height
        /// </summary>
        /// <returns></returns>
        public VoxelRange GetLowerGapRange(double minPassingHeight)
        {
            //obtain vox Bottom Gap
            double dblBottomGapSt = double.MinValue;
            double dblBottomGapEd = this.BottomElevation-minPassingHeight/2;
            if (BottomVoxel != null)
            {
                dblBottomGapSt = BottomVoxel.TopElevation;
            }
            return new VoxelRange(dblBottomGapSt, dblBottomGapEd);

        }
        /// <summary>
        /// Get the upper gap above the voxel
        /// </summary>
        /// <returns></returns>
        public VoxelRange GetUpperGapRange()
        {
            //obtan vox TopGap
            double dblTopGapSt = TopElevation;
            double dblTopGapEd = double.MaxValue;
            if (TopVoxel != null)
            {
                dblTopGapEd = TopVoxel.BottomElevation;
            }
            return new VoxelRange(dblTopGapSt, dblTopGapEd);
        }

        /// <summary>
        /// Get the upper gap above a voxel considering the min passing height
        /// </summary>
        /// <returns></returns>
        public VoxelRange GetUpperGapRange(double minPassingHeight)
        {
            //obtan vox TopGap
            double dblTopGapSt = TopElevation+minPassingHeight/2;
            double dblTopGapEd = double.MaxValue;
            if (TopVoxel != null)
            {
                dblTopGapEd = TopVoxel.BottomElevation;
            }
            return new VoxelRange(dblTopGapSt, dblTopGapEd);
        }
        //Used for comparing F

       
    }

    
   
    public class VoxelRectangle
    {
        /// <summary>
        /// index used for generating accessible rectangles
        /// </summary>
        public int Index { get; set; }
        /// <summary>
        /// bottom elevation in millimeter
        /// </summary>
        public int BottomElevation { get; set; }
        public CellIndex Start { get; set; }
        /// <summary>
        /// first: col scale; second:Rogetw scale，third：height
        /// </summary>
        public int ScaleIndex { get; set; }
        public CellIndex3D  Get_Scale(List<CellIndex3D> scaleList)
        {
            return scaleList[this.ScaleIndex];
        }
        public VoxelRectangle()
        {
           
            this.Start = new CellIndex(0,0);
            
        }
       
        /// <summary>
        /// Construct a rectangle
        /// </summary>
        /// <param name="voxelIndex">voxel height index</param>
        /// <param name="bottomElev_MM">rectange bottom elevation</param>
        /// <param name="start">start voxel index</param>
        /// <param name="scale">scale,col-row-heightIndex</param>
        public VoxelRectangle (int scaleIndex,int bottomElev_MM,CellIndex start,CellIndex scale)
        {
            this.BottomElevation = bottomElev_MM;
            this.Start = start;
            this.ScaleIndex = scaleIndex;
        }

        public IEnumerable<CellIndex> Get_CellIndexes(CellIndex3D scale)
        {
            
            int colScale = scale.Col;
            int rowScale=scale.Row; 
            for(int col=0;col<=colScale;col++)
            {
                for(int row=0;row<=rowScale; row++)
                {
                    yield return (Start + new CellIndex(col, row));
                }
            }
        }
        /// <summary>
        /// convert rectangle to voxels
        /// </summary>
        /// <param name="doc"> compressed doc</param>
        /// <param name="unitConversionRatio">the ratio from mm to the internal unit of the software</param>
        /// <returns>voxels</returns>
        public IEnumerable<Voxel> ToVoxels(CompressedVoxelDocument doc, double unitConversionRatio)
        {
            var scale = doc.VoxelScale[this.ScaleIndex];
            var heightMM = scale.Layer;
            double dblActualHeight = heightMM * unitConversionRatio;
            double dblActualBottomElev = this.BottomElevation * unitConversionRatio;
            double dblActualTopElev = this.BottomElevation * unitConversionRatio + dblActualHeight;
            foreach (var cix in this.Get_CellIndexes(scale))
            {
                Voxel voxNew = new Voxel() { BottomElevation = dblActualBottomElev, TopElevation = dblActualTopElev, ColIndex=cix.Col,RowIndex=cix.Row };
                yield return voxNew;  
            }
        }

        internal void Get_BoundingBox(CompressedVoxelDocument compVoxelDoc, out CellIndex min, out CellIndex max, out double bottomElev, out double topElev)
        {
            var rect = this;
            int colMax = int.MinValue;
            int rowMax = int.MinValue;
            int colMin = int.MaxValue;
            int rowMin = int.MaxValue;
            int zMin = int.MaxValue;
            int zMax = int.MinValue;
            var cix3D = rect.Get_Scale(compVoxelDoc.VoxelScale);
            var colMaxTemp = rect.Start.Col + cix3D.Col;
            var rowMaxTemp = rect.Start.Row + cix3D.Row;
            var zMaxTemp = rect.BottomElevation + cix3D.Layer;

            var colMinTemp = rect.Start.Col;
            var rowMinTemp = rect.Start.Row;
            var zMinTemp = rect.BottomElevation;
            colMax = Math.Max(colMaxTemp, colMax);
            rowMax = Math.Max(rowMaxTemp, rowMax);
            colMin = Math.Min(colMinTemp, colMin);
            rowMin = Math.Min(rowMinTemp, rowMin);
            zMin = Math.Min(zMinTemp, zMin);
            zMax = Math.Max(zMaxTemp, zMax);
            min = new CellIndex(colMin, rowMin);
            max = new CellIndex(colMax, rowMax);
            bottomElev = zMin / 304.8;
            topElev = zMax / 304.8;
        }
    }
    public enum VoxelType
    {
        Common=0,
        Odd=1,

    }
    
    /// <summary>
    /// Voxel Range representing the void space between 2 verticl adjacent voxels
    /// </summary>
    public class VoxelRange
    {
        public double StartElevation { get; set; }
        public double EndElevation { get; set; }
        public double GetElevationRange()
        {
            return EndElevation - StartElevation;
        }
        public VoxelRange(double startElev,double endElev)
        {
            StartElevation = startElev;
            EndElevation = endElev;
        }
        public bool Intersect(VoxelRange other,out VoxelRange intersectionResult)
        {
            var endElev = Math.Min(this.EndElevation, other.EndElevation);
            var startElev = Math.Max(this.StartElevation, other.StartElevation);
            if(Math.Round (endElev -startElev,4)>0)
            {
                intersectionResult = new VoxelRange(startElev, endElev);
                return true;
            }
            else
            {
                intersectionResult = null;
                return false;
            }
            
        }
        public bool Intersect(VoxelRange other)
        {
            var stThis = this.StartElevation;
            var edThis = this.EndElevation;
            var stOther = other.StartElevation;
            var edOther = other.EndElevation;
            var dis0 = edThis - stOther;
            var dis1 = edOther - stThis;
            if(Math.Round (dis0,4)>0 && Math.Round (dis1,4)>0)
            {
                return true;
            }
            else
            {
                return false;
            } 
                
        }
    }
    public class VoxelRangeData
    {
        public double StartElevation { get; set; }
        public double EndElevation { get; set; }
        public int Index { get; set; }
        public int ColIndex { get; set; }
        public int RowIndex { get; set; }
        public bool IsOutside { get; set; }
        public double GetElevationRange()
        {
            return EndElevation - StartElevation;
        }
        public VoxelRangeData()
        {

        }
        public VoxelRangeData(double startElev,double endElev)
        {
            StartElevation = startElev;
            EndElevation = endElev;
            IsOutside = false;
            Index = -1;
        }
        public bool Intersect(VoxelRangeData other, out VoxelRangeData intersectionResult)
        {
            var endElev = Math.Min(this.EndElevation, other.EndElevation);
            var startElev = Math.Max(this.StartElevation, other.StartElevation);
            if (Math.Round(endElev - startElev, 4) > 0)
            {
                intersectionResult = new VoxelRangeData(startElev, endElev);
                return true;
            }
            else
            {
                intersectionResult = null;
                return false;
            }

        }
        public bool Intersect(VoxelRangeData other)
        {
            var stThis = this.StartElevation;
            var edThis = this.EndElevation;
            var stOther = other.StartElevation;
            var edOther = other.EndElevation;
            var dis0 = edThis - stOther;
            var dis1 = edOther - stThis;
            if (Math.Round(dis0, 4) > 0 && Math.Round(dis1, 4) > 0)
            {
                return true;
            }
            else
            {
                return false;
            }

        }
    }


    public class GridPoint
    {
        public int Column { get; set; }
        public int Row { get; set; }
        public double Z { get; set; }

        public Vec2 XY { get; set; }

        public GridPointType GridType { get; set; }
        public GridPoint(double x,double y,int column, int row, double z, GridPointType gridType)
        {
            Column = column;
            Row = row;
            Z = z;
            GridType = gridType;
            this.XY =new Vec2(x,y);
        }
        public GridPoint(Vec3 pt,Vec3 origin,double voxSize)
        {
            this.XY = new Vec2(pt.X, pt.Y);
            var dblCol = Math.Round((pt.X - origin.X) / voxSize, 4);
            var dblRow = Math.Round((pt.Y - origin.Y) / voxSize, 4);
            this.Column =(int) Math.Floor(dblCol);
            this.Row = (int)Math.Floor(dblRow);
            this.Z = pt.Z;
            if(dblCol !=this.Column && dblRow !=this.Row)
            {
                this.GridType = GridPointType.VGP;
            }
            else if(dblCol ==this.Column && dblRow !=this.Row)
            {
                this.GridType = GridPointType.CGP;
            }
            else if(dblCol !=this.Column && dblRow ==this.Row)
            {
                this.GridType = GridPointType.RGP;
            }
            else
            {
                this.GridType = GridPointType.IGP;
            }
        }
        
        

        public Vec3 GetCoordinates(Vec3 origin,double voxelSize)
        {
            if(this.XY !=null)
            {
                return new Vec3(this.XY.U, this.XY.V, this.Z);
            }
            else
            {
                var x = origin.X + this.Column * voxelSize;
                var y = origin.Y + this.Row * voxelSize;
                var z = this.Z;
                return new Vec3(x, y, z); 
            }
        }
    }

   

    public enum GridPointType
    {
        VGP=0,
        CGP=1,
        RGP=2,
        IGP=3,
    }

    public class VoxelDataManager
    {
        public Vec3 Origin { get; set; }
        public double VoxelSize { get; set; }
        public Dictionary<Vec3, int> VerticesIndex { get; set; } = new Dictionary<Vec3, int>();
        public Dictionary<CellIndex3D, int> TriIndex { get; set; } = new Dictionary<CellIndex3D, int>();
        public Dictionary<OffsetH, int> HorizontalOffset { get; set; } = new Dictionary<OffsetH, int>();
        public Dictionary<string,TriangleVoxelTemplate> TriangleVoxelTemplates { get; set; }= new Dictionary<string,TriangleVoxelTemplate>();
        public VoxelDataManager(Vec3 origin,double voxelSize)
        {
            this.Origin = origin;
            this.VoxelSize = voxelSize;
        }
        
        public bool TryGetAndUpdateTemplate(MeshTriangle tri,out TriangleVoxelTemplate template ,out string templateId)
        {
            Vec3[] ptLoc=new Vec3[3];
            var voxInv = 1 / VoxelSize;
            //get tri box
            double zMin = double.MaxValue;
            double xMin = double.MaxValue;
            double yMin = double.MaxValue;
            for (int i = 0; i <= 2; i++)
            {
                var pt = tri.Get_Vertex(i);
                ptLoc[i] = pt;
                xMin = Math.Min(pt.X, xMin);
                yMin =Math.Min(pt.Y ,yMin);
                zMin = Math.Min(pt.Z, zMin);
            }
            Vec3 offset = new Vec3(xMin, yMin, zMin);
            //convert to local
            for(int i=0;i<=2;i++)
            {
                ptLoc[i]-=offset;
            }
            var pt0 = ptLoc[0];
            var pt1 = ptLoc[1];
            var pt2 = ptLoc[2];
            //Get offset
            double dblColOff = Math.Round((xMin - Origin.X) * voxInv, 4);
            double dblRowOff = Math.Round((yMin - Origin.Y) * voxInv, 4);
            OffsetH ofh = new OffsetH(dblColOff, dblRowOff);
            if (VerticesIndex.ContainsKey(pt0) && VerticesIndex.ContainsKey (pt1) && VerticesIndex.ContainsKey(pt2))
            {
                int pi0Loc = VerticesIndex[pt0];
                int pi1Loc=VerticesIndex[pt1];
                int pi2Loc = VerticesIndex[pt2];
                int[] intPis= new int[] {pi0Loc ,pi1Loc,pi2Loc};
                Array.Sort(intPis);
                var triCode = new CellIndex3D(intPis[0], intPis[1], intPis[2]);
                if (TriIndex .ContainsKey(triCode))
                {
                    var triIdx=TriIndex [triCode];
                    if(HorizontalOffset.ContainsKey (ofh))
                    {
                        int offIdx=HorizontalOffset [ofh];
                        string striTri_OffIdx=string.Join(",", triIdx, offIdx);
                        if(TriangleVoxelTemplates.ContainsKey (striTri_OffIdx))
                        {
                            template = TriangleVoxelTemplates[striTri_OffIdx];
                            templateId = striTri_OffIdx;
                            return true;  
                        }
                        else
                        {
                            template = null;
                            templateId = striTri_OffIdx;
                            return false;
                        }
                    }
                    else
                    {
                        int offIdx= UpdateOffset(ofh);
                        templateId= string.Join(",",triIdx, offIdx);
                        template = null;
                        return false;
                    }
                }
                else
                {
                    int triIdx= UpdateTriangle(intPis);
                    int offIdx= UpdateOffset(ofh);
                    templateId = string.Join(",", triIdx, offIdx);
                    template = null;
                    return false;
                }
            }
            else
            {
                var newIndex= UpdateTriVerices(ptLoc);
                int triIdx=  UpdateTriangle(newIndex);
                int offIdx= UpdateOffset(ofh);
                templateId = string.Join(",", triIdx, offIdx);
                template = null;
                return false;
            }
        }
        
        public void UpdateTemplate(string tempId,TriangleVoxelTemplate template)
        {
            this.TriangleVoxelTemplates[tempId] = template;
        }
        private int[] UpdateTriVerices(Vec3[] triVerticesLocal)
        {
            int[] triIndex = new int[3];
            //update vertices
            for(int i=0;i<=2;i++)
            {
                var pt=triVerticesLocal[i];
                if (!VerticesIndex.ContainsKey(pt))
                {
                    triIndex[i] = this.VerticesIndex.Count;
                    VerticesIndex.Add(pt, VerticesIndex.Count);
                }
                else
                {
                    triIndex[i] = VerticesIndex[pt];
                }
            }
            //update triangle List
            Array.Sort(triIndex);
            
            return triIndex;
        }

        private int UpdateTriangle(int[] sortedTriLocalIndex)
        {
            var triIdx = new CellIndex3D(sortedTriLocalIndex[0], sortedTriLocalIndex[1], sortedTriLocalIndex[2]);
            int result = this.TriIndex.Count;
            if (!this.TriIndex.ContainsKey(triIdx))
            {
                this.TriIndex.Add(triIdx, this.TriIndex.Count);
            }
            else
            {
                result = this.TriIndex[triIdx];
            }
            return result;
        }
        
        private int UpdateOffset(OffsetH offData)
        {
            int result = 0;
            if(!this.HorizontalOffset.ContainsKey (offData))
            {
                result = this.HorizontalOffset.Count;
                this.HorizontalOffset.Add(offData, this.HorizontalOffset.Count);
            }
            else
            {
                result = this.HorizontalOffset[offData];
            }
            return result;
        }
    }
    public class OffsetH:IEquatable<OffsetH> 
    {
        public OffsetH(double dblColOff, double dblRowOff)
        {
            this.Xoffset = dblColOff;
            this.Yoffset = dblRowOff;
        }

        public double Xoffset { get; set; } = 0;
        public double Yoffset { get; set; } = 0;

        public bool Equals(OffsetH other)
        {
            return this.ToString() == other.ToString();
            //throw new NotImplementedException();
        }
        public override string ToString()
        {
            return $"{Math.Round(Xoffset, 4)},{Math.Round(Yoffset, 4)}";
            //return base.ToString();
        }
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

    }

    public class TriangleVoxelTemplate
    {
        public List<VoxelRawData> Voxels { get; set; }
        public int ColBase { get; set; }
        public int RowBas { get; set; }
        public double ElevationBase { get; set; }
        public TriangleVoxelTemplate(List<VoxelRawData> voxels, int colBase, int rowBas, double elevationBase)
        {
            Voxels = voxels;
            ColBase = colBase;
            RowBas = rowBas;
            ElevationBase = elevationBase;
        }
    }
    #endregion
    #region Nav Class
    public static class PathPlanningTool
    {
        public static AccessibeDocument LoadAccessibleRegionDocument(string filePath)
        {
            //1. Origin:24bytes;
            AccessibeDocument result = new AccessibeDocument();
            FileStream fs = new FileStream(filePath, FileMode.Open);
            BinaryReader br = new BinaryReader(fs, Encoding.Default);
            double[] dblVec3 = new double[3];
            for (int i = 0; i <= 2; i++)
            {
                dblVec3[i] = BitConverter.ToDouble(br.ReadBytes(8), 0);
            }
            Vec3 origin = new Vec3(dblVec3[0], dblVec3[1], dblVec3[2]);
            result.Origin = origin;
            //vox size
            double dblVoxSize = BitConverter.ToDouble(br.ReadBytes(8), 0);
            result.VoxelSize = dblVoxSize;
            //Load accessible regions
            int numARs = br.ReadInt32();
            List<AccessibleRegion> regions = new List<AccessibleRegion>() { Capacity = numARs };
            for (int i = 0; i < numARs; i++)
            {
                AccessibleRegion ar = new AccessibleRegion();
                result.Regions.Add(ar);
                //read rectangles
                int numRects = br.ReadInt32();
                ar.Rectangles = new List<AccessibleRectangle>() { Capacity = numRects };
                List<List<int>> lisRect_AdjRel = new List<List<int>>() { Capacity = numRects };
                for (int j = 0; j < numRects; j++)
                {
                    AccessibleRectangle rect = new AccessibleRectangle();
                    ar.AppendRectangle(rect);
                    rect.Index = j;
                    //read min and max
                    CellIndex cixMin = new CellIndex(br.ReadInt32(), br.ReadInt32());
                    CellIndex cixMax = new CellIndex(br.ReadInt32(), br.ReadInt32());
                    rect.Min = cixMin;
                    rect.Max = cixMax;
                    //read elev
                    rect.Elevation = br.ReadDouble();
                    //read adjRel
                    int numNeighbours = br.ReadInt32();
                    List<int> neighbourIndexes = new List<int>() { Capacity = numNeighbours };
                    for (int k = 0; k <= numNeighbours - 1; k++)
                    {
                        neighbourIndexes.Add(br.ReadInt32());
                    }
                    lisRect_AdjRel.Add(neighbourIndexes);
                }
                //restore rect neighbours
                for (int j = 0; j < numRects; j++)
                {
                    var neighbourIds = lisRect_AdjRel[j];
                    var rect = ar.Rectangles[j];
                    rect.AdjacentRectangles = new List<AccessibleRectangle>() { Capacity = neighbourIds.Count };
                    foreach (var neighbourIdx in neighbourIds)
                    {
                        rect.AdjacentRectangles.Add(ar.Rectangles[neighbourIdx]);
                    }
                }
            }
            return result;
        }
        public static bool PathPlanning(Vec3 start,Vec3 target,AccessibeDocument doc,out List<Vec3> path )
        {
            path = new List<Vec3>();
            var startPt = FindAccessibleRectBelow(start, doc, out var rectStart);
            var endPt = FindAccessibleRectBelow(target, doc, out var rectEnd);
            if(rectStart.Owner == rectEnd.Owner)
            {
                if (!doc.GateGenerated)
                {
                    doc.GenerateGates();
                }
                //reset result
                doc.ResetGate();
                //path planning
                //init first gate
                List<AccessibleGate> gateOpen = new List<AccessibleGate>();
                if (rectStart != null && rectEnd != null)
                {
                    foreach (var gate in rectStart.Gates)
                    {
                        gate.G = gate.DistanceTo(startPt, out var gateWP);
                        gate.H = (endPt - gateWP).GetLength();
                        gate.F = gate.G + gate.H;
                        gate.LocationPoints = gateWP;
                        gate.IsOpen = true;
                        gateOpen.Add(gate);
                    }
                }
                //using A* to find gates
                AccessibleGate gateCur = null;
                while (gateOpen.Count > 0)
                {
                    //find the path
                    gateCur = GetMin(ref gateOpen);
                    gateCur.IsClose = true;
                    Vec3 lcPtCur = gateCur.LocationPoints;
                    var rectCur = gateCur.RectanglesFrom;
                    //get the adjacent gate of current gate
                    List<AccessibleGate> gatesAdj = new List<AccessibleGate>();
                    gatesAdj.Add(gateCur.GateTo);
                    foreach (var gate in rectCur.Gates)
                    {
                        if (gate.IsClose == false)
                        {
                            gatesAdj.Add(gate);
                        }
                    }

                    if (rectCur == rectEnd)
                    {
                        break;
                    }
                    foreach (var gateAdj in gatesAdj)
                    {
                        if (!gateAdj.IsClose)
                        {
                            double gAdj = gateCur.G + gateAdj.DistanceTo(lcPtCur, out var gateWP);
                            if (gateAdj.G > gAdj)
                            {
                                gateAdj.G = gAdj;
                                gateAdj.LocationPoints = gateWP;
                                gateAdj.H = (endPt - gateWP).GetLength();
                                gateAdj.F = gateAdj.G + gateAdj.H;
                                gateAdj.Previous = gateCur;
                            }
                            if (!gateAdj.IsOpen)
                            {
                                gateAdj.IsOpen = true;
                                gateOpen.Add(gateAdj);
                            }
                        }
                    }
                }
                //generate path
                path.Add(endPt);
                while (gateCur != null)
                {
                    path.Add(gateCur.LocationPoints);
                    gateCur = gateCur.Previous;
                }
                path.Add(startPt);
                return true;
            }
            else
            {
                return false;
            }
           
        }
        private static AccessibleGate GetMin(ref List<AccessibleGate> gateOpen)
        {
            double dblF = double.MaxValue;
            AccessibleGate result = null;
            int resultIndex = 0;
            int pt = 0;
            foreach (var item in gateOpen)
            {
                if(item.F<dblF)
                {
                    dblF = item.F;
                    result = item;
                    resultIndex = pt;
                }
                pt += 1;
            }
            if(resultIndex!=gateOpen.Count-1) //pt is not the last, move it to gateOpen[pt] and then remove it
            {
                var gateLast = gateOpen[gateOpen.Count -1];
                gateOpen[resultIndex] = gateLast;
            }
            gateOpen.RemoveAt(gateOpen.Count - 1);

            return result;
        }
        private static Vec3 FindAccessibleRectBelow(Vec3 pt, AccessibeDocument doc,out AccessibleRectangle rect)
        {
            Vec3 result = null;
            double dblElev = double.MinValue;
            rect = null;
            foreach (var rng in doc.Regions)
            {
                foreach (var r in rng.Rectangles)
                {
                    double xMin = r.Min.Col * doc.VoxelSize + doc.Origin.X;
                    double yMin=r.Min.Row *doc.VoxelSize + doc.Origin.Y;
                    double xMax = (r.Max.Col + 1) * doc.VoxelSize + doc.Origin.X;
                    double yMax=(r.Max.Row +1) * doc.VoxelSize + doc.Origin.Y;
                    if(pt.Z >=r.Elevation && pt.X >=xMin && pt.X <=xMax && pt.Y >=yMin && pt.Y <=yMax)
                    {
                        if(r.Elevation >dblElev)
                        {
                            dblElev = r.Elevation;
                            result = new Vec3(pt.X, pt.Y, dblElev);
                            rect = r;
                        }
                    }
                }
            }
            return result;
        }
    }

    public class AccessibeDocument
    {
        public Vec3 Origin { get; set; }
        public double VoxelSize { get; set; }
        public List<AccessibleRegion> Regions { get; set; }
        public bool GateGenerated { get; private set; }

        public double GetArea(AreaUnit unit)
        {
            double dblAreaSquareFeet = 0;
            foreach (var rng in this.Regions)
            {
                foreach (var rect in rng.Rectangles)
                {
                    CellIndex cixScale = rect.Max - rect.Min + new CellIndex(1, 1);
                    double width = cixScale.Col * this.VoxelSize;
                    double height = cixScale.Row * this.VoxelSize;
                    double area=width* height;
                    dblAreaSquareFeet += area;
                }
            }
            switch(unit)
            {
                case AreaUnit.SquareMeter:
                    return dblAreaSquareFeet * Math.Pow(0.3048, 2);
                    break;
                default:
                    return dblAreaSquareFeet;
            }
        }
        public enum AreaUnit
        {
            SqureFeet=0,
            SquareMeter=1,
        }
        public AccessibeDocument()
        {
            this.Origin = Vec3.Zero;
            this.VoxelSize = 0;
            this.Regions = new List<AccessibleRegion>();
        }
        /// <summary>
        /// Generate accessible gates for each rectangles.
        /// </summary>
        public void GenerateGates()
        {
            foreach (var rng in this.Regions)
            {
                foreach (var rect in rng.Rectangles)
                {
                    rect.GenerateGates(this.Origin, this.VoxelSize);
                }
            }
            this.GateGenerated = true;
        }
        /// <summary>
        /// Clear generated way points, call it before a new navigation start
        /// </summary>
        public void ResetGate()
        {
            foreach (var rng in this.Regions)
            {
                foreach (var rect in rng.Rectangles)
                {
                    foreach (var gate in rect.Gates)
                    {
                        gate.LocationPoints = null;
                        gate.G = double.MaxValue;
                        gate.H = double.MaxValue;
                        gate.F = double.MaxValue;
                        gate.Previous = null;
                        gate.IsClose = false;
                        gate.IsOpen = false;
                    }
                }
            }
        }
    }
    public class AccessibleRectangle
    {
        public double Elevation { get; set; }
        public CellIndex Min { get; set; }
        public CellIndex Max { get; set; }
        public List<AccessibleRectangle> AdjacentRectangles { get; set; } = new List<AccessibleRectangle>();
        public Voxel[,] Voxels { get; set; }
        public int Index { get; set; }
        public Vec3 LocationPoint { get; set; }
        
        //way pt of path planning
        public double G { get; set; }
        public double H { get; set; }
        public double F { get; set; }

        public bool IsOpen { get; set; } = false;
        public bool IsClosed { get; set; } = false;
        public bool WaypointGenerated { get; set; } = false;
        public AccessibleRectangle Previous { get; set; }
        //adj rects reaching the current rectangle
        public HashSet<int> AdjRectIdxReached { get; set; } = new HashSet<int>();
        public AccessibleRegion Owner { get; internal set; }

        public List<AccessibleGate> Gates { get; set; } = new List<AccessibleGate>();
        public List<AccessibleCell> GenerateCells()
        {
            var min=this.Min;
            var max = this.Max;
            List<AccessibleCell> cells = new List<AccessibleCell>();
            for(int col=min.Col;col<=max.Col;col++)
            {
                AccessibleCell cellLeft = null;
                for(int row=min.Row;row<=max.Row;row++ )
                {
                    var cell=new AccessibleCell() { Index=new CellIndex (col,row),Owner =this};
                    cell.Neighbours = new AccessibleCell[8];
                    if(cellLeft!=null)
                    {

                    }
                }
            }
            return cells;
        }

        public void GenerateGates(Vec3 origin, double voxelSize)
        {
            CellIndex min = this.Min;
            CellIndex max = this.Max+new CellIndex (1,1);
            foreach (var rectNear in this.AdjacentRectangles)
            {
                if(rectNear.Gates.Count !=rectNear.AdjacentRectangles.Count)
                {
                    var minNear = rectNear.Min;
                    var maxNear = rectNear.Max + new CellIndex(1, 1);
                    int colGateSt = Math.Max(minNear.Col, min.Col);
                    int  colGateEd = Math.Min(max.Col, maxNear.Col);
                    int rowGateSt = Math.Max(min.Row, minNear.Row);
                    int rowGateEd = Math.Min(max.Row, maxNear.Row);
                    double elevation = this.Elevation;
                    double elevationNear = rectNear.Elevation;
                    Vec3 gateSt = origin + new Vec3(colGateSt * voxelSize, rowGateSt * voxelSize, elevation);
                    Vec3 gateEd = origin + new Vec3(colGateEd * voxelSize, rowGateEd * voxelSize, elevation);
                    Vec3 gateStNear = origin + new Vec3(colGateSt * voxelSize, rowGateSt * voxelSize, elevationNear);
                    Vec3 gateEdNear = origin + new Vec3(colGateEd * voxelSize, rowGateEd * voxelSize, elevationNear);

                    //create gate for this rectangle
                    AccessibleGate gate = new AccessibleGate() { Min = gateSt, Max = gateEd, Elevation = elevation, RectanglesFrom = this };
                    this.Gates.Add(gate);
                    //create gate for nearby rectangle
                    AccessibleGate gateNearBy = new AccessibleGate() { Min = gateStNear, Max = gateEdNear, Elevation = elevationNear, RectanglesFrom = rectNear };
                    rectNear.Gates.Add(gateNearBy);
                    gate.GateTo = gateNearBy;
                    gateNearBy.GateTo = gate;
                }
            }
        }
    }
    public class AccessibleRegion
    {
        public List<Voxel> Voxels { get; set; } = new List<Voxel>();
        public List<AccessibleRectangle> Rectangles { get; set; } = new List<AccessibleRectangle>();
        public AccessibleRegion(List<Voxel> voxels)
        {
            Voxels = voxels;
            this.Voxels.ForEach(c => c.OwnerRegion = this);
        }
        public AccessibleRegion()
        {

        }
        public static List<AccessibleRegion> GenerateAccessibleRegions(List<AccessibleRectangle> rects)
        {
            List<AccessibleRegion> regions = new List<AccessibleRegion>();
          
            int accessibleRngIdx = 0;
            foreach (var rect in rects)
            {
                if (rect.Owner !=null)
                {
                    continue;
                }
                AccessibleRegion region = new AccessibleRegion();
                regions.Add(region);
                Stack<AccessibleRectangle> stkRects = new Stack<AccessibleRectangle>();
                rect.Owner = region;
                stkRects.Push(rect);
                while (stkRects.Count != 0)
                {
                    var rout = stkRects.Pop();
                    rout.Index = region.Rectangles.Count;
                    region.Rectangles.Add(rout);
                    foreach (var rAdj in rout.AdjacentRectangles)
                    {
                        if (rAdj.Owner == null)
                        {
                            rAdj.Owner = region;
                            stkRects.Push(rAdj);
                            
                        }
                    }

                }
            }
            return regions;
        }
        public AccessibleRegion(List<AccessibleRectangle> rects)
        {
            this.Rectangles = rects;
        }
        public void AppendRectangle(AccessibleRectangle rect)
        {
            this.Rectangles.Add(rect);
            rect.Owner = this;
        }
        public static List<AccessibleRegion> GenerateAccessibleRegions(VoxelDocument doc, double strideHeight, double minPassingHeight)
        {
            List<AccessibleRegion> regions = new List<AccessibleRegion>();
            Dictionary<CellIndex, List<Voxel>> cix_ObsVoxels = new Dictionary<CellIndex, List<Voxel>>();
            Dictionary<CellIndex, List<Voxel>> cix_SupVoxels = new Dictionary<CellIndex, List<Voxel>>();
            Dictionary<CellIndex, List<Voxel>> cix_TransportVoxels = new Dictionary<CellIndex, List<Voxel>>();
            //Init
            foreach (var ve in doc.Elements)
            {
                if (!ve.IsTransportElement)
                {
                    if (ve.IsActive)
                    {
                        foreach (var vox in ve.Voxels)
                        {
                            vox.IsActive = true;
                            var cix = new CellIndex(vox.ColIndex, vox.RowIndex);
                            if (ve.IsSupportElement)
                            {
                                //update ve's adj voxels
                                vox.TopAdjVoxels = new Voxel[8];//Caution: we modify the adjacent as 9
                                                                //0-east
                                                                //1-north
                                                                //2-west
                                                                //3-south
                                                                //4-northeast
                                                                //5-northwest
                                                                //6-southwest
                                                                //7-southeast

                                if (!cix_SupVoxels.ContainsKey(cix))
                                {
                                    cix_SupVoxels.Add(cix, new List<Voxel>());
                                }
                                cix_SupVoxels[cix].Add(vox);
                            }
                            else
                            {
                                if (!cix_ObsVoxels.ContainsKey(cix))
                                {
                                    cix_ObsVoxels.Add(cix, new List<Voxel>());

                                }
                                cix_ObsVoxels[cix].Add(vox);
                            }
                        }
                    }
                }
                else //update transport voxels
                {
                    foreach (var vox in ve.Voxels)
                    {
                        var cix = new CellIndex(vox.ColIndex, vox.RowIndex);
                        vox.TopAdjVoxels = null;
                        //0-linked support voxels,which will be updated later
                        if (vox.BottomVoxel != null)
                            vox.LinkedTransportVoxels.Add(vox.BottomVoxel);
                        if (vox.TopVoxel != null)
                            vox.LinkedTransportVoxels.Add(vox.TopVoxel);

                        if (cix_TransportVoxels.ContainsKey(cix))
                        {
                            cix_TransportVoxels[cix].Add(vox);
                        }
                        else
                        {
                            cix_TransportVoxels.Add(cix, new List<Voxel>() { vox });
                        }
                    }
                }
            }
            //Merge intersected voxel
            var keyColl = cix_ObsVoxels.Keys.ToList();
            foreach (var key in keyColl)
            {
                var voxelOriginal = cix_ObsVoxels[key];
                var mergedVoxels = MergeIntersectingVoxels(voxelOriginal);
                cix_ObsVoxels[key] = mergedVoxels;
            }
            //merge support voxels
            keyColl = cix_SupVoxels.Keys.ToList();
            foreach (var key in keyColl)
            {
                var voxOriginal = cix_SupVoxels[key];
                var mergedVoxels = MergeIntersectingVoxels(voxOriginal);
                cix_SupVoxels[key] = mergedVoxels;
            }
            //merge transport voxels
            keyColl = cix_TransportVoxels.Keys.ToList();
            foreach (var key in keyColl)
            {
                var voxOriginal = cix_TransportVoxels[key];
                var mergetVoxels = MergeIntersectingVoxels(voxOriginal);
                cix_TransportVoxels[key] = mergetVoxels;
            }
            //support voxel Adjacency test(8-adjacent)
            CellIndex[] adjCellIndex = new CellIndex[8] { new CellIndex(1, 0), new CellIndex(0, 1), new CellIndex(-1, 0), new CellIndex(0, -1), new CellIndex(1, 1), new CellIndex(-1, 1), new CellIndex(-1, -1), new CellIndex(1, -1) };
            foreach (var supVoxels in cix_SupVoxels.Values)
            {
                foreach (var voxSup in supVoxels)
                {
                    CellIndex voxIndex = new CellIndex(voxSup.ColIndex, voxSup.RowIndex);
                    //search vox-adj rel
                    for (int j = 0; j <= 7; j++)
                    {
                        var cellIdxNear = voxIndex + adjCellIndex[j];
                        voxSup.TopAdjVoxels[j] = null;
                        if (cix_SupVoxels.ContainsKey(cellIdxNear))
                        {
                            var potentialVoxAdj = cix_SupVoxels[cellIdxNear];
                            foreach (var voxAdj in potentialVoxAdj)
                            {
                                double voxAdjTop = voxAdj.TopElevation;
                                double voxAdjBottom = voxAdj.BottomElevation;
                                if (Math.Abs(voxAdj.TopElevation - voxSup.TopElevation) <= strideHeight)
                                {
                                    voxSup.TopAdjVoxels[j] = voxAdj;
                                }
                                if (voxAdjBottom > voxSup.TopElevation)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    //search potential transport rel
                    if (cix_TransportVoxels.ContainsKey(voxIndex))
                    {
                        var transVoxes = cix_TransportVoxels[voxIndex];
                        //find transport voxels intersecting with the support voxels
                        var transVox = transVoxes.Where(c => voxSup.TopElevation + strideHeight > c.BottomElevation && voxSup.TopElevation < c.TopElevation).FirstOrDefault();
                        if (transVox != null)
                        {
                            //link vox to transVox
                            voxSup.LinkedTransportVoxels.Add(transVox);
                            //link transvox to vox
                            transVox.LinkedSupportVoxels.Add(voxSup);
                        }
                    }
                }
            }
            //transport voxel adjacent text
            foreach (var transVoxels in cix_TransportVoxels.Values)
            {
                foreach (var transVox0 in transVoxels)
                {
                    foreach (var transVox1 in transVoxels)
                    {
                        if (transVox0 != transVox1)
                        {
                            transVox0.LinkedTransportVoxels.Add(transVox1);
                        }
                    }
                }
            }
            //voxel navigability test
            foreach (var supVoxes in cix_SupVoxels.Values)
            {
                //check if any supprot voxels obstruct voxel
                foreach (var vox in supVoxes)
                {
                    var cix = new CellIndex(vox.ColIndex, vox.RowIndex);
                    var voxTop = vox.TopElevation;
                    //check if current voxel is navigable
                    //get voxels
                    var voxAbove = vox.TopVoxel;
                    var scanElev0 = vox.TopElevation + strideHeight;
                    var scanElev1 = vox.TopElevation + minPassingHeight;
                    //check if there are support voxels cover or obstruct current voxels
                    if (voxAbove != null && voxAbove.BottomElevation <= scanElev1)
                    {
                        vox.Navigable = false;
                        //break;
                    }
                    //check if any obstruction voxels obstructs voxel
                    if (cix_ObsVoxels.ContainsKey(cix))
                    {
                        var potentialObsVoxes = cix_ObsVoxels[cix];
                        var obsElev0 = voxTop + strideHeight;
                        var obsElev1 = voxTop + minPassingHeight;
                        foreach (var voxOb in potentialObsVoxes)
                        {
                            var voxObTop = voxOb.TopElevation;
                            var voxObBtm = voxOb.BottomElevation;
                            if (voxObBtm <= obsElev1 && voxObTop >= obsElev0) //Obstruction detected
                            {
                                vox.Navigable = false;
                                break;
                            }
                            else if (voxObBtm > obsElev1) //ob out of scan range
                            {
                                break;
                            }
                        }
                    }
                }
            }
            //generate accessible region
            HashSet<Voxel> voxScanned = new HashSet<Voxel>();
            foreach (var voxes in cix_SupVoxels.Values)
            {
                foreach (var vox in voxes)
                {
                    if (!voxScanned.Contains(vox) && vox.Navigable)
                    {
                        List<Voxel> voxAccessibleRng = new List<Voxel>();
                        Stack<Voxel> vox2Check = new Stack<Voxel>();
                        vox2Check.Push(vox);
                        voxScanned.Add(vox);
                        int arIndex = regions.Count;
                        while (vox2Check.Count != 0)
                        {
                            var voxOut = vox2Check.Pop();
                            voxAccessibleRng.Add(voxOut);
                            //add the adjacent voxels, linked tranport and support voxels(tranport voxels only)
                            var voxesNear = new List<Voxel>();
                            if (voxOut.TopAdjVoxels != null)
                            {
                                voxesNear.AddRange(voxOut.TopAdjVoxels.Where(c => c != null));
                            }
                            voxesNear.AddRange(voxOut.LinkedSupportVoxels);
                            voxesNear.AddRange(voxOut.LinkedTransportVoxels);
                            if (voxesNear.Count != 0)
                            {
                                //support to support navigation
                                foreach (var voxNear in voxesNear)
                                {
                                    if (voxNear != null && voxNear.Navigable && !(voxScanned.Contains(voxNear)))
                                    {
                                        voxScanned.Add(voxNear);
                                        vox2Check.Push(voxNear);
                                    }
                                }
                            }
                            //transport to support navigation
                        }
                        AccessibleRegion ar = new AccessibleRegion(voxAccessibleRng);
                        regions.Add(ar);
                    }
                }
            }
            return regions;
        }

        public List<AccessibleCell> GenerateAccessibleCells()
        {
            return null;
            foreach (var rect in this.Rectangles)
            {
                 
            }
        }
        private static List<Voxel> MergeIntersectingVoxels(List<Voxel> voxWithSameIndex)
        {
            var sortedVoxels = voxWithSameIndex.OrderBy(c => c.BottomElevation).ToList();
            int snakePointer = 0;
            int foodPointer = 1;
            List<Voxel> mergedVoxels = new List<Voxel>();
            var snakeVoxel = sortedVoxels[snakePointer];
            snakeVoxel.Parent = snakeVoxel;
            mergedVoxels.Add(snakeVoxel);
            while (foodPointer < voxWithSameIndex.Count)
            {
                Voxel foodVoxel = sortedVoxels[foodPointer];
                //check if snake voxels intersects food voxels
                if (Math.Round(snakeVoxel.BottomElevation - foodVoxel.TopElevation, 4) <= 0 &&
                    Math.Round(snakeVoxel.TopElevation - foodVoxel.BottomElevation, 4) >= 0)
                {
                    snakeVoxel.TopElevation = Math.Max(snakeVoxel.TopElevation, foodVoxel.TopElevation);
                    
                    foodPointer += 1;
                }
                else //snake voxel and food voxel do not intersect
                {
                    snakePointer = foodPointer;
                    snakeVoxel = sortedVoxels[snakePointer];
                    
                    mergedVoxels.Add(snakeVoxel);
                    foodPointer += 1;
                }
            }
            //link voxels in mergrdVoxels
            for (int i = 0; i < mergedVoxels.Count - 1; i++)
            {
                var vox = mergedVoxels[i];
                var voxAbove = mergedVoxels[i + 1];
                vox.TopVoxel = voxAbove;
                voxAbove.BottomVoxel = vox;
            }
            return mergedVoxels;
        }
       
        

       
        


       

       
        
    }
    public class AccessibleCell
    {
        public AccessibleRectangle Owner { get; set; }
        public CellIndex Index { get; set; }
        public AccessibleCell[] Neighbours { get; set; }
        public double G { get; set; }=double.MaxValue;
        public double H { get; set; } = double.MaxValue;
        public double F { get; set; } = double.MaxValue;
        public AccessibleCell Previous { get; set; }

        public double Get_Elevation()
        {
            return Owner.Elevation;
        }
        
    }

    public class AccessibleGate : IAStarObject
    {
        public Vec3 LocationPoints { get; set; } = null;
        public WayPoint[] WayPoints { get; set; }
        public Vec3 Min { get; set; }
        public Vec3 Max { get; set; }
        public double Elevation { get; set; }
        public double G { get; set; } = double.MaxValue;
        public double H { get; set; } = double.MaxValue;
        public double F { get; set; } = double.MaxValue;
        public bool IsOpen { get; set; } = false;
        public bool IsClose { get; set; } = false;
        public AccessibleRectangle RectanglesFrom { get; set; }

        public AccessibleGate GateTo{ get; set; }
        public AccessibleGate Previous { get; internal set; }
        public double DistanceTo(AccessibleGate other)
        {
            Vec3 pt0 = WayPoints[0].Points;
            Vec3 pt1 = WayPoints[WayPoints.Length - 1].Points;
            Vec3 pta = other.WayPoints[0].Points;
            Vec3 ptb = other.WayPoints[other.WayPoints.Length - 1].Points;
            double distance = double.MaxValue;
            if (pt0.X == pt1.X && pta.X == ptb.X)
            {
                if (pt0.Y < ptb.Y && pt1.Y > pta.Y) //intersection
                {
                    return Math.Abs(pta.X - pt0.X);
                }
                else
                {
                    double dblDistance = double.MaxValue;
                    var ptsBase = new Vec3[2] { pt0, pt1 };
                    var ptOther = new Vec3[2] { pta, ptb };
                    foreach (var p0 in ptsBase)
                    {
                        foreach (var p1 in ptOther)
                        {
                            double dblDisTemp = (p0 - p1).GetLength();
                            if (dblDistance > dblDisTemp)
                                dblDistance = dblDisTemp;
                        }
                    }
                    return dblDistance;
                }
            }
            else if (pt0.Y == pt1.Y && pta.Y == ptb.Y)
            {
                if (pt0.X < ptb.X && pt1.X > pta.X) //intersection
                {
                    return Math.Abs(pta.Y - pt0.Y);
                }
                else
                {
                    double dblDistance = double.MaxValue;
                    var ptsBase = new Vec3[2] { pt0, pt1 };
                    var ptOther = new Vec3[2] { pta, ptb };
                    foreach (var p0 in ptsBase)
                    {
                        foreach (var p1 in ptOther)
                        {
                            double dblDisTemp = (p0 - p1).GetLength();
                            if (dblDistance > dblDisTemp)
                                dblDistance = dblDisTemp;
                        }
                    }
                    return dblDistance;
                }
            }
            else
            {
                double dblDistance = double.MaxValue;
                var ptsBase = new Vec3[2] { pt0, pt1 };
                var ptOther = new Vec3[2] { pta, ptb };
                foreach (var p0 in ptsBase)
                {
                    foreach (var p1 in ptOther)
                    {
                        double dblDisTemp = (p0 - p1).GetLength();
                        if (dblDistance > dblDisTemp)
                            dblDistance = dblDisTemp;
                    }
                }
                return dblDistance;
            }

        }

        public double DistanceTo(Vec3 pt, out Vec3 ptOnGateNear)
        {
            Vec3 pt0 = Min;
            Vec3 pt1 = Max;
            if (pt0.X == pt1.X)//vertical gate
            {
                if (pt.Y > pt0.Y && pt.Y < pt1.Y) //pt falls in between the gate
                {
                    ptOnGateNear = new Vec3(pt0.X, pt.Y, pt0.Z);
                    return Math.Abs(pt.X - pt0.X);
                }
                else// pt is onut of the gate
                {
                    ptOnGateNear = null;
                    var disPt20 = (pt - pt0).GetLength();
                    var disPt21 = (pt - pt1).GetLength();
                    if (disPt20 < disPt21)
                    {
                        ptOnGateNear = pt0;
                        return disPt20;
                    }
                    else
                    {
                        ptOnGateNear = pt1;
                        return disPt21;
                    }
                }
            }
            else if (pt0.Y == pt1.Y) //horizontal gate
            {
                if (pt.X > pt0.X && pt.X < pt1.X)// pt falls in between ptx
                {
                    ptOnGateNear = new Vec3(pt.X, pt0.Y, pt0.Z);
                    return Math.Abs(pt.Y - pt0.Y);
                }
                else //pt is out of the gate
                {
                    ptOnGateNear = null;
                    var disPt20 = (pt - pt0).GetLength();
                    var disPt21 = (pt - pt1).GetLength();
                    if (disPt20 < disPt21)
                    {
                        ptOnGateNear = pt0;
                        return disPt20;
                    }
                    else
                    {
                        ptOnGateNear = pt1;
                        return disPt21;
                    }
                }
            }
            else
            {
                ptOnGateNear = null;
                var disPt20 = (pt - pt0).GetLength();
                var disPt21 = (pt - pt1).GetLength();
                if (disPt20 < disPt21)
                {
                    ptOnGateNear = pt0;
                    return disPt20;
                }
                else
                {
                    ptOnGateNear = pt1;
                    return disPt21;
                }
            }
        }

        public void GenerateWayPoints()
        {
            List<WayPoint> wps = new List<WayPoint>();
            wps.Add(new WayPoint(this.Min));
            if ((this.Max - this.Min).GetSquareLen() > 1e-4) // the max and min are not the same
            {
                wps.Add(new WayPoint(this.Max));

            }
            wps.Add(new WayPoint(this.LocationPoints));
            this.WayPoints = wps.ToArray();
        }
    }
    public class WayPoint : IAStarObject
    {
        public Vec3 Points { get; set; }
        public WayPoint Previous { get; set; }
        public List<AccessibleRectangle> Rectangles { get; set; } = new List<AccessibleRectangle>();

        public double G { get; set; } = double.MaxValue;
        public double H { get; set; } = double.MaxValue;
        public double F { get; set; } = double.MaxValue;
        public bool IsOpen { get; set; } = false;
        public bool IsClose { get; set; } = false;

        public AccessibleGate Gate { get; set; }

        public WayPoint(Vec3 point)
        {
            this.Points = point;
        }
        public WayPoint()
        {

        }
        public List<WayPoint> AdjacentWayPoints { get; set; } = new List<WayPoint>();
    }
    public interface IAStarObject
    {
        double G { get; set; }
        double H { get; set; }
        double F { get; set; }

        bool IsOpen { get; set; }
        bool IsClose { get; set; }
    }
    #endregion
    #region Model Converter
    public class MeshDocumentConverter
    {
       
        
    }
    //save voxel as obj formats
    public class VoxelObjConverter
    {

    }
    #endregion

    #region voxel tool
    public static class LEGOVoxelTool
    {
        
        public static CompressedVoxelDocument CompressVoxelDocuments (ref Dictionary<CellIndex3D,int> scales, VoxelDocument voxDoc)
        {
            CompressedVoxelDocument cvd = new CompressedVoxelDocument(); 
            cvd. VoxelSize = voxDoc.VoxelSize;
            cvd.  Origin = voxDoc.Origin;
            Dictionary<int, int> voxHeigtMM_VoxelIndex = new Dictionary<int, int>();
            cvd.Elements = new List<CompressedVoxelElement>();
            foreach (var ve in voxDoc.Elements)
            {
                var rects = CompressVoxels(ref scales, ve);
                var compElem = new CompressedVoxelElement(ve, rects);
                cvd. Elements.Add(compElem);
            }
            cvd.VoxelHight = voxHeigtMM_VoxelIndex;
            return cvd;
        }
        public static List<VoxelRectangle> CompressVoxels(ref Dictionary<CellIndex3D,int> scales, VoxelElement ve)
        {
            Dictionary<int, Dictionary<CellIndex, int>> dicBtmElev_Cix_Height = new Dictionary<int, Dictionary<CellIndex, int>>();
            //get or update voxelHeightIndex
            
            foreach (var vox in ve.Voxels)
            {
                int dblHeight_MM = (int)Math.Round((vox.TopElevation - vox.BottomElevation)*304.8);
                //update dicBtmElev_Cix_HeightIdx
                var vbtm_MM =(int) Math.Round (vox.BottomElevation*304.8);
                var voxCellIdx = new CellIndex(vox.ColIndex, vox.RowIndex);
                if (!dicBtmElev_Cix_Height.ContainsKey(vbtm_MM))
                {
                    dicBtmElev_Cix_Height.Add(vbtm_MM, new Dictionary<CellIndex, int>()
                    {
                        { voxCellIdx,dblHeight_MM  }
                    });
                }
                else if (!dicBtmElev_Cix_Height[vbtm_MM].ContainsKey (voxCellIdx))
                {
                    dicBtmElev_Cix_Height[vbtm_MM].Add (voxCellIdx, dblHeight_MM);
                }
            }
            //Generate voxelRactangle
            List<VoxelRectangle> voxelRectangle = new List<VoxelRectangle>();
            foreach (var btmElev_cix_Height in dicBtmElev_Cix_Height)
            {
                var btmElev_MM = btmElev_cix_Height.Key;
                var cix_Height_MM = btmElev_cix_Height.Value;
                HashSet<CellIndex> cixChecked = new HashSet<CellIndex>();
                
                foreach (var cix in cix_Height_MM.Keys)
                {
                    if(!cixChecked.Contains (cix))
                    {
                        var vr = GenerateRectangleAndMarkVoxels(cix_Height_MM,cixChecked, ref scales, btmElev_MM, cix, voxelRectangle.Count, out var scale);
                        voxelRectangle.Add(vr);
                        //update cixChecked
                        foreach (var vix in vr.Get_CellIndexes(scale))
                        {
                            cixChecked.Add(vix);
                        }
                    }
                }
            }
            return voxelRectangle;
        }

        private static VoxelRectangle GenerateRectangleAndMarkVoxels(Dictionary<CellIndex, int> voxelOriginal, HashSet<CellIndex> cixChecked,ref Dictionary<CellIndex3D,int> scales, double bottomElev, CellIndex voxStart, int rectIndex, out CellIndex3D scale)
        {
            var voxMax = voxStart;
            var voxMin = voxStart;
            int voxelIndex = voxelOriginal[voxStart];
            CellIndex voxMinRight = voxStart;
            CellIndex voxMaxLeft = voxStart;
            bool minFound = false;
            bool maxFound = false;
            while (!minFound || !maxFound)
            {
                List<CellIndex> voxE = null;
                List<CellIndex> voxS = null;
                List<CellIndex> voxW = null;
                List<CellIndex> voxN = null;
                CellIndex voxNE=default ;
                CellIndex voxSW=default;
                CellIndex voxSE =default;
                CellIndex voxNW = default ;
                if (!maxFound) //voxMax has not been found yet
                {
                    voxNE = GetDirectionVoxel(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, voxMaxLeft, voxMinRight, "NE");
                    voxN = GetDirectionVoxels(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, "N");
                    voxE = GetDirectionVoxels(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, "E");
                }
                if (!minFound) //voxMin has not been found yet
                {
                    voxSW = GetDirectionVoxel(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, voxMaxLeft, voxMinRight, "SW");
                    voxSE = GetDirectionVoxel(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, voxMaxLeft, voxMinRight, "SE");
                    voxS = GetDirectionVoxels(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, "S");
                    voxNW = GetDirectionVoxel(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, voxMaxLeft, voxMinRight, "NW");
                    voxW = GetDirectionVoxels(voxelOriginal,cixChecked, voxelIndex, voxMax, voxMin, "W");
                }
                
                //mark found voxels
                if (voxE != null && voxNE != default && voxN != null) //max=max.NE
                {
                    voxMax = voxNE;
                    if (voxS != null && voxW != null && voxSE != null && voxNW != null && voxSW != null) //min =min.SW
                    {
                        voxMin = voxSW;
                        voxMinRight = voxSE;
                        voxMaxLeft = voxNW;
                    }
                    else if (voxS != null && voxSE != null)
                    {
                        voxMin = voxMin + new CellIndex(0, -1);// voxMin.TopAdjVoxels[3];//S
                        voxMinRight = voxSE;
                        voxMaxLeft = voxN.Last();
                    }
                    else if (voxW != null && voxNW != null)
                    {
                        voxMin += new CellIndex(-1, 0);// voxMin.TopAdjVoxels[2];//W
                        voxMinRight = voxE.Last(); ;
                        voxMaxLeft = voxNW;
                    }
                    else
                    {
                        minFound = true;
                    }
                }
                /*
                 2 max(t+1)=max(t) +(0,1)
                        N 
                        2.1  min(t+1)=nw
                            S,W,SW,NW
                        2.2  min(t+1)=min(t)-(0,1)
                            S
                        2.3  min(t+1)=min(t)-(1,0)
                            W，NW
                        2.4  min(t+1)=min(t)
                            Else	 
                 */
                else if (voxN != null) //voxMax=voxN
                {
                    voxMax += new CellIndex(0, 1);//voxMax.TopAdjVoxels[1];
                   
                    //2.1  min(t+1)=nw
                    //S,W,SW,NW
                    if (voxS != null && voxW != null && voxSW != null && voxNW != null)
                    {
                        voxMin += new CellIndex(-1, -1); // = voxMin.TopAdjVoxels[6];
                        voxMinRight = voxS.Last(); ;
                        voxMaxLeft = voxNW;
                    }
                    //2.2  min(t+1)=min(t)-(0,1)
                    //S
                    else if (voxS != null)
                    {
                        voxMinRight = voxS.Last(); ;
                        voxMaxLeft = voxN.Last();
                        voxMin += new CellIndex(0, -1); //voxMin.TopAdjVoxels[3];
                    }
                    //2.3  min(t+1)=min(t)-(1,0)
                    //W，NW
                    else if (voxW != null && voxNW != null) //voxMin
                    {
                        voxMin += new CellIndex(-1, 0); //= voxMin.TopAdjVoxels[2];
                        voxMaxLeft = voxNW;
                    }
                    //2.4  min(t + 1) = min(t)
                    //Else
                    else
                    {
                        minFound = true;
                    }
                }
                /*
                 3 max(t+1)=max(t) +(1,0)
                    E 
                    3.1  min(t+1)=SW
                        S,W,SW,SE
                    3.2  min(t+1)=min(t)-(0,1)
                        S，SE
                    3.3  min(t+1)=min(t)-(1,0)
                        W
                    3.4  min(t+1)=min(t)
                        Else	
                 */

                else if (voxE != null)
                {
                    voxMax += new CellIndex(1, 0); //= voxMax.TopAdjVoxels[0];
                    if (voxS != null && voxW != null && voxSW != null && voxSE != null)
                    {
                        voxMin += new CellIndex(-1, -1);   //voxMin.TopAdjVoxels[6];
                        voxMinRight = voxSE; ;
                        voxMaxLeft = voxW.Last();
                    }
                    else if (voxS != null && voxSE != null)
                    {
                        voxMin += new CellIndex(0, -1);// = voxMin.TopAdjVoxels[3];
                        voxMinRight = voxSE;
                    }
                    else if (voxW != null)
                    {
                        voxMinRight = voxE.Last();
                        voxMin += new CellIndex(-1, 0); //voxMin.TopAdjVoxels[2];
                    }
                    else
                    {
                        minFound = true;
                    }
                }
                else
                {
                    maxFound = true;
                    if (voxW != null && voxS != null && voxSW != null)
                    {
                        voxMin +=new CellIndex(-1, -1); //= voxMin.TopAdjVoxels[6];
                        voxMinRight = voxS.Last();
                        voxMaxLeft = voxW.Last();
                    }
                    else if (voxS != null)
                    {
                        voxMin += new CellIndex(0, -1);// = voxMin.TopAdjVoxels[3];
                        voxMinRight = voxS.Last();
                    }
                    else if (voxW != null)
                    {
                        voxMin += new CellIndex(-1, 0);//= voxMin.TopAdjVoxels[2];
                        voxMaxLeft = voxW.Last();
                    }
                    else
                    {
                        minFound = true;
                    }
                }
            }
            //collect boundary voxels
            
            //Genearte accessible region
            VoxelRectangle rect = new VoxelRectangle();
            rect.Start = new CellIndex(voxMin.Col, voxMin.Row);
            var scale2D = voxMax - voxMin;
            scale =new CellIndex3D (scale2D.Col ,scale2D.Row ,voxelIndex);
            int scaleIdx = -1;
            if(scales.ContainsKey (scale))
            {
                scaleIdx = scales [scale];
            }
            else
            {
                scaleIdx = scales.Count;
                scales.Add(scale,scaleIdx);
            }
            rect.ScaleIndex = scaleIdx;
            rect.BottomElevation = (int)bottomElev;
            return rect;
        }
        /// <summary>
        /// Get voxels at a direction of a rectangle zone defined by voxMax and voxMin
        /// </summary>
        /// <param name="voxMax">the upper right voxels</param>
        /// <param name="voxMin">the bottom left voxels</param>
        /// <param name="strDir">string,SW,NW,SE,NE</param>
        /// <returns>voxels, null if one or more voxels in current direction is invalid</returns>
        private static CellIndex GetDirectionVoxel(Dictionary<CellIndex,int> voxelOriginal,HashSet<CellIndex> indexChecked, int voxelTypeIndex,  CellIndex voxMax, CellIndex voxMin, CellIndex voxMaxLeft, CellIndex voxMinRight, string strDir)
        {
            CellIndex voxCur = default;
            switch (strDir)
            {
                case "NE":
                    voxCur = voxMax+new CellIndex (1,1);
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur)) 
                        return default;
                    break;
                case "NW":
                    voxCur = voxMaxLeft+new CellIndex (-1,1);
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                        return default;
                    break;
                case "SE":
                    voxCur = voxMinRight+new CellIndex(1,-1);
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                        return default;
                    break;

                case "SW":
                    voxCur = voxMin+new CellIndex(-1, -1); ;
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                        return voxCur;
                    break;
            }
            return voxCur;
        }

        /// <summary>
        /// Get voxels at a direction of a rectangle zone defined by voxMax and voxMin
        /// </summary>
        /// <param name="voxMax">the upper right voxels</param>
        /// <param name="voxMin">the bottom left voxels</param>
        /// <param name="strDir">string,E-ease,W-west,N-north,S-south</param>
        /// <returns>voxels, null if one or more voxels in current direction is invalid</returns>
        private static List<CellIndex> GetDirectionVoxels(Dictionary<CellIndex, int> voxelOriginal, HashSet<CellIndex> indexChecked,   int voxelTypeIndex, CellIndex voxMax, CellIndex voxMin, string strDir)
        {
            int colSt = voxMin.Col;
            int colEd = voxMax.Col;
            int rowSt = voxMin.Row;
            int rowEd = voxMax.Row;
            List<CellIndex> result = new List<CellIndex>();
            switch (strDir)
            {
                case "E":
                    CellIndex voxCur = voxMax+new CellIndex (1,0);
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                        return null;
                    result.Add(voxCur);
                    for (int row = rowEd - 1; row >= rowSt; row--) //search backward
                    {
                        voxCur = voxCur+new CellIndex (0,-1);
                        if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                            return null;
                        result.Add(voxCur);
                    }
                    break;
                case "W":
                    voxCur = voxMin+new CellIndex (-1,0);
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                            return null;
                    
                    result.Add(voxCur);
                    for (int row = rowSt + 1; row <= rowEd; row++)
                    {
                        voxCur = voxCur+new CellIndex (0,1);
                        if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                            return null;
                        result.Add(voxCur);
                    }
                    break;
                case "S":
                    voxCur = voxMin+new CellIndex (0,-1);
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                        return null;
                    result.Add(voxCur);
                    for (int col = colSt + 1; col <= colEd; col++)
                    {
                        voxCur = voxCur+new CellIndex (1,0);
                        if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                            return null;
                        result.Add(voxCur);
                    }
                    break;
                case "N":
                    voxCur = voxMax+new CellIndex (0,1);
                    if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                        return null;
                    result.Add(voxCur);
                    for (int col = colEd - 1; col >= colSt; col--)
                    {
                        voxCur = voxCur+new CellIndex(-1,0);
                        if (!voxelOriginal.ContainsKey(voxCur) || voxelOriginal[voxCur] != voxelTypeIndex || indexChecked.Contains(voxCur))
                            return null;
                        result.Add(voxCur);
                    }
                    break;
            }
            return result;
        }
        /// <summary>
        /// Save compressed voxels
        /// </summary>
        /// <param name="compVoxDoc">the compressed voxels</param>
        /// <param name="filePath">file path</param>
        public static void SaveCompressedVoxelDocument(CompressedVoxelDocument compVoxDoc,  string filePath)
        {
            FileStream fs = new FileStream(filePath,FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fs);
            try
            {
                //write voxel info
                bw.Write(Vec32Byte(compVoxDoc.Origin));//origin
                bw.Write(BitConverter.GetBytes(compVoxDoc.VoxelSize));//voxel size;
                bw.Write(BitConverter.GetBytes(compVoxDoc.Elements.Count));//element count                                                   //write elem
                foreach (var elem in compVoxDoc.Elements)
                {
                    bw.Write(CompressedVoxelElems2Bytes(elem));
                }
                bw.Flush();
                
            }
            finally
            {
                bw.Close();
                fs.Close();
            }

        }

        public static CompressedVoxelDocument LoadCompressedVoxelDocument(string filePath)
        {
            BinaryReader br = new BinaryReader(new FileStream(filePath, FileMode.Open));
            try
            {
                //load origin
                double x = br.ReadDouble();
                double y = br.ReadDouble();
                double z=br.ReadDouble();
                var origin = new Vec3(x, y, z);
                //lode voxelsze
                double dblVoxSize = br.ReadDouble();
                //load scales
                int scaleCount = br.ReadInt32();
                List<CellIndex3D> scales = new List<CellIndex3D>();
                for(int i=0;i<scaleCount;i++)
                {
                    scales.Add(new CellIndex3D(br.ReadInt32(), br.ReadInt32(), br.ReadInt32()));
                }
                //load element
                int numElem=br.ReadInt32();
                List<CompressedVoxelElement> elems = new List<CompressedVoxelElement>() { Capacity = numElem };
                for(int i=0;i<=numElem -1;i++)
                {
                    //Id
                    byte strLen = br.ReadByte();
                    var stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    var elemId = Encoding.Default.GetString(stringByte);//load string
                    //Name
                    strLen = br.ReadByte();
                    stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    var elemName = Encoding.Default.GetString(stringByte);//load string

                    //Category
                    strLen = br.ReadByte();
                    stringByte = new byte[strLen];
                    for (int j = 0; j < strLen; j++)
                    {
                        stringByte[j] = br.ReadByte();
                    }
                    var elemCat = Encoding.Default.GetString(stringByte);//load string
                    /*
                    bool isSupport = cve.IsSupportElement;
                    bool isObstruct = cve.IsObstructElement;
                    bool isTransport = cve.IsTransportElement;
                    bool isActive = cve.IsActive;
                    */
                    bool isSupport=br.ReadBoolean();
                    bool isObstruct=br.ReadBoolean();
                    bool isTransport = br.ReadBoolean();
                    bool isActive = br.ReadBoolean();
                    //rectangle
                    /*
                    var rSt = rect.Start;
                    var rScale = rect.Scale;
                    var rBtmElev = rect.BottomElevation;
                    List<byte> result = new List<byte>();
                    result.AddRange(CellIndex2Byte(rSt));
                    result.AddRange(CellIndex3D2Byte(rScale));
                    result.AddRange(BitConverter.GetBytes(rBtmElev));
                    */
                    int numRects = br.ReadInt32();
                    List<VoxelRectangle> rects = new List<VoxelRectangle>();
                    for(int j=0;j<=numRects-1;j++)
                    {
                        var rSt = new CellIndex(br.ReadInt32(), br.ReadInt32());
                        var rScaleIdx =br.ReadInt32();
                        var rBtmElev = br.ReadInt32();
                        VoxelRectangle vRect = new VoxelRectangle() { BottomElevation = rBtmElev, Start = rSt, ScaleIndex= rScaleIdx };
                        rects.Add(vRect);
                    }
                    CompressedVoxelElement cve = new CompressedVoxelElement() { ElementId=elemId,Category=elemCat,Name =elemName,IsActive=isActive,IsObstructElement=isObstruct,IsTransportElement =isTransport,IsSupportElement =isSupport,VoxelRectangles=rects};
                    elems.Add (cve);
                }
                CompressedVoxelDocument result = new CompressedVoxelDocument();
                result.Origin = origin;
                result.VoxelSize = dblVoxSize;
                result.Elements = elems;
                result.VoxelScale = scales;
                return result;
                
            }
            finally
            {
                br.Close();
            }
        }
        public static byte[] CompressedVoxelElems2Bytes(CompressedVoxelElement cve)
        {
            string cId = cve.ElementId;
            string cNa = cve.Name;
            string cCat = cve.Category;
            bool isSupport = cve.IsSupportElement;
            bool isObstruct = cve.IsObstructElement;
            bool isTransport = cve.IsTransportElement;
            bool isActive = cve.IsActive;
            List<byte> result = new List<byte>();
            result.AddRange(String2Bytes(cId));
            result.AddRange(String2Bytes(cNa));
            result.AddRange(String2Bytes(cCat));
            result.AddRange(BitConverter.GetBytes(isSupport));
            result.AddRange(BitConverter.GetBytes(isObstruct));
            result.AddRange(BitConverter.GetBytes(isTransport));
            result.AddRange(BitConverter.GetBytes(isActive));
            //number of rects
            result.AddRange(BitConverter.GetBytes(cve.VoxelRectangles.Count));
            foreach (var rect in cve.VoxelRectangles)
            {
                result.AddRange(Rect2Bytes(rect));
            }
            return result.ToArray();
        }
        public static byte[] String2Bytes(string str)
        {
            List<byte> result=new List<byte>();
            var stringByte = Encoding.Default.GetBytes(str);
            result.Add((byte)stringByte.Length);
            result.AddRange(stringByte);
            return result.ToArray();
        }
        public static byte[] Vec32Byte(Vec3 input)
        {
            List<byte> results = new List<byte>();
            results.AddRange(BitConverter.GetBytes(input.X));
            results.AddRange(BitConverter.GetBytes(input.Y));
            results.AddRange(BitConverter.GetBytes(input.Z));
            return results.ToArray();
        }
       
        public static byte[] Rect2Bytes(VoxelRectangle rect)
        {
            var rSt = rect.Start;
            var rScaleIdx = rect.ScaleIndex;
            var rBtmElev = rect.BottomElevation;
            List<byte> result = new List<byte>();
            result.AddRange(CellIndex2Byte(rSt));
            result.AddRange(BitConverter.GetBytes(rScaleIdx));
            result.AddRange(BitConverter.GetBytes(rBtmElev));
            return result.ToArray();
        }
        public static byte[] CellIndex2Byte(CellIndex cix)
        {
            List<byte> result = new List<byte>() { Capacity = 8 };
            result.AddRange(BitConverter.GetBytes(cix.Col));
            result.AddRange(BitConverter.GetBytes(cix.Row));
            return result.ToArray();
        }
        public static byte[] CellIndex3D2Byte(CellIndex3D cix3d)
        {
            List<byte> result = new List<byte>() { Capacity = 8 };
            result.AddRange(BitConverter.GetBytes(cix3d.Col));
            result.AddRange(BitConverter.GetBytes(cix3d.Row));
            result.AddRange(BitConverter.GetBytes(cix3d.Layer));
            return result.ToArray();
        }
        /// <summary>
        /// Split accessible region by support rectangles
        /// </summary>
        /// <param name="doc">compressed voxel document</param>
        /// <param name="rect">accessible rectangle to be splitted</param>
        /// <param name="cuttingRectangle">Potential rectangles that is used for splitting accessible Regions</param>
        /// <param name="rectIndex">rectangle index of current rectangle</param>
        /// <param name="strideHeight">man stride height</param>
        /// <returns>the splitted accessible regions</returns>
        public static List<AccessibleRectangle> CutSupportARwithSupVoxelRects(CompressedVoxelDocument doc,  List<AccessibleRectangle> rect,List<VoxelRectangle> cuttingRectangle,int rectIndex,double strideHeight)
        {
            Stack<AccessibleRectangle> rect2Cut = new Stack<AccessibleRectangle>();
            foreach (var r in rect)
            {
                rect2Cut.Push(r);
            }
            foreach (var rectNear in cuttingRectangle)
            {
                if (rectNear.Index == rectIndex)//no need for self-cut
                    continue;
                List<AccessibleRectangle> newRectangleAfterCut = new List<AccessibleRectangle>();
                while (rect2Cut.Count>0)
                {
                    //determine if the cut can be done
                    var rectOut = rect2Cut.Pop();
                    //get rect boundary box
                    rectNear.Get_BoundingBox(doc, out var nearMin, out var nearMax, out var nearBtmElev, out var nearTopElev);
                    double dblElevSt = rectOut.Elevation;
                    double dblElevEd = dblElevSt + strideHeight;
                    CellIndex outMin = rectOut.Min;
                    CellIndex outMax = rectOut.Max;
                    //check if the detection range intersects the box
                    if (RangeIntersects(outMin, outMax, dblElevSt, dblElevEd, nearMin, nearMax, nearBtmElev, nearTopElev))////intersest happen
                    {
                        if (Math.Round(nearTopElev - dblElevSt, 4) == 0) //the 2 support face equal height
                        {
                            if (rectIndex < rectNear.Index)//cut
                            {
                                var newArs = CutAccessibleRectangle(rectOut, nearMin, nearMax);
                                newRectangleAfterCut.AddRange(newArs);
                            }
                            else
                            {
                                newRectangleAfterCut.Add(rectOut);
                            }
                        }
                        else //the support rectangle obstructs the current ar
                        {
                            var newArs = CutAccessibleRectangle(rectOut, nearMin, nearMax);
                            newRectangleAfterCut.AddRange(newArs);
                        }
                    }
                    else
                    {
                        newRectangleAfterCut.Add(rectOut);
                    }
                }
                //push the new accessible regions back to stack
                foreach (var newAr in newRectangleAfterCut)
                {
                    rect2Cut.Push(newAr);
                }
            }
            return rect2Cut.ToList();
        }
        /// <summary>
        /// Split accessible region by obstruction rectangles
        /// </summary>
        /// <param name="doc">compressed voxel document</param>
        /// <param name="rect">accessible rectangle to be splitted</param>
        /// <param name="cuttingRectangle"></param>
        /// <param name="rectIndex"></param>
        /// <param name="offsetIndex"></param>
        /// <param name="strdeHeight"></param>
        /// <param name="minPassingHeight"></param>
        /// <returns></returns>
        public static List<AccessibleRectangle> CutSupportARwithObsVoxelRects(CompressedVoxelDocument doc, List<AccessibleRectangle> rect, List<VoxelRectangle> cuttingRectangle, int offsetIndex,double strdeHeight,double minPassingHeight)
        {
            Stack<AccessibleRectangle> rect2Cut = new Stack<AccessibleRectangle>();
            foreach (var r in rect)
                rect2Cut.Push(r);
            foreach (var rectNear in cuttingRectangle)
            {
                List<AccessibleRectangle> newRectangleAfterCut = new List<AccessibleRectangle>();
                //determine if the cut can be done
                while (rect2Cut.Count > 0)
                {
                    var rectOut = rect2Cut.Pop();
                    //get rect boundary box
                    rectNear.Get_BoundingBox(doc, out var nearMin, out var nearMax, out var nearBtmElev, out var nearTopElev);
                    nearMin -= new CellIndex(offsetIndex, offsetIndex);
                    nearMax += new CellIndex(offsetIndex, offsetIndex);
                    double dblElevSt = rectOut.Elevation + strdeHeight;
                    double dblElevEd = rectOut.Elevation + minPassingHeight;
                    CellIndex outMin = rectOut.Min;
                    CellIndex outMax = rectOut.Max;
                    //check if the detection range intersects the box
                    if (RangeIntersects(outMin, outMax, dblElevSt, dblElevEd, nearMin, nearMax, nearBtmElev, nearTopElev))////intersest happen
                    {
                        var newArs = CutAccessibleRectangle(rectOut, nearMin, nearMax);
                        newRectangleAfterCut.AddRange(newArs);
                    }
                    else
                    {
                        newRectangleAfterCut.Add (rectOut);
                    }
                }
                //push the new accessible regions back to stack
                foreach (var newAr in newRectangleAfterCut)
                {
                    rect2Cut.Push(newAr);
                }
            }
            return rect2Cut.ToList();
        }
        private static bool RangeIntersects(CellIndex cixMin0,CellIndex cixMax0,double elevMin0,double elevMax0,CellIndex cixMin1,CellIndex cixMax1,double elevMin1,double elevMax1)
        {
            if (cixMin0.Col <= cixMax1.Col && cixMin0.Row <= cixMax1.Row
                    && cixMax0.Col >= cixMin1.Col && cixMax0.Row >= cixMin1.Row &&
                elevMin0 <=elevMax1 && elevMax0 >=elevMin1 )
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private static List<AccessibleRectangle> CutAccessibleRectangle(AccessibleRectangle rect2Cut,CellIndex cuttingMin,CellIndex cuttingMax)
        {
            var rectMin = rect2Cut.Min;
            var rectMax = rect2Cut.Max;
            int colMax = rectMax.Col;
            int colMin = rectMin.Col;
            int rowMax = rectMax.Row;
            int rowMin = rectMin.Row;
            List<AccessibleRectangle> result = new List<AccessibleRectangle>();
            if(rectMin.Col<=cuttingMax.Col && rectMin.Row <=cuttingMax.Row && rectMax.Col >=cuttingMin.Col && rectMax.Row >=cuttingMin.Row)//can cut
            {
                int colCommonMin = Math.Max(rectMin.Col, cuttingMin.Col);
                int rowCommonMin = Math.Max(rectMin.Row, cuttingMin.Row);
                int colCommonMax = Math.Min(rectMax.Col, cuttingMax.Col); 
                int rowCommonMax=Math.Min(rectMax.Row, cuttingMax.Row);
                //POTENTIAL rectEast, the boundary is(colCommonMax+1,rowMin)-(colMax,rowmax)
                //rectNorth:(comCommonMin,rowCommonMax+1)-(colCommonMax,rowMax)
                //rectWest:(colMin,rowMin)-(colCommonMin-1,rowMax)
                //rectSouth:(colComonMin,rowMin)-(colCommonMax, rowCommonMin-1)
                List<CellIndex[]> cixRectBdry2Check = new List<CellIndex[]>();
                cixRectBdry2Check.Add(new CellIndex[2] { new CellIndex(colCommonMax + 1, rowMin), new CellIndex(colMax, rowMax) });//east
                cixRectBdry2Check.Add(new CellIndex[2] { new CellIndex(colCommonMin, rowCommonMax + 1), new CellIndex(colCommonMax, rowMax) });//north
                cixRectBdry2Check.Add(new CellIndex[2] { new CellIndex(colMin, rowMin), new CellIndex(colCommonMin - 1, rowMax) });//west
                cixRectBdry2Check.Add(new CellIndex[2] { new CellIndex(colCommonMin, rowMin), new CellIndex(colCommonMax, rowCommonMin - 1) });//south
                foreach (var cixBdry in cixRectBdry2Check)
                {
                    var cixMinTemp = cixBdry[0];
                    var cixMaxTemp = cixBdry[1];
                    var cixScale = cixMaxTemp - cixMinTemp;
                    if (cixScale.Col >= 0 && cixScale.Row >= 0) //the voxel is valid
                    {
                        AccessibleRectangle newAr = new AccessibleRectangle() { Elevation = rect2Cut.Elevation, Min = cixMinTemp, Max = cixMaxTemp };
                        result.Add(newAr);
                    }
                }
            }
            else
            {
                result.Add (rect2Cut);
            }
            return result;
        }

        public static HashSet<VoxelRectangle>  FindVoxelRectanglesWithinRanges( Dictionary<CellIndex3D,List<VoxelRectangle>> searchRange,  CellIndex min,CellIndex max,int layerSt,int layerEd)
        {
            HashSet <VoxelRectangle> rectFound=new HashSet<VoxelRectangle>();
            int colMin = min.Col;
            int colMax = max.Col;
            int rowMin = min.Row;
            int rowMax = max.Row;
           
            //organize rectangles into cells
            for (int col = colMin; col <= colMax; col ++)
            {
                for (int row = rowMin; row <= rowMax; row ++)
                {
                    for (int layer = layerSt; layer <= layerEd; layer++)
                    {
                        CellIndex3D cix = new CellIndex3D(col, row, layer);
                        if (searchRange.TryGetValue(cix, out var searchResul))
                        {
                            foreach (var rectAdj in searchResul)
                            {
                                rectFound.Add(rectAdj);
                            }
                        }
                    }
                }
            }
            return rectFound;
        }

        public static IEnumerable<AccessibleRectangle> GenereateAccessibleRectangles(VoxelRectangleManager manager,double dblStrideHeight,double dblMinPassingHeight,double dblObsAffRng)
        {
            var supRects = manager.SupRects;
            var compVoxelDoc = manager.Doc;
            var bigCellInterval = manager.CellBuffer;
            var voxSize = compVoxelDoc.VoxelSize;
            int obsAffRng = (int)Math.Ceiling(Math.Round(dblObsAffRng / voxSize, 4));
            foreach (var supRect in supRects)
            {
                foreach (var ars in GenereateAccessibleRectangles4SupportRect(supRect, manager, dblStrideHeight, dblMinPassingHeight, obsAffRng))
                {
                    yield return ars;   
                }
            }
        }
        public static IEnumerable<AccessibleRectangle> GenereateAccessibleRectangles4SupportRect(VoxelRectangle supportRect,  VoxelRectangleManager manager, double dblStrideHeight, double dblMinPassingHeight, int obsAffRng)
        {
            var compVoxelDoc = manager.Doc;
            var bigCellInterval = manager.CellBuffer;
            var voxSize = compVoxelDoc.VoxelSize;
            var cell_compVoxRect_Support = manager.Cell_compVoxRect_Support;
            var cell_compVoxRect_Obstruct = manager.Cell_compVoxRect_Obstruct;
            var supRect = supportRect;
            //get support rectangles that may affect supRect
            var scale = supRect.Get_Scale(compVoxelDoc.VoxelScale);
            double dblZ = (supRect.BottomElevation + scale.Layer) / 304.8;
            //search for potential supprot rectangles that may affect curret support
            var colMinSup = supRect.Start.Col;
            var colMaxSup = colMinSup + scale.Col;
            var rowMinSup = supRect.Start.Row;
            var rowMaxSup = rowMinSup + scale.Row;
            CellIndex cixMinSup = new CellIndex(colMinSup / bigCellInterval, rowMinSup / bigCellInterval);
            CellIndex cixMaxSup = new CellIndex(colMaxSup / bigCellInterval, rowMaxSup / bigCellInterval);
            var zMax = dblZ + dblStrideHeight;
            var rectLayerSt = (int)Math.Ceiling(dblZ / (bigCellInterval * voxSize)) - 1;
            var rectLayerEd = (int)Math.Floor(zMax / (bigCellInterval * voxSize)) + 1;
            HashSet<VoxelRectangle> supRectangleNear = LEGOVoxelTool.FindVoxelRectanglesWithinRanges(cell_compVoxRect_Support, cixMinSup, cixMaxSup, rectLayerSt, rectLayerEd);
            //create a rectangle
            AccessibleRectangle ar = new AccessibleRectangle();
            ar.Max = new CellIndex(colMaxSup, rowMaxSup);
            ar.Min = supRect.Start;
            ar.Elevation = dblZ;
            List<AccessibleRectangle> rectSup = new List<AccessibleRectangle>() { ar };
            //try cut the ars in rectGenerates
            List<AccessibleRectangle> validSupRectangles = LEGOVoxelTool.CutSupportARwithSupVoxelRects(compVoxelDoc, rectSup, supRectangleNear.ToList(), supRect.Index, dblStrideHeight);
            //search potential obstruct rectangles
            var colMinObs = colMinSup - obsAffRng;
            var rowMinObs = rowMinSup - obsAffRng;
            var colMaxObs = colMaxSup + obsAffRng;
            var rowMaxObs = rowMaxSup + obsAffRng;
            CellIndex cixMinObs = new CellIndex(colMinObs / bigCellInterval, rowMinObs / bigCellInterval);
            CellIndex cixMaxObs = new CellIndex(colMaxObs / bigCellInterval, rowMaxObs / bigCellInterval);
            var elevObsSt = dblZ + dblStrideHeight;
            var elevObsEd = dblZ + dblMinPassingHeight;
            var elevObsLayerSt = (int)Math.Ceiling(elevObsSt / (bigCellInterval * voxSize)) - 1;
            var elevObsLayerEd = (int)Math.Floor(elevObsEd / (bigCellInterval * voxSize)) + 1;
            HashSet<VoxelRectangle> obsRectangleNear = LEGOVoxelTool.FindVoxelRectanglesWithinRanges(cell_compVoxRect_Obstruct, cixMinObs, cixMaxObs, elevObsLayerSt, elevObsLayerEd);
            //try cut the ars in rectGenerates with solids
            validSupRectangles = LEGOVoxelTool.CutSupportARwithObsVoxelRects(compVoxelDoc, validSupRectangles, obsRectangleNear.ToList(), obsAffRng, dblStrideHeight, dblMinPassingHeight);
            foreach (var arect in validSupRectangles)
            {
                yield return arect;
            }
        }

        public static void FindAccessibleRectangleNeighbors(List<AccessibleRectangle> ars,double strideHeight,int bufferInterval)
        {
            Dictionary<CellIndex3D, List<AccessibleRectangle>> cix_Ars = new Dictionary<CellIndex3D, List<AccessibleRectangle>>();
            List<List<CellIndex3D>> boudaryCells = new List<List<CellIndex3D>>();
            int arIndex = 0;
            foreach (var ar in ars)
            {
                ar.Index = arIndex;
                arIndex += 1;
                CellIndex arMin = ar.Min;
                CellIndex arMax = ar.Max;
                //expand arMax by 1
                arMax += new CellIndex(1, 1);
                //get the big index of ar
                CellIndex minBigger =ConvertBigger_LeftInclusive( arMin,bufferInterval);
                CellIndex maxBigger =ConvertBigger_RightInclusive(arMax,bufferInterval);
                int layer = (int)Math.Floor(ar.Elevation / strideHeight);

                //search along boundary
                int colSt = minBigger.Col;
                int colEd = maxBigger.Col;
                int rowSt = minBigger.Row;
                int rowEd = maxBigger.Row;
                int colStBdry = (int)Math.Ceiling((double)arMin.Col / bufferInterval) - 1;
                int colEdBdry = (int)Math.Floor((double)arMax.Col/ bufferInterval) ;
                int rowStBdry = (int)Math.Ceiling((double)arMin.Row / bufferInterval) - 1;
                int rowEdBdry = (int)Math.Floor((double)arMax.Row / bufferInterval);
                // add edge bottom
                List<CellIndex3D> bdryCells = new List<CellIndex3D>();
                for(int col=colSt;col<=colEd;col++)
                {
                    CellIndex3D cix = new CellIndex3D(col, rowSt, layer);
                    bdryCells.Add(new CellIndex3D(col,rowStBdry,layer));
                    if(!cix_Ars.ContainsKey(cix))
                    {
                        cix_Ars.Add (cix, new List<AccessibleRectangle>() { ar});
                    }
                    else
                    {
                        cix_Ars[cix].Add(ar);
                    }
                }
                //add edge right
                for (int row = rowSt; row <=rowEd; row++)
                {
                    CellIndex3D cix = new CellIndex3D(colEd, row, layer);
                    bdryCells.Add(new CellIndex3D(colEdBdry,row,layer));
                    if (!cix_Ars.ContainsKey(cix))
                    {
                        cix_Ars.Add(cix, new List<AccessibleRectangle>() { ar });
                    }
                    else
                    {
                        cix_Ars[cix].Add(ar);
                    }
                }
                // add edge top
                for (int col = colEd; col >= colSt; col--)
                {
                    CellIndex3D cix = new CellIndex3D(col, rowEd, layer);
                    bdryCells.Add(new CellIndex3D(col, rowEdBdry, layer));
                    if (!cix_Ars.ContainsKey(cix))
                    {
                        cix_Ars.Add(cix, new List<AccessibleRectangle>() { ar });
                    }
                    else
                    {
                        cix_Ars[cix].Add(ar);
                    }
                }
                //add edge left
                for (int row = rowEd; row >=rowSt; row--)
                {
                    CellIndex3D cix = new CellIndex3D(colSt, row, layer);
                    bdryCells.Add(new CellIndex3D(colStBdry, row, layer));
                    if (!cix_Ars.ContainsKey(cix))
                    {
                        cix_Ars.Add(cix, new List<AccessibleRectangle>() { ar });
                    }
                    else
                    {
                        cix_Ars[cix].Add(ar);
                    }
                }
                boudaryCells.Add (bdryCells);
            }
            //search ars
            for(int i=0;i<=ars.Count -1;i++)
            {
                var ar = ars[i];
                var arBdryCells=boudaryCells[i];
                HashSet<AccessibleRectangle> rectNear = new HashSet<AccessibleRectangle>();
                foreach (var cixOriginal in arBdryCells)
                {
                    for(int offLayer=-1;offLayer <=1;offLayer ++)
                    {
                        CellIndex3D cix = new CellIndex3D(cixOriginal.Col, cixOriginal.Row, cixOriginal.Layer + offLayer);
                        if (cix_Ars.TryGetValue(cix, out var rectsAdj))
                        {
                            foreach (var arAdj in rectsAdj)
                            {
                                if (arAdj != ar)
                                {
                                    rectNear.Add(arAdj);
                                }
                            }
                        }
                    }
                   
                }
                foreach (var rectNearTemp in rectNear)
                {
                    if(NavigableTo(ar,rectNearTemp,strideHeight))
                    {
                        ar.AdjacentRectangles.Add(rectNearTemp);
                    }
                }
            }
        }
        
        
        
        private static CellIndex ConvertBigger_LeftInclusive(CellIndex cix, int bufferInterval)
        {
            int col = (int)Math.Floor(cix.Col / (double)bufferInterval);
            int row= (int)Math.Floor(cix.Row / (double)bufferInterval);
            return new CellIndex(col,row);
        }
        private static CellIndex ConvertBigger_RightInclusive(CellIndex cix, int bufferInterval)
        {
            int col = (int)Math.Ceiling(cix.Col / (double)bufferInterval)-1;
            int row = (int)Math.Ceiling(cix.Row / (double)bufferInterval)-1;
            return new CellIndex(col, row);
        }
        private static bool NavigableTo(AccessibleRectangle source, AccessibleRectangle target,double strideHeight)
        {
           

            if (Math.Abs (source.Elevation -target.Elevation)<=strideHeight)
            {
                CellIndex minS = source.Min;
                CellIndex maxS = source.Max + new CellIndex(1, 1);
                CellIndex minT = target.Min;
                CellIndex maxT = target.Max + new CellIndex(1, 1);
                CellIndex disMaxT_MinS = maxT - minS;
                CellIndex disMaxS_MinT = maxS - minT;

                int colMin0 = minS.Col;
                int rowMin0 = minS.Row;
                int colMax0 = maxS.Col;
                int rowMax0 = maxS.Row;

                int colMin1 = minT.Col;
                int rowMin1 = minT.Row;
                int colMax1 = maxT.Col;
                int rowMax1 = maxT.Row;

                if((disMaxS_MinT.Col >=0 && disMaxT_MinS.Col >=0)&& disMaxS_MinT.Row>0 && disMaxT_MinS.Row >0)
                {
                    return true;
                }
                
                if ((disMaxS_MinT.Col >0 && disMaxT_MinS.Col > 0) && disMaxS_MinT.Row >= 0 && disMaxT_MinS.Row >= 0)
                {
                    return true;
                }

                return false;
            }
            return false;
        }

        
    }

    public class VoxelRectangleManager
    {
        public CompressedVoxelDocument Doc { get; set; }
        public Dictionary<CellIndex3D, List<VoxelRectangle>> Cell_compVoxRect_Support;
        public  Dictionary<CellIndex3D, List<VoxelRectangle>> Cell_compVoxRect_Obstruct;
        public  List<VoxelRectangle> SupRects = new List<VoxelRectangle>();
        public  List<VoxelRectangle> ObsRects = new List<VoxelRectangle>();
        public int CellBuffer { get; set; }
        public VoxelRectangleManager(CompressedVoxelDocument doc,int cellBuffer)
        {
            //param 
            this.CellBuffer=cellBuffer;
            this.Doc = doc;
            //use a dictionary to store element spatical occupy situation
            int bigCellInterval = 10;// the buffer is 10 times that of the voxel size
            Cell_compVoxRect_Support = new Dictionary<CellIndex3D, List<VoxelRectangle>>();
            Cell_compVoxRect_Obstruct = new Dictionary<CellIndex3D, List<VoxelRectangle>>();
            SupRects = new List<VoxelRectangle>();
            ObsRects = new List<VoxelRectangle>();
            Vec3 origin = Doc.Origin;
            double voxSize = Doc.VoxelSize;
            //change the offset to int
            foreach (var elem in doc.Elements)
            {
                if (!elem.IsActive)
                {
                    continue;
                }
                foreach (var rect in elem.VoxelRectangles)
                {
                    if (elem.IsSupportElement)
                    {
                        rect.Index = SupRects.Count;
                        SupRects.Add(rect);
                    }
                    if (elem.IsObstructElement)
                    {
                        ObsRects.Add(rect);
                    }
                    rect.Get_BoundingBox(Doc, out CellIndex min, out CellIndex max, out double bottomElev, out double topElev);
                    int colStOriginal = min.Col;
                    int colEdOriginal = max.Col;
                    int rowStOriginal = min.Row;
                    int rowEdOriginal = max.Row;
                    int layerSt = (int)Math.Floor((bottomElev - origin.Z) / (bigCellInterval * voxSize));
                    int layerEd = (int)Math.Floor((topElev - origin.Z) / (bigCellInterval * voxSize));
                    //organize rectangles into cells
                    int colSt = colStOriginal / bigCellInterval;
                    int colEd = colEdOriginal / bigCellInterval;
                    int rowSt = rowStOriginal / bigCellInterval;
                    int rowEd = rowEdOriginal / bigCellInterval;
                    for (int col = colSt; col <= colEd; col++)
                    {
                        for (int row = rowSt; row <= rowEd; row++)
                        {
                            for (int layer = layerSt; layer <= layerEd; layer++)
                            {
                                CellIndex3D bufferCell3D = new CellIndex3D(col, row, layer);

                                if (elem.IsSupportElement)
                                {
                                    if (!Cell_compVoxRect_Support.ContainsKey(bufferCell3D))
                                    {
                                        Cell_compVoxRect_Support.Add(bufferCell3D, new List<VoxelRectangle>());
                                    }
                                    Cell_compVoxRect_Support[bufferCell3D].Add(rect);
                                }
                                if (elem.IsObstructElement)
                                {
                                    if (!Cell_compVoxRect_Obstruct.ContainsKey(bufferCell3D))
                                    {
                                        Cell_compVoxRect_Obstruct.Add(bufferCell3D, new List<VoxelRectangle>());
                                    }
                                    Cell_compVoxRect_Obstruct[bufferCell3D].Add(rect);
                                }
                            }
                        }
                    }

                }
            }
        }
    }

    public class TimeRecorder
    {
        public DataTable Data { get; set; }
        private List<List<object>> time = new List<List<object>>();
        private List<DataColumn> header = new List<DataColumn>();
        private Stopwatch stopwatch = new Stopwatch();
        public TimeRecorder(string[] header)
        {
            Data = new DataTable();
            /*
            
            foreach (var headerText in header)
            {
                Data.Columns.Add(headerText);
            }
            */
        }
        public TimeRecorder()
        {

        }
        public void Start()
        {
            
            stopwatch.Start();
        }
        public void RecordWithoutTime(string data)
        {
            int curRec = time.Count;
            if(curRec > 0)
            {
                time[curRec].Add(data);
            }
            else
            {
                time.Add(new List<object>() { data});
            }
            
        }
        public void RecordWithoutTime(string headerText,string data)
        {
            this.header.Add (new DataColumn( headerText));
            int curRec = time.Count-1;
            if (curRec >= 0)
            {
                time[curRec].Add(data);
            }
            else
            {
                time.Add(new List<object>() { data });
            }

        }
        public void RecordTimeAndReStart()
        {

        }
        public void RecordTimeAndReStart(string headerText)
        {
            this.header.Add(new DataColumn( headerText));
            stopwatch.Stop();
            var data= stopwatch.Elapsed.TotalMilliseconds;
            int curRec = time.Count-1;
            if (curRec >= 0)
            {
                time[curRec].Add(data);
            }
            else
            {
                time.Add(new List<object>() { data });
            }
            stopwatch.Restart();
        }
        public void Stop()
        {

        }

        public void Stop(string headerText)
        {
            
            stopwatch.Stop();
            this.header.Add(new DataColumn(headerText));
           
            var data = stopwatch.Elapsed.TotalMilliseconds;
            int curRec = time.Count - 1;
            if (curRec >= 0)
            {
                time[curRec].Add(data);
            }
            else
            {
                time.Add(new List<object>() { data });
            }
            if (this.Data.Columns.Count == 0)
            {
                this.Data = new DataTable();
                this.Data.Columns.AddRange(this.header.ToArray());
            }
            
            foreach (var t in this.time)
            {
                this.Data.Rows.Add(t.ToArray());
            }
            time.Clear();
        }
        public void Save(string filePath)
        {
            CSV.Save(filePath, this.Data);
        }
    }
    #endregion
    public class LightWeightVoxelElement
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public List<VoxelBox> Boxes { get; set; } = new List<VoxelBox>();
        public bool IsSupportElement { get; set; } = false;
        public bool IsObstructElement { get; set; } = true;
        public bool IsTransportElement { get; set; } = false;
        public LightWeightVoxelElement()
        {

        }
        public LightWeightVoxelElement(VoxelElement ve)
        {
            this.Name = ve.Name;
            this.Category = ve.Category;
            this.ElementId = ve.ElementId;
            this.IsSupportElement = ve.IsSupportElement;
            this.IsObstructElement = ve.IsObstructElement;
            this.IsTransportElement = ve.IsTransportElement;
            this.Boxes = new List<VoxelBox>();
            Dictionary<(int, int), List<Voxel>> dicVoxElev = new Dictionary<(int, int), List<Voxel>>();
            foreach (var vox in ve.Voxels)
            {
                int btmElev = (int)vox.BottomElevation;
                int topElev = (int)vox.TopElevation;
                if (dicVoxElev.TryGetValue((btmElev, topElev), out var lst))
                {
                    lst.Add(vox);
                }
                else
                {
                    dicVoxElev.Add((btmElev, topElev), new List<Voxel>() { vox });
                }
            }

            foreach (var kvp in dicVoxElev)
            {
                var btmElev = kvp.Key.Item1;
                var topElev = kvp.Key.Item2;
                var voxels = kvp.Value;
                //get voxel boundary
                int colMin = int.MaxValue;
                int rowMin = int.MaxValue;
                int colMax = int.MinValue;
                int rowMax = int.MinValue;
                foreach (var vox in kvp.Value)
                {
                    colMin = Math.Min(vox.ColIndex, colMin);
                    colMax = Math.Max(vox.ColIndex, colMax);
                    rowMin = Math.Min(vox.RowIndex, rowMin);
                    rowMax = Math.Max(vox.RowIndex, rowMax);
                }
                var colRange = colMax - colMin + 1;
                var rowRange = rowMax - rowMin + 1;
                bool[,] voxOccupy = new bool[colRange, rowRange];
                foreach (var vox in kvp.Value)
                {
                    var colLoc = vox.ColIndex - colMin;
                    var rowLoc = vox.RowIndex - rowMin;
                    voxOccupy[colLoc, rowLoc] = true;
                }
                //apply ScanLine method
                List<(int col0, int row0, int col1, int row1)> activeRect = new List<(int col0, int row0, int col1, int row1)>();
                for (int col = 0; col < colRange; col++)
                {
                    bool prevStrip = voxOccupy[col, 0];
                    int rowSt = 0;
                    var rowEd = 0;
                    List<(int col0, int row0, int col1, int row1)> colRects = new List<(int col0, int row0, int col1, int row1)>();
                    //find valid rowSt
                    for (int row = 1; row < rowRange; row++)
                    {
                        var currenStrip = voxOccupy[col, row];
                        if (currenStrip == prevStrip)
                        {
                            rowEd = row;
                        }
                        else//stop scan
                        {
                            rowEd = row - 1;
                            if (prevStrip == true)
                            {
                                colRects.Add((col, rowSt, col, rowEd));

                            }
                            rowSt = row;
                            rowEd = row;
                            prevStrip = currenStrip;
                        }
                    }
                    //add last
                    if (prevStrip == true)
                    {
                        colRects.Add((col, rowSt, col, rowEd));
                    }

                    //is it necessary to expand active rect
                    bool merge = true;
                    if (colRects.Count == activeRect.Count)
                    {
                        for (int i = 0; i <= colRects.Count - 1; i++)
                        {
                            var actRect = activeRect[i];
                            var (col0, row0, col1, row1) = colRects[i];

                            if (actRect.row0 != row0 || actRect.row1 != row1) //merge
                            {
                                merge = false;
                                break;
                            }
                        }
                    }
                    else
                    {
                        merge = false;
                    }
                    if (merge)
                    {
                        for (int i = 0; i <= colRects.Count - 1; i++)
                        {
                            var actRect = activeRect[i];
                            var newRect = (actRect.col0, actRect.row0, col, actRect.row1);
                            activeRect[i] = newRect;
                        }
                    }
                    else
                    {
                        foreach (var rect in activeRect)
                        {
                            var min = new CellIndex3D(rect.col0 + colMin, rect.row0 + rowMin, btmElev);
                            var max = new CellIndex3D(rect.col1 + colMin, rect.row1 + rowMin, topElev);
                            var box = new VoxelBox(min, max) { Host = this };

                            this.Boxes.Add(box);
                        }
                        activeRect = colRects;
                    }
                }
                //add final
                foreach (var rect in activeRect)
                {
                    var min = new CellIndex3D(rect.col0 + colMin, rect.row0 + rowMin, btmElev);
                    var max = new CellIndex3D(rect.col1 + colMin, rect.row1 + rowMin, topElev);
                    var box = new VoxelBox(min, max) { Host = this };
                    this.Boxes.Add(box);
                }
            }
        }

        public VoxelElement Convert2VoxelElement(int voxSize)
        {
            var ve = new VoxelElement();
            ve.ElementId = this.ElementId;
            ve.Category = this.Category;
            ve.IsObstructElement = this.IsObstructElement;
            ve.IsSupportElement = this.IsSupportElement;
            ve.IsTransportElement = this.IsTransportElement;
            ve.Voxels = new List<Voxel>();
            foreach (var box in this.Boxes)
            {
                CellIndex3D min = box.Min;
                CellIndex3D max = box.Max;
                int colSt = min.Col;
                int colEd = max.Col;
                int rowSt = min.Row;
                int rowEd = max.Row;
                int btmElev = min.Layer;
                int topElev = max.Layer;
                for (int col = colSt; col <= colEd; col++)
                {
                    for (int row = rowSt; row <= rowEd; row++)
                    {
                        var vox = new Voxel()
                        {
                            BottomElevation = btmElev,
                            TopElevation = topElev,
                            ColIndex = col,
                            RowIndex = row,
                        };
                        ve.Voxels.Add(vox);
                    }
                }
            }
            return ve;
        }
    }

    public class VoxelBox
    {
        public CellIndex3D Min { get; set; }
        public CellIndex3D Max { get; set; }
        public CellIndex3D CenterMM { get; set; }
        public LightWeightVoxelElement Host { get; set; }
        public VoxelBox(CellIndex3D min, CellIndex3D max)
        {
            Min = min;
            Max = max;
        }
        public void CalculateCenter(int voxelSize)
        {
            this.CenterMM = new CellIndex3D((Max.Col + 1 + Min.Col) / 2 * voxelSize, (Max.Row + Min.Row + 1) / 2 * voxelSize, (Max.Layer + Min.Layer) / 2);
        }
    }
    public static class CSV
    {
        public static void Save(string fileName,DataTable dt)
        {
            StreamWriter sw = new StreamWriter(fileName,false,Encoding.Default);
            List<string> header = new List<string>();
            foreach (DataColumn dc in  dt.Columns)
            {
                header.Add(dc.ColumnName);
            }
            sw.WriteLine(string.Join(",", header));
            foreach (DataRow dr in dt.Rows)
            {
                sw.WriteLine(string.Join(",", dr.ItemArray));
            }
            sw.Flush();
            sw.Close();

        }
    }
}
