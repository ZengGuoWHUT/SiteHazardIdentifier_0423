using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using RevitVoxelzation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Document = Autodesk.Revit.DB.Document;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
namespace SiteHazardIdentifier
{

    public class RevitMeshExporter : IExportContext
    {
        private Dictionary<Document, int> documents = new Dictionary<Document, int>();
        private Stack<Document> stkDocs = new Stack<Document>();
        private Stack<Element> stkElements = new Stack<Element>();
        private Stack<Autodesk.Revit.DB.Transform> stkTransforms = new Stack<Autodesk.Revit.DB.Transform>();
        private string error = "";
        private StringBuilder data;
        public StreamWriter sw;
        public RevitMeshExporter(Dictionary<Document, int> docs, string meshSavePath)
        {
            FileStream fs = new FileStream(meshSavePath, FileMode.Create, FileAccess.Write);
            sw = new StreamWriter(fs, Encoding.Default, 1024 * 1024);
            this.documents = docs;
            stkDocs.Push(documents.Keys.FirstOrDefault());
            stkTransforms.Push(Autodesk.Revit.DB.Transform.Identity);
        }

        public void Finish()
        {
            sw.Flush();
            sw.Close();
            //throw new NotImplementedException();
        }

        public bool IsCanceled()
        {
            return (error != "");
            //throw new NotImplementedException();
        }

        public RenderNodeAction OnElementBegin(ElementId elementId)
        {
            sw.WriteLine($"{documents[stkDocs.Peek()]}${elementId.Value}");
            return RenderNodeAction.Proceed;
            //throw new NotImplementedException();
        }

        public void OnElementEnd(ElementId elementId)
        {
            sw.WriteLine();
            //throw new NotImplementedException();
        }

        public RenderNodeAction OnFaceBegin(FaceNode node)
        {
            return RenderNodeAction.Proceed;
            //throw new NotImplementedException();
        }

        public void OnFaceEnd(FaceNode node)
        {
            // throw new NotImplementedException();
        }

        public RenderNodeAction OnInstanceBegin(InstanceNode node)
        {
            var transform = node.GetTransform();
            stkTransforms.Push(stkTransforms.Peek().Multiply(transform));
            return RenderNodeAction.Proceed;
            //throw new NotImplementedException();
        }

        public void OnInstanceEnd(InstanceNode node)
        {
            stkTransforms.Pop();
            //throw new NotImplementedException();
        }

        public void OnLight(LightNode node)
        {
            //throw new NotImplementedException();
        }

        public RenderNodeAction OnLinkBegin(LinkNode node)
        {
            var doc = node.GetDocument();
            stkDocs.Push(doc);
            var docTransform = node.GetTransform();
            stkTransforms.Push(stkTransforms.Peek().Multiply(docTransform));
            return RenderNodeAction.Proceed;
            //throw new NotImplementedException();
        }

        public void OnLinkEnd(LinkNode node)
        {
            stkDocs.Pop();
            stkTransforms.Pop();
            //throw new NotImplementedException();
        }

        public void OnMaterial(MaterialNode node)
        {
            //throw new NotImplementedException();
        }

        public void OnPolymesh(PolymeshTopology node)
        {
            StringBuilder data = new StringBuilder();
            var triangles = node.GetFacets();
            var pts = node.GetPoints();
            var strPts = new string[pts.Count()];
            for (int i = 0; i < pts.Count; i++)
            {
                var ptsAfterTransform = stkTransforms.Peek().OfPoint(pts[i]);
                strPts[i] = (Point2StringMM(ptsAfterTransform));
            }
            data.Append(string.Join(",", strPts));
            data.Append(";");
            var strTris = new string[triangles.Count()];
            for (int i = 0; i < triangles.Count; i++)
            {
                var tri = triangles[i];
                strTris[i] = this.TrianglesStringMM(tri.V1, tri.V2, tri.V3);

            }
            data.Append(string.Join(",", strTris));
            sw.WriteLine(data);
        }

        public void OnRPC(RPCNode node)
        {

        }

        public RenderNodeAction OnViewBegin(ViewNode node)
        {
            return RenderNodeAction.Proceed;
        }

        public void OnViewEnd(ElementId elementId)
        {

        }

