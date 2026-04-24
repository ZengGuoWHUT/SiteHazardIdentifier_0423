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

        public static IEnumerable<(HazardAABBElementInfo elem, double distance)> GetElementsWithinDistance(List<HazardAABBElementInfo> ignitionElems, List<HazardAABBElementInfo> elems, HazardBVHTree tree, double distance)
        {
            foreach (var elem in ignitionElems)
            {
                foreach (var box in elem.Boxes)
                {
                    foreach (var elemFound in tree.SearchWithinDistance(box, new Vec3(distance, distance, 0), distance))
                    {
                        yield return elemFound;
                    }
                }
            }
        }
        /// <summary>
        /// 包围盒相交的暴力算法
        /// </summary>
        /// <param name="ignitionElems"></param>
        /// <param name="elems"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public static IEnumerable<(HazardAABBElementInfo elem, double distance)> GetElementsWithinDistance(List<HazardAABBElementInfo> ignitionElems, List<HazardAABBElementInfo> elems, double distance)
        {
            foreach (var elem in ignitionElems)
            {
                foreach (var box in elem.Boxes)
                {
                    Vec3 min = box.Min;
                    Vec3 max = box.Max;
                    Vec3 searchMin = min - new Vec3(distance, distance, 0);
                    Vec3 searchMax = max + new Vec3(distance, distance, 0);
                    foreach (var elem2 in elems)
                    {
                        if (elem2.Ignited)
                            continue;
                        foreach (var box2 in elem2.Boxes)
                        {
                            if (box2.Max.X < searchMin.X || box2.Min.X > searchMax.X ||
                                box2.Max.Y < searchMin.Y || box2.Min.Y > searchMax.Y)
                            {
                                continue; // No intersection
                            }
                            else
                            {
                                var disTemp = CalculateHorizontalDistance(box, box2);
                                if (disTemp <= distance)
                                {
                                    elem2.Ignited = true;
                                    yield return (elem2, disTemp);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static double CalculateHorizontalDistance(HazardAABB box1,HazardAABB box2)
        {
            //check if box intersects horizontally
            Vec3 min1= box1.Min;
            Vec3 max1=box1.Max;
            Vec3 min2= box2.Min;
            Vec3 max2=box2.Max;
            if(min1.X<=max2.X && max1.X>=min2.X && min1.Y<=max2.Y && max1.Y>=min2.Y)//box intersect horizontally
            {
                return 0;
            }
            else
            {
                //calculate horizontal distance
                double dx = 0;
                if (max1.X < min2.X)
                    dx = min2.X - max1.X;
                else if (min1.X > max2.X)
                    dx = min1.X - max2.X;
                double dy = 0;
                if (max1.Y < min2.Y)
                    dy = min2.Y - max1.Y;
                else if (min1.Y > max2.Y)
                    dy = min1.Y - max2.Y;
                return Math.Sqrt(dx * dx + dy * dy);
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

        public void Build(List<HazardAABBElementInfo> elems, int boxLimit)
        {
            //expand voxBox
            List<HazardAABB> boxes = new List<HazardAABB>();
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

        private void GenerateNode(HazardBVHTreeNode thisNode, List<HazardAABB> boxes2Add, int boxLimit)
        {
            //get the center of the node
            thisNode.Bound = new HazardBVHNodeBoundary(boxes2Add);
            if (boxes2Add.Count <= boxLimit)
            {
                thisNode.AABBBoxes = boxes2Add;
            }
            else
            {
                var boxMax = thisNode.Bound.Max;
                var boxMin = thisNode.Bound.Min;
                var colMax = boxMax.X;
                var colMin = boxMin.X;
                var rowMax = boxMax.Y;
                var rowMin = boxMin.Y;
                var layerMax = boxMax.Z;
                var layerMin = boxMin.Z;
                var colSize = colMax - colMin;
                var rowSize = rowMax - rowMin;
                var layerSize = layerMax - layerMin;
                if (colSize >= rowSize && colSize >= layerSize)
                {
                    boxes2Add.Sort((a, b) => a.Center.X.CompareTo(b.Center.X));
                }
                else if (rowSize >= colSize && rowSize >= layerSize)
                {
                    boxes2Add.Sort((a, b) => a.Center.Y.CompareTo(b.Center.Y));
                }
                else
                {
                    boxes2Add.Sort((a, b) => a.Center.Z.CompareTo(b.Center.Z));
                }
                //divide voxels
                HazardBVHTreeNode leftNode = new HazardBVHTreeNode();
                HazardBVHTreeNode rightNode = new HazardBVHTreeNode();
                thisNode.Left = leftNode;
                leftNode.Parent = thisNode;

                thisNode.Right = rightNode;
                rightNode.Parent = thisNode;
                int leftElem = boxes2Add.Count / 2;
                List<HazardAABB> boxesL = new List<HazardAABB>();
                List<HazardAABB> boxesR = new List<HazardAABB>();
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
        public IEnumerable<(HazardAABBElementInfo, double)> SearchWithinDistance(HazardAABB fireBox, Vec3 expension, double searchDistanceMM)
        {
            Vec3 searchMin = fireBox.Min - expension;
            Vec3 searchMax = fireBox.Max + expension;
            List<HazardAABBElementInfo> result = new List<HazardAABBElementInfo>();
            var curNode = this.RootNode;
            List<HazardAABB> box2Search = new List<HazardAABB>();
            HazardBVHNodeBoundary searchbdry = new HazardBVHNodeBoundary(searchMin, searchMax);
            Stack<HazardBVHTreeNode> stkNodes = new Stack<HazardBVHTreeNode>();
            stkNodes.Push(curNode);
            while (stkNodes.Count != 0)
            {
                curNode = stkNodes.Pop();
                if (curNode.AABBBoxes != null)
                {
                    foreach (var elemBox in curNode.AABBBoxes)
                    {
                        if (searchbdry.BoundaryIntersect(elemBox.Box) && !elemBox.Host.Ignited)//if iteernate to the leaf and the elem has been ignited
                            box2Search.Add(elemBox);
                    }
                }
                else
                {
                    var nodeLeft = curNode.Left;
                    var nodeRight = curNode.Right;
                    if (searchbdry.BoundaryIntersect(nodeLeft.Bound))
                        stkNodes.Push(nodeLeft);
                    if (searchbdry.BoundaryIntersect(nodeRight.Bound))
                        stkNodes.Push(nodeRight);
                }
            }
            //detail search
            foreach (var box in box2Search)
            {
                if (!box.Host.Ignited)
                {
                    var thisBox = fireBox.Box;
                    var dis = thisBox.CalculateHorizontalDistance(box.Box);
                    if (dis <= searchDistanceMM)
                    {
                        box.Host.Ignited = true;
                        yield return (box.Host, dis);
                    }
                }
            }
        }

    }


    public class HazardBVHTreeNode
    {
        public HazardBVHTreeNode Left { get; set; }
        public HazardBVHTreeNode Right { get; set; }
        public HazardBVHTreeNode Parent { get; set; }
        public List<HazardAABB> AABBBoxes { get; set; }
        public HazardBVHNodeBoundary Bound { get; set; }

    }

    public class HazardBVHNodeBoundary
    {
        public Vec3 Min { get; set; }
        public Vec3 Max { get; set; }
        public HazardBVHNodeBoundary(List<HazardAABB> boxes)
        {
            //get voxel boundary
            double xMin = double.MaxValue;
            double yMin = double.MaxValue;
            double xMax = double.MinValue;
            double yMax = double.MinValue;
            double zMin = double.MaxValue;
            double zMax = double.MinValue;
            foreach (var box in boxes)
            {
                xMin = Math.Min(box.Min.X, xMin);
                xMax = Math.Max(box.Max.X , xMax);
                yMin = Math.Min(box.Min.Y, yMin);
                yMax = Math.Max(box.Max.Y, yMax);
                zMin = Math.Min(box.Min.Z, zMin);
                zMax = Math.Max(box.Max.Z, zMax);
            }
           
            this.Max = new Vec3(xMax, yMax, zMax);
            this.Min = new Vec3(xMin, yMin, zMin);
        }
        public HazardBVHNodeBoundary(Vec3 min,Vec3 max)
        {
            this.Min = min;
            this.Max =max;
        }

        public bool BoundaryIntersect(HazardBVHNodeBoundary other)
        {
            return (this.Min.X <= other.Max.X && this.Max.X >= other.Min.X
                && this.Min.Y <= other.Max.Y && this.Max.Y >= other.Min.Y &&
                this.Min.Z <= other.Max.Z && this.Max.Z >= other.Min.Z);
        }

        public double CalculateHorizontalDistance(HazardBVHNodeBoundary other)
        {
            if (this.BoundaryIntersect(other))
                return 0;
            //get the direction of otheer
            var dirThisMax2OtherMin = other.Min - this.Max;
            var dirThisMin2OtherMax = other.Max - this.Min;
            BoxDirection[] dirOnAxis = new BoxDirection[3];
            //scan column
            if (dirThisMax2OtherMin.X > 0) //other is to the positive side of col
            {
                dirOnAxis[0] = BoxDirection.Positive;
            }
            else if (dirThisMin2OtherMax.X < 0)
            {
                dirOnAxis[0] = BoxDirection.Negative;
            }
            else
            {
                dirOnAxis[0] = BoxDirection.Mid;
            }
            //scan row
            if (dirThisMax2OtherMin.Y > 0) //other is to the positive side of row
            {
                dirOnAxis[1] = BoxDirection.Positive;
            }
            else if (dirThisMin2OtherMax.Y < 0)
            {
                dirOnAxis[1] = BoxDirection.Negative;
            }
            else
            {
                dirOnAxis[1] = BoxDirection.Mid;
            }
            //scan Layer
            if (dirThisMax2OtherMin.Z > 0) //other is to the positive side of row
            {
                dirOnAxis[2] = BoxDirection.Positive;
            }
            else if (dirThisMin2OtherMax.Z < 0)
            {
                dirOnAxis[2] = BoxDirection.Negative;
            }
            else
            {
                dirOnAxis[2] = BoxDirection.Mid;
            }
            //calculate distance 2D
            if (dirOnAxis[0] == BoxDirection.Positive)
            {
                if (dirOnAxis[1] == BoxDirection.Positive)//NE
                    return dirThisMax2OtherMin.GetHorizontalLen();
                else if (dirOnAxis[1] == BoxDirection.Negative)//SE
                    return new Vec3(dirThisMax2OtherMin.X, dirThisMin2OtherMax.Y, 0).GetHorizontalLen();
                else//E
                    return dirThisMax2OtherMin.X;
            }
            else if (dirOnAxis[0] == BoxDirection.Negative)
            {
                if (dirOnAxis[1] == BoxDirection.Negative)//SW
                    return dirThisMin2OtherMax.GetHorizontalLen();
                else if (dirOnAxis[1] == BoxDirection.Positive)//NW
                    return new Vec3(dirThisMin2OtherMax.X, dirThisMax2OtherMin.Y, 0).GetHorizontalLen();
                else//E
                    return -dirThisMin2OtherMax.X;
            }
            else //col overlap
            {
                if (dirOnAxis[1] == BoxDirection.Positive)//North
                    return dirThisMax2OtherMin.Y;
                else//South
                    return -dirThisMin2OtherMax.Y;
            }

        }
       
    }
    public enum BoxDirection
    {
        Negative=0,//Bounding box is to the negative direction of the axis
        Mid=1,//Bounding box intersects with current axis
        Positive=2,//bounding box is to the right direction of the axis
    }
    public class HazardAABB
    {
        
        public HazardBVHNodeBoundary Box { get; set; }
        public HazardAABBElementInfo Host { get; set; }
        public Vec3 Min
        {
            get
            {
                return Box.Min;
            }
        }
        public Vec3 Max
        {
            get
            {
                return Box.Max;
            }
        }

        public HazardAABB(Vec3 min, Vec3 max, HazardAABBElementInfo elem)
        {
            this.Host = elem;
            this.Box=new HazardBVHNodeBoundary(min,max);
        }
        public Vec3 Center { get; set;  }
        public bool VerticalNearFire { get; internal set; } = false;

        public void CalculateCenter()
        {
            if(this.Center == null)
            {
                this.Center = new Vec3((Min.X + Max.X) / 2, (Min.Y + Max.Y) / 2, (Min.Z + Max.Z) / 2);
            }
        }
    }
    public class HazardVoxelBox
    {
        
        public HazardBoxElementInfo Host { get; set; }
        public CellIndex3D Min { get; }
        
        public CellIndex3D Max { get; }
       

        public HazardVoxelBox(CellIndex3D min, CellIndex3D max, HazardBoxElementInfo elem)
        {
            this.Host = elem;
            this.Min = min;
            this.Max = max;
        }
       
        public bool VerticalNearFire { get; internal set; } = false;
    }


}
