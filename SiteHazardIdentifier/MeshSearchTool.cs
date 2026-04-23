using Autodesk.Revit.DB;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index;
using NetTopologySuite.Index.Strtree;
using RevitVoxelzation;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Shapes;
using Polygon = NetTopologySuite.Geometries.Polygon;

namespace SiteHazardIdentifier
{
    public static class SpatialSearchTool
    {
        public static IEnumerable<(HazardMeshElementInfo elem,double distance)> GetElementsWithinDistance(List<HazardMeshElementInfo> ignitionElems, List<HazardMeshElementInfo> elems,double distance)
        {
            var factory = new GeometryFactory();
            var vertices2Search = new List<Vec3>();
            var triangles2Search = new List<int>();
            //collect the triangles and vertices of thee element
            foreach (var ig in ignitionElems)
            {
                var verticesOff = vertices2Search.Count;
                foreach (var v in ig.GetVertices())
                {
                    vertices2Search.Add(v);
                }
                foreach (var tri in ig.GetTriangleIndexes())
                {
                    triangles2Search.Add(tri + verticesOff);
                }
            }
            // 1. 将两个模型的三角形分别投影为二维多边形列表
            var polygons2Search = ProjectToPolygons(vertices2Search, triangles2Search.ToArray(), factory);
            // 2. 为 polygonsA 构建 STRtree 索引
            var tree = new STRtree<Polygon>();
            foreach (var poly in polygons2Search)
            {
                tree.Insert(poly.EnvelopeInternal, poly);
            }
            tree.Build();
            //获得距离
            bool[] elemScanned=new bool[elems.Count];
            for(int i=0;i<elems.Count;i++ ) 
            {
                var e = elems[i];
               
                //获取三角形投影
                List<Vec3> verticesB = e.GetVertices().ToList();
                int[] trianglesB = e.GetTriangleIndexes().ToArray();
                var polygonsB = ProjectToPolygons(verticesB, trianglesB, factory);
                foreach (var poly in polygonsB)
                {
                    if (elemScanned[i] == true) //the element has been ignited, skip scanning
                        break;
                    var envelop = poly.EnvelopeInternal.Copy();
                    envelop.ExpandBy(distance);
                    //seearch tree
                    var polygonFound = tree.Query(envelop);
                    if (polygonFound.Count != 0)
                    {
                        foreach (var polyFound in polygonFound)
                        {
                            var disTemp = polyFound.Distance(poly);
                            
                            if (disTemp <= distance)
                            {
                                elemScanned[i] = true;
                                yield return ((e, disTemp));
                                break;
                            }
                        }
                    }
                }
            }
        }

        private static List<Polygon> ProjectToPolygons(
            List<Vec3> vertices, int[] triangles, GeometryFactory factory)
        {
            var polygons = new List<Polygon>(triangles.Length / 3);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i1 = triangles[i];
                int i2 = triangles[i + 1];
                int i3 = triangles[i + 2];

                var v1 = vertices[i1];
                var v2 = vertices[i2];
                var v3 = vertices[i3];

                var coord1 = new Coordinate(v1.X, v1.Y);
                var coord2 = new Coordinate(v2.X, v2.Y);
                var coord3 = new Coordinate(v3.X, v3.Y);

                /*
                // 跳过共线退化三角形
                if (IsCollinear(coord1, coord2, coord3))
                    continue;
                */
                var shell = factory.CreateLinearRing(new[] { coord1, coord2, coord3, coord1 });
                polygons.Add(factory.CreatePolygon(shell));
            }