        public bool Start()
        {
            return true;
        }
#if Revit2016
     public void OnDaylightPortal(DaylightPortalNode Node)
     {

     }
#endif
        private string Point2StringMM(XYZ point)
        {
            var x = Convert.ToInt32((double)point.X * 304.8);
            var y = Convert.ToInt32((double)point.Y * 304.8);
            var z = Convert.ToInt32((double)point.Z * 304.8);
            return $"{x},{y},{z}";
        }
        private string TrianglesStringMM(int v0, int v1, int v2)
        {

            return $"{v0},{v1},{v2}";
        }
    }
    public class RevitMaterialExporter : IExportContext
    {

        private string savePath = "";
        private string matElemPath = "";
        private string errMessage = "";
        private Stack<Document> docStack = new Stack<Document>();
        private Stack<Element> elementStack = new Stack<Element>();
        public Dictionary<string, string> MaterialName { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> ElementId_MaterialId { get; set; } = new Dictionary<string, string>();

        public List<Document> Documents { get; set; }
        public RevitMaterialExporter(Document doc, List<Document> documents, string savePath,string matElempath)
        {
            docStack.Push(doc);
            Documents = documents;
            this.savePath = savePath;
            this.matElemPath = matElempath;
        }
        public void Finish()
        {
            if (errMessage != "")
            {
                TaskDialog.Show("Revit", errMessage);
            }
            else
            {
                string folder = Path.GetDirectoryName(savePath);
                string strElemData = matElemPath;
                using (var sw = new StreamWriter(savePath, false, Encoding.Default, 1024))
                {
                    sw.WriteLine("Material Id,Material Names,Combustible(Yes/No),Reason (Less than 50 words.Use semicolon to replace commas)");
                    int mPointer = 0;
                    foreach (var matName in MaterialName)
                    {
                        sw.WriteLine($"{matName.Value},{matName.Key},{string.Empty},{string.Empty}");
                        mPointer += 1;
                    }
                    sw.Flush();
                }
                using (var sw = new StreamWriter(strElemData, false, Encoding.Default, 1024))
                {
                    sw.WriteLine("Element Id,Element Internal Id");
                    int mPointer = 0;
                    foreach (var elemInfo in ElementId_MaterialId)
                    {
                        sw.WriteLine($"{elemInfo.Key},{elemInfo.Value}");
                        mPointer += 1;
                    }
                    sw.Flush();
                }
                //Process.Start("explorer.exe", $"/select,\"{savePath}\"");
            }
            //throw new NotImplementedException();
        }

        public bool IsCanceled()
        {
            return errMessage != "";
            //throw new NotImplementedException();
        }

        public RenderNodeAction OnElementBegin(ElementId elementId)
        {
            try
            {
                var elem = docStack.Peek().GetElement(elementId);
                elementStack.Push(elem);
                return RenderNodeAction.Proceed;
            }
            catch (Exception ex)
            {
                errMessage = ex.Message + ex.StackTrace;
                return RenderNodeAction.Skip;
            }
            //throw new NotImplementedException();
        }

        public void OnElementEnd(ElementId elementId)
        {
            try
            {
                var curDoc = docStack.Peek();
                var docIdx = this.Documents.IndexOf(curDoc);
                var elem = elementStack.Pop();
                var InternalElemId = $"{docIdx}${elem.Id.ToString()}";
                var matIds0 = elem.GetMaterialIds(false);
                var matIds1 = elem.GetMaterialIds(true);
                var matIds = matIds0.Union(matIds1);
                
                HashSet<string> InternalMatIds = new HashSet<string>();
                if(matIds.Count() !=0)
                {
                    foreach (var matId in matIds)
                    {
                        Material mat = curDoc.GetElement(matId) as Material;

                        if (mat != null)
                        {
                            string name = mat.Name.Replace(",", "-");
                            string materialId = $"M{MaterialName.Count}";
                            if (!this.MaterialName.ContainsKey(name))
                            {
                                this.MaterialName.Add(name, materialId);
                            }
                            else
                            {
                                materialId = this.MaterialName[name];
                            }
                            InternalMatIds.Add(materialId);
                        }

                    }
                }
                else //no material attatched 
                {
                    string name = "NoMaterial";
                    string materialId = $"M{MaterialName.Count}";
                    if (!this.MaterialName.ContainsKey(name))
                    {
                        this.MaterialName.Add(name, materialId);
                    }
                    else
                    {
                        materialId = this.MaterialName[name];
                    }
                    InternalMatIds.Add(materialId);
                }
                if (InternalMatIds.Count > 0)
                {
                    var lstMatIds = InternalMatIds.ToList();
                    lstMatIds.Sort();
                    string strMatIds = string.Join("_", lstMatIds);
                    if (!this.ElementId_MaterialId.ContainsKey(InternalElemId))
                    {
                        ElementId_MaterialId.Add(InternalElemId, strMatIds);
                    }
                }
            }
            catch (Exception ex)
            {
                errMessage = ex.Message + ex.StackTrace;
            }

            //throw new NotImplementedException();
        }

        public RenderNodeAction OnFaceBegin(FaceNode node)
        {
            return RenderNodeAction.Skip;
        }

        public void OnFaceEnd(FaceNode node)
        {
            //throw new NotImplementedException();
        }

        public RenderNodeAction OnInstanceBegin(InstanceNode node)
        {
            return RenderNodeAction.Proceed;
        }

        public void OnInstanceEnd(InstanceNode node)
        {

        }

        public void OnLight(LightNode node)
        {

        }

        public RenderNodeAction OnLinkBegin(LinkNode node)
        {
            Document doc = node.GetDocument();
            this.docStack.Push(doc);
            return RenderNodeAction.Proceed;
        }

        public void OnLinkEnd(LinkNode node)
        {
            this.docStack.Pop();
            //throw new NotImplementedException();
        }

        public void OnMaterial(MaterialNode node)
        {
            //throw new NotImplementedException();
        }

        public void OnPolymesh(PolymeshTopology node)
        {
            //throw new NotImplementedException();
        }

        public void OnRPC(RPCNode node)
        {
            //throw new NotImplementedException();
        }

        public RenderNodeAction OnViewBegin(ViewNode node)
        {
            return RenderNodeAction.Proceed;
            //throw new NotImplementedException();
        }

        public void OnViewEnd(ElementId elementId)
        {
            //throw new NotImplementedException();
        }

        public bool Start()
        {
            return this.savePath != string.Empty;
            //throw new NotImplementedException();
        }

#if Revit2016
     public void OnDaylightPortal(DaylightPortalNode Node)
     {

     }
#endif
    }
    public class RevitMeshDocumenetConverter
    {
        public Vec3 Origin { get; set; }
        public double VoxelSize { get; set; }
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
                                if(curElem.Solids.Count== 0)
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

        public IEnumerable<VoxelElement> Voxelize(string meshPath, string savePath)
        {
            var voxDoc = new VoxelDocument();
            voxDoc.Origin = this.Origin;
            voxDoc.VoxelSize = this.VoxelSize;

            FileStream fs = new FileStream(savePath, FileMode.Create);
            using (StreamWriter sw = new StreamWriter(fs, Encoding.Default, 1024 * 1024))
            {
                sw.WriteLine(Origin.ToString());
                sw.WriteLine(VoxelSize.ToString());
                //var elems = CreateMeshElement(meshPath).ToList();
                foreach (var elem in CreateMeshElement(meshPath))
                {
                    VoxelElement ve = new VoxelElement(voxDoc, elem, true, 1e-2);
                    yield return ve;
                    sw.WriteLine(ve.ElementId);
                    int[] strVoxelData = new int[ve.Voxels.Count * 4];
                    for (int i = 0; i < ve.Voxels.Count; i++)
                    {
                        var vox = ve.Voxels[i];
                        var pointer = i * 4;
                        strVoxelData[pointer] = vox.ColIndex;
                        strVoxelData[pointer + 1] = vox.RowIndex;
                        strVoxelData[pointer + 2] = (int)vox.BottomElevation;
                        if((int)vox.BottomElevation<-1000000)
                        {

                        }
                        strVoxelData[pointer + 3] = (int)vox.TopElevation;
                    }
                    sw.WriteLine(string.Join(",", strVoxelData));
                }
                sw.Flush();
                sw.Close();
            }
        }