            return polygons;
        }


        

    }

    public class HazardBVHTree
    {
        public HazardBVHTreeNode RootNode { get; set; }

        public void Build(List<HazardBoxElementInfo> elems, int boxLimit)
        {
            //expand voxBox
            List<HazardVoxelBox> boxes = new List<HazardVoxelBox>();
            foreach (var elem in elems)
            {
                boxes.AddRange(elem.Boxes);
            }
            //calculate centr
            foreach (var box in boxes)
            {
                box.CalculateCenter();
            }
            //create a root node
            var root = new HazardBVHTreeNode();
            this.RootNode = root;
            GenerateNode(root, boxes, boxLimit);
        }

        private void GenerateNode(HazardBVHTreeNode thisNode, List<HazardVoxelBox> boxes2Add, int boxLimit)
        {
            //get the center of the node
            thisNode.Bound = new HazardBVHNodeBoundary(boxes2Add);
            if (boxes2Add.Count <= boxLimit)
            {
                thisNode.VoxelBoxes = boxes2Add;
            }
            else
            {
                var boxMax = thisNode.Bound.Max;
                var boxMin = thisNode.Bound.Min;
                var colMax = boxMax.Col;
                var colMin = boxMin.Col;
                var rowMax = boxMax.Row;
                var rowMin = boxMin.Row;
                var layerMax = boxMax.Layer;
                var layerMin = boxMin.Layer;
                var colSize = colMax - colMin;
                var rowSize = rowMax - rowMin;
                var layerSize = layerMax - layerMin;
                if (colSize >= rowSize && colSize >= layerSize)
                {
                    boxes2Add.Sort((a, b) => a.Center.Col.CompareTo(b.Center.Col));
                }
                else if (rowSize >= colSize && rowSize >= layerSize)
                {
                    boxes2Add.Sort((a, b) => a.Center.Row.CompareTo(b.Center.Row));
                }
                else
                {
                    boxes2Add.Sort((a, b) => a.Center.Layer.CompareTo(b.Center.Layer));
                }
                //divide voxels
                HazardBVHTreeNode leftNode = new HazardBVHTreeNode();
                HazardBVHTreeNode rightNode = new HazardBVHTreeNode();
                thisNode.Left = leftNode;
                leftNode.Parent = thisNode;

                thisNode.Right = rightNode;
                rightNode.Parent = thisNode;
                int leftElem = boxes2Add.Count / 2;
                List<HazardVoxelBox> boxesL = new List<HazardVoxelBox>();
                List<HazardVoxelBox> boxesR = new List<HazardVoxelBox>();
                for (int i = 0; i < leftElem; i++)
                {
                    boxesL.Add(boxes2Add[i]);
                }
                for (int i = leftElem; i < boxes2Add.Count; i++)
                {
                    boxesR.Add(boxes2Add[i]);
                }
                GenerateNode(leftNode, boxesL, boxLimit);
                GenerateNode(rightNode, boxesR, boxLimit);
            }
        }

        public IEnumerable<HazardBVHTreeNode> GetAllNodes()
        {
            Stack<HazardBVHTreeNode> stkNodes = new Stack<HazardBVHTreeNode>();
            stkNodes.Push(RootNode);
            while (stkNodes.Count != 0)
            {
                var node = stkNodes.Pop();
                yield return node;
                if (node.Left != null)
                    stkNodes.Push(node.Left);
                if (node.Right != null)
                    stkNodes.Push(node.Right);
            }
        }

       
    }


    public class HazardBVHTreeNode
    {
        public HazardBVHTreeNode Left { get; set; }
        public HazardBVHTreeNode Right { get; set; }
        public HazardBVHTreeNode Parent { get; set; }
        public List<HazardVoxelBox> VoxelBoxes { get; set; }
        public HazardBVHNodeBoundary Bound { get; set; }

    }

    public class HazardBVHNodeBoundary
    {
        public CellIndex3D Min { get; set; }
        public CellIndex3D Max { get; set; }
        public HazardBVHNodeBoundary(List<HazardVoxelBox> boxes)
        {
            //get voxel boundary
            int colMin = int.MaxValue;
            int rowMin = int.MaxValue;
            int colMax = int.MinValue;
            int rowMax = int.MinValue;
            int layerMin = int.MaxValue;
            int layerMax = int.MinValue;
            foreach (var box in boxes)
            {
                colMin = Math.Min(box.Min.Col, colMin);
                colMax = Math.Max(box.Max.Col , colMax);
                rowMin = Math.Min(box.Min.Row, rowMin);
                rowMax = Math.Max(box.Max.Row, rowMax);
                layerMin = Math.Min(layerMin, box.Min.Layer);
                layerMax = Math.Max(layerMax, box.Max.Layer);
            }
           
            this.Max = new CellIndex3D(colMax, rowMax, layerMax);
            this.Min = new CellIndex3D(colMin, rowMin, layerMin);
        }
        public HazardBVHNodeBoundary(CellIndex3D min,CellIndex3D max)
        {
            this.Min = min;
            this.Max =max;
        }

        public bool BoundaryIntersect(HazardBVHNodeBoundary other)
        {
            return (this.Min.Col <= other.Max.Col && this.Max.Col >= other.Min.Col
                && this.Min.Row <= other.Max.Row && this.Max.Row >= other.Min.Row &&
                this.Min.Layer <= other.Max.Layer && this.Max.Layer >= other.Min.Layer);
        }

        
    }
    public enum BoxDirection
    {
        Negative=0,//Bounding box is to the negative direction of the axis
        Mid=1,//Bounding box intersects with current axis
        Positive=2,//bounding box is to the right direction of the axis
    }
    public class HazardVoxelBox
    {
        
        public HazardBVHNodeBoundary Box { get; set; }
        public HazardBoxElementInfo Host { get; set; }
        public CellIndex3D Min
        {
            get
            {
                return Box.Min;
            }
        }
        public CellIndex3D Max
        {
            get
            {
                return Box.Max;
            }
        }

        public HazardVoxelBox(CellIndex3D min,CellIndex3D max,HazardBoxElementInfo elem)
        {
            this.Host = elem;
            this.Box=new HazardBVHNodeBoundary(min,max);
        }
        public CellIndex3D Center { get; set;  }
        public bool VerticalNearFire { get; internal set; } = false;

        public void CalculateCenter()
        {
            if(this.Center == null)
            {
                this.Center = new CellIndex3D((Min.Col + Max.Col) / 2, (Min.Row + Max.Row) / 2, (Min.Layer + Max.Layer) / 2);
            }
        }
    }
   
}