        public double timeGenGridPts;
        public double timeGenVoxels;
        public double timeMergeVoxels;
        public double timeFillVoxels;
        public IEnumerable<VoxelElement> VoxelizeReportTime(string meshPath, string savePath)
        {
            var voxDoc = new VoxelDocument();
            voxDoc.Origin = this.Origin;
            voxDoc.VoxelSize = this.VoxelSize;

            FileStream fs = new FileStream(savePath, FileMode.Create);
            using (StreamWriter sw = new StreamWriter(fs, Encoding.Default, 1024 * 1024))
            {
                sw.WriteLine(Origin.ToString());
                sw.WriteLine(VoxelSize.ToString());
                //var elems = CreateMeshElement(meshPath).ToList();
                foreach (var elem in CreateMeshElement(meshPath))
                {
                    VoxelElement ve = new VoxelElement(voxDoc, elem, true, 1e-2,true);
                    timeGenGridPts += ve.timeGridPtGen;
                    timeFillVoxels += ve.timeVoxFill;
                    timeMergeVoxels += ve.timeVoxMerge;
                    timeGenVoxels += ve.timeVoxGen;
                    yield return ve;
                    sw.WriteLine(ve.ElementId);
                    int[] strVoxelData = new int[ve.Voxels.Count * 4];
                    for (int i = 0; i < ve.Voxels.Count; i++)
                    {
                        var vox = ve.Voxels[i];
                        var pointer = i * 4;
                        strVoxelData[pointer] = vox.ColIndex;
                        strVoxelData[pointer + 1] = vox.RowIndex;
                        strVoxelData[pointer + 2] = (int)vox.BottomElevation;
                        strVoxelData[pointer + 3] = (int)vox.TopElevation;
                    }
                    sw.WriteLine(string.Join(",", strVoxelData));
                }
                sw.Flush();
                sw.Close();
            }
        }

        public IEnumerable<VoxelElement> Voxelize(List<MeshElement> meshElems, string savePath)
        {
            var voxDoc = new VoxelDocument();
            voxDoc.Origin = this.Origin;
            voxDoc.VoxelSize = this.VoxelSize;

            FileStream fs = new FileStream(savePath, FileMode.Create);
            using (StreamWriter sw = new StreamWriter(fs, Encoding.Default, 1024 * 1024))
            {
                sw.WriteLine(Origin.ToString());
                sw.WriteLine(VoxelSize.ToString());
                //var elems = CreateMeshElement(meshPath).ToList();
                foreach (var elem in meshElems)
                {
                    VoxelElement ve = new VoxelElement(voxDoc, elem, true, 1e-2);
                    yield return ve;
                    sw.WriteLine(ve.ElementId);
                    int[] strVoxelData = new int[ve.Voxels.Count * 4];
                    for (int i = 0; i < ve.Voxels.Count; i++)
                    {
                        var vox = ve.Voxels[i];
                        var pointer = i * 4;
                        strVoxelData[pointer] = vox.ColIndex;
                        strVoxelData[pointer + 1] = vox.RowIndex;
                        strVoxelData[pointer + 2] = (int)vox.BottomElevation;
                        strVoxelData[pointer + 3] = (int)vox.TopElevation;
                    }
                    sw.WriteLine(string.Join(",", strVoxelData));
                }
                sw.Flush();
                sw.Close();
            }
        }

        public IEnumerable<VoxelElement> LoadVoxelizedElements(string path)
        {
            using(var sr=new StreamReader(path))
            {
                //Read Origin
                var origin = sr.ReadLine();
                string[] strOrigin=origin.Split(',');
                double dblX=Double.Parse(strOrigin[0]);
                double dblY=Double.Parse(strOrigin[1]);
                double dblZ=Double.Parse(strOrigin[2]);
                XYZ o=new XYZ(dblX, dblY, dblZ);
                var step = double.Parse(sr.ReadLine()) ;
               
                this.Origin = new Vec3(dblX,dblY ,dblZ)   ;
                this.VoxelSize= step;
                //load voxel info
                while (!sr.EndOfStream)
                {
                    string elemId = sr.ReadLine();
                    string[] strVoxelData = sr.ReadLine().Split(',');
                    VoxelElement ve = new VoxelElement() { ElementId = elemId };
                    ve.Voxels = new List<Voxel>();
                    for (int i = 0; i < strVoxelData.Length; i += 4)
                    {
                        var st = i;
                        var col = int.Parse(strVoxelData[st]);
                        var row = int.Parse(strVoxelData[st + 1]);
                        var btmElemv = double.Parse(strVoxelData[st + 2]);
                        var topElev = double.Parse(strVoxelData[st + 3]);
                        Voxel vox = new Voxel() { ColIndex = col, RowIndex = row, BottomElevation = btmElemv, TopElevation = topElev };
                        ve.Voxels.Add(vox);
                    }
                    yield return ve;
                    
                }
                sr.Close();

                //Read VoxelSize
            }
        }

        public IEnumerable<LightWeightVoxelElement> LoadBoxElements(string path)
        {
            using (var sr = new StreamReader(path))
            {
                //Read Origin
                var origin = sr.ReadLine();
                string[] strOrigin = origin.Split(',');
                double dblX = Double.Parse(strOrigin[0]);
                double dblY = Double.Parse(strOrigin[1]);
                double dblZ = Double.Parse(strOrigin[2]);
                XYZ o = new XYZ(dblX, dblY, dblZ);
                var step = double.Parse(sr.ReadLine());

                this.Origin = new Vec3(dblX, dblY, dblZ);
                this.VoxelSize = step;
                //load voxel info
                while (!sr.EndOfStream)
                {
                    string elemId = sr.ReadLine();
                    string[] strBoxData = sr.ReadLine().Split(',');
                    LightWeightVoxelElement ve = new LightWeightVoxelElement() { ElementId = elemId };
                    ve.Boxes = new List<VoxelBox>();
                    for (int i = 0; i < strBoxData.Length; i += 6)
                    {
                        var st = i;
                        var colMin = int.Parse(strBoxData[st]);
                        var rowMin = int.Parse(strBoxData[st + 1]);
                        var btmElemv = int.Parse(strBoxData[st + 2]);
                        var colMax = int.Parse(strBoxData[st + 3]);
                        var rowMax = int.Parse(strBoxData[st + 4]);
                        var topElev = int.Parse(strBoxData[st + 5]);
                        var box = new VoxelBox(new CellIndex3D(colMin, rowMin, btmElemv), new CellIndex3D(colMax, rowMax, topElev));
                        ve.Boxes.Add(box);
                    }
                    yield return ve;

                }
                sr.Close();

                //Read VoxelSize
            }
        }

        public IEnumerable<VoxelElement> LoadBoxElementsAndConvertVoxel(string path)
        {
            using (var sr = new StreamReader(path))
            {
                //Read Origin
                var origin = sr.ReadLine();
                string[] strOrigin = origin.Split(',');
                double dblX = Double.Parse(strOrigin[0]);
                double dblY = Double.Parse(strOrigin[1]);
                double dblZ = Double.Parse(strOrigin[2]);
                XYZ o = new XYZ(dblX, dblY, dblZ);
                var step = double.Parse(sr.ReadLine());

                this.Origin = new Vec3(dblX, dblY, dblZ);
                this.VoxelSize = step;
                //load voxel info
                while (!sr.EndOfStream)
                {
                    string elemId = sr.ReadLine();
                    string[] strBoxData = sr.ReadLine().Split(',');
                    LightWeightVoxelElement ve = new LightWeightVoxelElement() { ElementId = elemId };
                    ve.Boxes = new List<VoxelBox>();
                    for (int i = 0; i < strBoxData.Length; i += 6)
                    {
                        var st = i;
                        var colMin = int.Parse(strBoxData[st]);
                        var rowMin = int.Parse(strBoxData[st + 1]);
                        var btmElemv = int.Parse(strBoxData[st + 2]);
                        var colMax = int.Parse(strBoxData[st + 3]);
                        var rowMax = int.Parse(strBoxData[st + 4]);
                        var topElev = int.Parse(strBoxData[st + 5]);
                        var box = new VoxelBox(new CellIndex3D(colMin, rowMin, btmElemv), new CellIndex3D(colMax, rowMax, topElev));
                        ve.Boxes.Add(box);
                    }
                    yield return ve.Convert2VoxelElement((int)step);

                }
                sr.Close();

                //Read VoxelSize
            }
        }




    }
}
