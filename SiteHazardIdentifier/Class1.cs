using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ICSharpCode.SharpZipLib.Zip;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;

using RevitVoxelzation;
using RevitVoxelzation;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Shapes;
using Document = Autodesk.Revit.DB.Document;
using Line = Autodesk.Revit.DB.Line;
using Path = System.IO.Path;
using Polygon = NetTopologySuite.Geometries.Polygon;
namespace SiteHazardIdentifier
{
    [Transaction(TransactionMode.Manual)]
    public class IdentifyHazardRisk : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            DependencyResolver.Initialize();
            var doc = commandData.Application.ActiveUIDocument.Document;
            var view = doc.ActiveView;
            FrmFireHazard frm = new FrmFireHazard();
            frm.identifier = new RiskIdentifier(new List<VoxelElement>(), new List<Work>(), 200);
            ExternalEvent exportMeshes = ExternalEvent.Create(new VoxelizeHandler(frm));
            ExternalEvent loadVoxel = ExternalEvent.Create(new LoadVoxelHandler(frm));
            ExternalEvent generateChunk = ExternalEvent.Create(new VisualizeChunk(frm));
            ExternalEvent visualizeBox = ExternalEvent.Create(new VisualizeBox(frm));

            frm.GetMeshes = exportMeshes;
            frm.LoadVoxels = loadVoxel;
            frm.VisualizeBox = visualizeBox;
            frm.GenerateVoxels = generateChunk;
            frm.Documents = new List<Document>();
            foreach (Document d in commandData.Application.Application.Documents)
            {
                frm.Documents.Add(d);
            }


            frm.Show();
            return Result.Succeeded;
        }
    }
    public class VisualizeBox : IExternalEventHandler
    {
        private FrmFireHazard frm;
        public VisualizeBox(FrmFireHazard frm)
        {
            this.frm = frm;
        }
        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                var view = doc.ActiveView;
                var solidFill = new FilteredElementCollector(doc).
                    OfClass(typeof(FillPatternElement)).
                    Cast<FillPatternElement>().
                    Where(c => c.GetFillPattern().IsSolidFill).
                    FirstOrDefault();
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Visualize box");
                    double dblVoxSize = frm.Voxelizer.VoxelSize / 304.8;
                    foreach (var ve in frm.LightWeightVoxelElements)
                    {
                        List<Solid> slds = new List<Solid>();
                        foreach (var box in ve.Boxes)
                        {
                            var min = box.Min;
                            var max = box.Max;
                            var zScale = Math.Max(max.Layer - min.Layer, 1) / 304.8;
                            var colScale = (max.Col - min.Col + 1) * dblVoxSize;
                            var rowScale = (max.Row - min.Row + 1) * dblVoxSize;
                            var pt0 = new XYZ(min.Col * dblVoxSize, min.Row * dblVoxSize, min.Layer / 304.8);
                            var pt1 = pt0 + XYZ.BasisX * colScale;
                            var pt2 = pt1 + XYZ.BasisY * rowScale;
                            var pt3 = pt2 - XYZ.BasisX * colScale;
                            var pts = new List<XYZ>() { pt0, pt1, pt2, pt3 };
                            var loop = new CurveLoop();
                            var loops = new List<CurveLoop>() { loop };
                            for (int i = 0; i <= 3; i++)
                            {
                                var p0 = pts[i % 4];
                                var p1 = pts[(i + 1) % 4];
                                if ((p1 - p0).GetLength() < 1e-4)
                                {

                                }
                                Line li = Line.CreateBound(p0, p1);
                                loop.Append(li);
                            }
                            var sld = GeometryCreationUtilities.CreateExtrusionGeometry(loops, XYZ.BasisZ, zScale);
                            slds.Add(sld);
                        }
                        DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.AppendShape(slds.ToArray());
                        var setting = view.GetElementOverrides(ds.Id);
                        setting.SetSurfaceTransparency(50);
                        setting.SetProjectionFillPatternId(solidFill.Id);
                        setting.SetProjectionLineColor(new Color(0, 255, 0));
                        view.SetElementOverrides(ds.Id, setting);
                    }
                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
            //throw new NotImplementedException();
        }

        public string GetName()
        {
            return "Visualize Box";
            //throw new NotImplementedException();
        }
    }
    public class VoxelizeHandler : IExternalEventHandler
    {
        private FrmFireHazard frm;
        public VoxelizeHandler(FrmFireHazard frmHazard)
        {
            this.frm = frmHazard;
        }
        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            var view = doc.ActiveView;
            var sel = app.ActiveUIDocument.Selection;
            Dictionary<Document, int> doc_Ids = new Dictionary<Document, int>();
            doc_Ids.Add(doc, 0);
            foreach (Document d in app.Application.Documents)
            {
                if (d.IsLinked && d.IsDetached == false)
                {
                    doc_Ids.Add(d, doc_Ids.Count);
                }
            }
            SaveFileDialog sfg = new SaveFileDialog();
            sfg.Filter = "fire Risk Data file|*.fireRiskData";
            if (sfg.ShowDialog() == DialogResult.OK)
            {
                //Create a folder to save the meshes
                string zipPath = sfg.FileName;
                string tempfilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempfilePath);
                //create the mesh files
                var meshPath = Path.Combine(tempfilePath, Path.GetFileNameWithoutExtension(zipPath) + ".txt");
                //Export mesh
                CustomExporter exporter = new CustomExporter(doc, new RevitMeshExporter(doc_Ids, meshPath));
                exporter.ShouldStopOnError = true;
                exporter.Export(view as View3D);
                //Export material
                var matPath = Path.Combine(tempfilePath, "materials.csv");
                var matElemPath = Path.Combine(tempfilePath, "matElemRel.csv");
                var materialExporter = new CustomExporter(doc, new RevitMaterialExporter(doc, doc_Ids.Keys.ToList(), matPath, matElemPath));
                materialExporter.ShouldStopOnError = true;
                materialExporter.Export(view as View3D);
                RevitMeshDocumenetConverter converter = new RevitMeshDocumenetConverter() { Origin = Vec3.Zero };
                var numElems = RevitMeshDocumenetConverter.CreateMeshElement(meshPath).Count();
                //save path
                var modelMataData = Path.Combine(tempfilePath, "ModelInfo.matadata");
                using (var sw = new StreamWriter(modelMataData, false, Encoding.Default))
                {
                    sw.WriteLine($"ElemNum:{numElems}");
                    sw.Flush();
                    sw.Close();
                }
                //create a zip file
                FastZip fastZip = new FastZip();
                var zipFullName = sfg.FileName;
                //Delete generated file
                fastZip.CreateZip(sfg.FileName, tempfilePath, true, "");
                Directory.Delete(tempfilePath, true);
                frm.initProgress(numElems);
                frm.MeshPath = zipFullName;
                frm.Voxelizer = converter;
                TaskDialog.Show("Revit", "Done!");
            }

        }

        public string GetName()
        {
            return "Voxelize Elements";
        }
    }

    public class VisualizeChunk : IExternalEventHandler
    {

        private FrmFireHazard frm;
        private Dictionary<ElementId, _4DElement> rvtElemExtreme;
        private Dictionary<ElementId, _4DElement> rvtElemHigh;
        private Dictionary<ElementId, _4DElement> rvtElemMedium;
        private Dictionary<ElementId, _4DElement> rvtElemLow;
        public VisualizeChunk(FrmFireHazard frm)
        {
            this.frm = frm;
        }
        public void Execute(UIApplication app)
        {
            try
            {
                app.Idling += App_Idling;

                var view = app.ActiveUIDocument.Document.ActiveView;
                var doc = app.ActiveUIDocument.Document;
                var docs = frm.Documents;
                //find solid fill
                var solidFill = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>()
                    .Where(c => c.GetFillPattern().IsSolidFill).FirstOrDefault();
                var combos = frm.elementChunks;
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("GenVox");
                    var chunkIndex = frm.SelectedIndexes;
                    rvtElemExtreme = new Dictionary<ElementId, _4DElement>();
                    rvtElemHigh = new Dictionary<ElementId, _4DElement>();
                    rvtElemMedium = new Dictionary<ElementId, _4DElement>();
                    rvtElemLow = new Dictionary<ElementId, _4DElement>();
                    //Obtain elements of current work
                    foreach (var kvp in frm.identifier.IternateElements())
                    {
                        var elemId = kvp.ElementId;
                        var comboList = kvp.Combinations;
                        List<string> hazardDescription = new List<string>();
                        CombinationHazardLevel highestLevel = new CombinationHazardLevel();
                        string[] docIdx_elemID = elemId.Split('$');
                        ElementId rvtId = new ElementId(int.Parse(docIdx_elemID[1]));
                        int combIdx = 0;
                        foreach (var combo in comboList)
                        {
                            hazardDescription.Add(combIdx.ToString() + "\r\n" + combo.Get_Description());
                            if (highestLevel < combo.HazardLevel)
                            {
                                highestLevel = combo.HazardLevel;
                            }
                            combIdx += 1;
                        }
                        var elem = frm.identifier.ElemBoxRel[elemId];
                        var elemPhaseString = "Element Phases:\r\n" + string.Join("\r\n", elem.GetElementPhaseString());
                        var comboText = elemPhaseString + "\r\n" + "Fire Hazard Combos:\r\n" + string.Join("\r\n", hazardDescription);
                        if (doc.GetElement(rvtId) != null)
                        {
                            switch (highestLevel)
                            {
                                case CombinationHazardLevel.Extreme:
                                    rvtElemExtreme.Add(rvtId, elem);
                                    break;
                                case CombinationHazardLevel.High:
                                    rvtElemHigh.Add(rvtId, elem);
                                    break;
                                case CombinationHazardLevel.Medium:
                                    rvtElemMedium.Add(rvtId, elem);
                                    break;
                                case CombinationHazardLevel.Low:
                                    rvtElemLow.Add(rvtId, elem);
                                    break;
                            }
                        }
                    }
                    var elem2Isolate = new List<ElementId>();
                    //modify scene
                    foreach (var id_Description in rvtElemExtreme)
                    {
                        var id = id_Description.Key;
                        elem2Isolate.Add(id);

                        var setting = view.GetElementOverrides(id);
                        setting.SetSurfaceTransparency(10);
                        setting.SetProjectionFillPatternId(solidFill.Id);
                        setting.SetProjectionFillColor(new Color(255, 0, 0));
                        view.SetElementOverrides(id, setting);
                        var elem = doc.GetElement(id);
                    }
                    //Group gpBase = doc.Create.NewGroup(rvtElemIdBase);
                    foreach (var id_Description in rvtElemHigh)
                    {
                        var id = id_Description.Key;
                        elem2Isolate.Add(id);
                        var setting = view.GetElementOverrides(id);
                        setting.SetSurfaceTransparency(10);
                        setting.SetProjectionFillPatternId(solidFill.Id);
                        setting.SetProjectionFillColor(new Color(255, 255, 0));
                        view.SetElementOverrides(id, setting);
                        var elem = doc.GetElement(id);

                    }
                    foreach (var id_Description in rvtElemMedium)
                    {
                        var id = id_Description.Key;
                        elem2Isolate.Add(id);

                        var setting = view.GetElementOverrides(id);
                        setting.SetSurfaceTransparency(10);
                        setting.SetProjectionFillPatternId(solidFill.Id);
                        setting.SetProjectionFillColor(new Color(0, 0, 255));
                        view.SetElementOverrides(id, setting);
                        var elem = doc.GetElement(id);

                    }
                    foreach (var id_Description in rvtElemLow)
                    {
                        var id = id_Description.Key;
                        elem2Isolate.Add(id);

                        var setting = view.GetElementOverrides(id);
                        setting.SetSurfaceTransparency(10);
                        setting.SetProjectionFillPatternId(solidFill.Id);
                        setting.SetProjectionFillColor(new Color(0, 255, 0));
                        view.SetElementOverrides(id, setting);
                        var elem = doc.GetElement(id);

                    }
                    //Group gpAff = doc.Create.NewGroup(rvtElemIdAff);
                    //view.IsolateElementsTemporary(elem2Isolate);
                    //view.IsolateElementsTemporary(new ElementId[3] {gpBase.Id,gpAff.Id,dsVoxel.Id });
                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
            //throw new NotImplementedException();
        }
        private ElementId previousSelElementIds = null;
        private void App_Idling(object sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {
            var uiApp = sender as UIApplication;
            if (frm.IsDisposed)
            {
                uiApp.Idling -= App_Idling;
                MessageBox.Show("Idling remvoved");
                previousSelElementIds = null;
                return;
            }
            var sel = uiApp.ActiveUIDocument.Selection;
            if (sel != null)
            {
                var elemId = sel.GetElementIds().FirstOrDefault();
                if (elemId != null && previousSelElementIds != elemId)
                {
                    _4DElement elemSel = null;
                    if (rvtElemExtreme.ContainsKey(elemId))
                    {
                        elemSel = rvtElemExtreme[elemId];

                    }
                    else if (rvtElemHigh.ContainsKey(elemId))
                    {
                        elemSel = rvtElemHigh[elemId];
                    }
                    else if (rvtElemMedium.ContainsKey(elemId))
                    {
                        elemSel = rvtElemMedium[elemId];
                    }
                    else if (rvtElemLow.ContainsKey(elemId))
                    {
                        elemSel = rvtElemLow[elemId];
                    }
                    var frm = new FrmElemHazardInfo(elemSel);
                    frm.ShowDialog();
                    previousSelElementIds = elemId;
                }
            }
            e.SetRaiseWithoutDelay();
            //throw new NotImplementedException();
        }

        public string GetName()
        {
            return "GenVoxelize";
            //throw new NotImplementedException();
        }
    }

    public class LoadVoxelHandler : IExternalEventHandler
    {
        private FrmFireHazard frm;
        public LoadVoxelHandler(FrmFireHazard frmHazard)
        {
            this.frm = frmHazard;

        }

        public void Execute(UIApplication app)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "text files|*.txt";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (frm.Voxelizer == null)
                    {
                        frm.Voxelizer = new RevitMeshDocumenetConverter();
                    }
                    var voxelElems = frm.Voxelizer.LoadVoxelizedElements(ofd.FileName).ToList();
                    frm.identifier.VoxelSize = frm.Voxelizer.VoxelSize;
                    //frm.identifier = new RiskIdentifier(voxelElems, new List<Work>(), frm.Voxelizer.VoxelSize, string.Empty, string.Empty, string.Empty, string.Empty);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message + ex.StackTrace);
                }
            }
            //throw new NotImplementedException();
        }

        public string GetName()
        {
            return "Load voxels";
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class GetOuterLoop : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            var view = doc.ActiveView;
            //this.LoadVoxels.Raise();
            OpenFileDialog opendiag = new OpenFileDialog();
            opendiag.Filter = "Mesh file|*.fireRiskData";
            if (opendiag.ShowDialog() == DialogResult.OK && opendiag.FileName != string.Empty)
            {
                var MeshPath = opendiag.FileName;
                //unpack
                FastZip fastZip = new FastZip();
                // 生成临时文件夹路径
                string tempDirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                fastZip.ExtractZip(MeshPath, tempDirPath, null);
                fastZip = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();

                //Obtain file path
                string strMesh = null;
                string strMaterial = null;
                string strElemMatRel = null;
                foreach (var file in Directory.GetFiles(tempDirPath))
                {
                    if (Path.GetExtension(file) == ".txt")//mesh
                    {
                        strMesh = file;
                    }
                    else if (Path.GetFileName(file) == "materials.csv")//mat
                    {
                        strMaterial = file;
                    }
                    else if (Path.GetFileName(file) == "matElemRel.csv")//elemData
                    {
                        strElemMatRel = file;
                    }
                    else if (Path.GetExtension(file) == ".matadata")//modeel info
                    {
                        using (StreamReader sr = new StreamReader(file))
                        {
                            var modeldata = sr.ReadToEnd();
                            int numElems = int.Parse(modeldata.Split(':')[1]);
                            sr.Close();
                        }
                    }
                    else
                    {
                        throw new Exception("Reading file error");
                    }
                }
                Dictionary<string, List<List<(double x, double Y)>>> elem_Loops = new Dictionary<string, List<List<(double x, double Y)>>>();
                foreach (var mesh in RevitMeshDocumenetConverter.CreateMeshElement(strMesh))
                {

                    List<List<(double x, double y)>> loops = new List<List<(double x, double y)>>();
                    elem_Loops.Add(mesh.ElementId, new List<List<(double x, double Y)>>());
                    var elemLoops = elem_Loops[mesh.ElementId];
                    foreach (var solid in mesh.Solids)
                    {
                        List<int> tris = new List<int>();
                        foreach (var tri in solid.Triangles)
                        {
                            tris.AddRange(tri.VerticesIndex);
                        }

                        List<List<(double X, double Y)>> lps = MeshProjector.GetProjectionBoundaryRings(solid.Vertices, tris.ToArray());
                        elemLoops.AddRange(lps);
                    }

                }
                if (MessageBox.Show("Visualized result?", "Revit", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    using (Transaction t = new Transaction(doc))
                    {
                        t.Start("draw polygon");
                        foreach (var kvp in elem_Loops)
                        {
                            WireframeBuilder wb = new WireframeBuilder();

                            foreach (var loop in kvp.Value)
                            {
                                var ptCount = loop.Count;
                                for (int i = 0; i <= ptCount - 1; i++)
                                {
                                    var p0 = loop[i % ptCount];
                                    var p1 = loop[(i + 1) % ptCount];
                                    XYZ pt0 = new XYZ(p0.x / 304.8, p0.Y / 304.8, 0);
                                    XYZ pt1 = new XYZ(p1.x / 304.8, p1.Y / 304.8, 0);
                                    try
                                    {
                                        Line li = Line.CreateBound(pt0, pt1);
                                        wb.AddCurve(li);
                                    }
                                    catch (Exception ex)
                                    {
                                        continue;
                                    }
                                }

                            }
                            DirectShape ds = null;
#if Revit2016
    ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel),new Guid().ToString(),new Guid().ToString());
#else
                            ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
#endif                            
                            ds.AppendShape(wb);
                        }


                        t.Commit();
                    }
                }
                Directory.Delete(tempDirPath, true);
            }

            return Result.Succeeded;
            //throw new NotImplementedException();
        }
    }

    public class MeshProjector
    {
        public static List<List<(double X, double Y)>> GetProjectionBoundaryRings(
            List<Vec3> vertices, int[] triangles)
        {
            var geometryFactory = new GeometryFactory();
            var trianglePolygons = new List<Polygon>();

            // 1. 构建每个三角形的投影多边形
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i1 = triangles[i];
                int i2 = triangles[i + 1];
                int i3 = triangles[i + 2];

                var v1 = vertices[i1];
                var v2 = vertices[i2];
                var v3 = vertices[i3];

                // 创建二维坐标点（忽略 Z）
                var coord1 = new Coordinate(v1.X, v1.Y);
                var coord2 = new Coordinate(v2.X, v2.Y);
                var coord3 = new Coordinate(v3.X, v3.Y);

                // 检查三点是否共线（退化三角形）
                if (IsCollinear(coord1, coord2, coord3))
                    continue;

                // 构建闭合环：需要首尾相同，所以用四个点
                var shellCoords = new[] { coord1, coord2, coord3, coord1 };
                var shell = geometryFactory.CreateLinearRing(shellCoords);
                var poly = geometryFactory.CreatePolygon(shell);
                trianglePolygons.Add(poly);
            }

            if (trianglePolygons.Count == 0)
                return new List<List<(double, double)>>();

            // 2. 合并所有三角形多边形（使用级联合并，提高性能）
            var geometries = trianglePolygons.Cast<Geometry>().ToList(); // 关键修正
            var unionGeom = new CascadedPolygonUnion(geometries).Union();

            // 3. 提取所有多边形的环
            var rings = new List<List<(double X, double Y)>>();

            if (unionGeom is Polygon polygon)
            {
                ExtractRingsFromPolygon(polygon, rings);
            }
            else if (unionGeom is MultiPolygon multiPolygon)
            {
                for (int i = 0; i < multiPolygon.NumGeometries; i++)
                {
                    ExtractRingsFromPolygon((Polygon)multiPolygon.GetGeometryN(i), rings);
                }
            }

            return rings;
        }

        private static bool IsCollinear(Coordinate a, Coordinate b, Coordinate c)
        {
            // 计算向量叉积判断三点是否共线
            double area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            return Math.Abs(area) < 1e-9; // 阈值可根据需要调整
        }

        private static void ExtractRingsFromPolygon(Polygon polygon, List<List<(double X, double Y)>> rings)
        {
            // 外环
            var exteriorRing = polygon.ExteriorRing;
            rings.Add(CoordinatesToList(exteriorRing.Coordinates));

            // 内环（孔洞）
            for (int i = 0; i < polygon.NumInteriorRings; i++)
            {
                var interiorRing = polygon.GetInteriorRingN(i);
                rings.Add(CoordinatesToList(interiorRing.Coordinates));
            }
        }

        private static List<(double X, double Y)> CoordinatesToList(Coordinate[] coords)
        {
            // 注意：最后一个坐标与第一个重复，通常需要去除重复点，但保留闭合性
            // 这里根据实际需求决定是否去除最后一个点。以下去除重复尾点。
            var list = new List<(double X, double Y)>();
            for (int i = 0; i < coords.Length - 1; i++) // 跳过最后一个（与第一个相同）
            {
                list.Add((coords[i].X, coords[i].Y));
            }
            return list;
        }
    }

    // 假设的 Vec3 类型（请替换为实际类型）





    [Transaction(TransactionMode.Manual)]
    public class AttatchElemIds2Works : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var app = uiApp.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc.Document;
            var view = doc.ActiveView as View3D;
            List<FamilySymbol> symbols = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().Where(c => c.Family.Name == "TestBall").ToList();
            foreach (var symbol in symbols)
            {
                symbol.Activate();
            }
            List<ElementId> testBallIds = new List<ElementId>();
            //generate 100 elements
            //if (MessageBox.Show("Refresh model?","Caution",MessageBoxButtons.YesNo)==DialogResult.Yes)
            {
                Random rand = new Random();

                using (var t = new Transaction(doc))
                {
                    t.Start("delete elements");
                    List<FamilyInstance> previousElems = new FilteredElementCollector(doc).
                        OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().Where(c => c.Symbol.Family.Name == "TestBall").ToList();
                    List<ElementId> elemId2Remove = new List<ElementId>();
                    if (previousElems.Count == 0 || TaskDialog.Show("Revit", "Existing element found, remove them?", TaskDialogCommonButtons.Yes) == TaskDialogResult.Yes)
                    {
                        previousElems.ForEach(c => elemId2Remove.Add(c.Id));
                        doc.Delete(elemId2Remove);
                        t.Commit();
                        t.Start("Generat elements");
                        for (int i = 0; i <= 99; i++)
                        {
                            int symboIdx = i % 2;
                            var sym = symbols[symboIdx];
                            XYZ lcPt = new XYZ(100000 * rand.NextDouble() / 304.8, 100000 * rand.NextDouble() / 304.8, 100000 * rand.NextDouble() / 304.8);
                            FamilyInstance testBall = doc.Create.NewFamilyInstance(lcPt, sym, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            testBallIds.Add(testBall.Id);

                        }
                        t.Commit();
                    }
                    else
                    {
                        foreach (var elem in previousElems)
                        {
                            testBallIds.Add(elem.Id);
                        }
                    }
                }
            }

            //link elements
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "csv file|*.csv";
            ofd.Title = "Please select the work table";
            string strFileName = "";
            string strWorkHeder = "";
            Dictionary<string, List<string>> WorkIdGroup = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> wid_Elemids = new Dictionary<string, List<string>>();
            Dictionary<ElementId, List<string>> elemid_Wids = new Dictionary<ElementId, List<string>>();
            if (ofd.ShowDialog() == DialogResult.OK)
            {

                DataTable dtWBS = new DataTable();
                strFileName = ofd.FileName;
                using (StreamReader sr = new StreamReader(strFileName, Encoding.Default))
                {
                    strWorkHeder = sr.ReadLine();
                    foreach (var h in strWorkHeder.Split(','))
                    {
                        dtWBS.Columns.Add(h);
                    }
                    while (!sr.EndOfStream)
                    {
                        var content = sr.ReadLine();
                        dtWBS.Rows.Add(content.Split(','));
                        var workId = content.Split(',')[0];
                        wid_Elemids.Add(workId, new List<string>());
                        var fatherId = workId.Split('.')[0];
                        if (!WorkIdGroup.ContainsKey(fatherId))
                        {
                            WorkIdGroup.Add(fatherId, new List<string>());
                        }
                        WorkIdGroup[fatherId].Add(workId);
                    }
                    sr.Close();
                }
                //attatch work to elements
                int groupCount = WorkIdGroup.Count;
                var groupKeys = WorkIdGroup.Keys.ToArray();
                for (int i = 0; i <= testBallIds.Count - 1; i++)
                {
                    int elemid = testBallIds[i].IntegerValue;
                    int groupIndex = i % groupCount;
                    var wkGroups = WorkIdGroup[groupKeys[groupIndex]];
                    foreach (var wId in wkGroups)
                    {
                        wid_Elemids[wId].Add($"0${elemid}");
                        if (elemid_Wids.ContainsKey(testBallIds[i]))
                        {
                            elemid_Wids[testBallIds[i]].Add(wId);
                        }
                        else
                        {
                            elemid_Wids.Add(testBallIds[i], new List<string>() { wId });
                        }
                    }
                }
                //update works
                foreach (DataRow dr in dtWBS.Rows)
                {
                    var workId = dr[0].ToString();
                    var elemLinked = string.Join(";", wid_Elemids[workId]);
                    dr["ElementId"] = elemLinked;
                }
                //update file
                using (StreamWriter sw = new StreamWriter(strFileName, false, Encoding.Default))
                {
                    sw.WriteLine(strWorkHeder);
                    foreach (DataRow dr in dtWBS.Rows)
                    {
                        sw.WriteLine(string.Join(",", dr.ItemArray));
                    }
                    sw.Flush();
                    sw.Close();
                }
                //log work info to elements
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Log WBS Ids");
                    foreach (var elemId in testBallIds)
                    {
                        var elem = doc.GetElement(elemId);
                        var workIds = string.Join(",", elemid_Wids[elemId]);
                        elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(workIds);
                    }
                    t.Commit();
                }
                TaskDialog.Show("Revit", "Done!");
                Process.Start("explorer.exe", $"/select,\"{strFileName}\"");
            }

            return Result.Succeeded;
            /*
            //load data
            Dictionary<string,List<int>>elemid_LinkingActivities = new Dictionary<string,List<int>>();
            List<string[]> workids = new List<string[]>();
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "csv file|*.csv";
            ofd.Title = "Please select the work table";
            string strFileName = "";
            string strWorkHeder = "";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                strFileName=ofd.FileName;
                using(StreamReader sr = new StreamReader(strFileName,Encoding.Default))
                {
                    strWorkHeder= sr.ReadLine();
                    while(!sr.EndOfStream)
                    {
                        var content = sr.ReadLine();
                        var workId = content.Split(',');
                        workids.Add(workId);
                    }
                    sr.Close();
                }
            }
            ofd.Title = "Please select the element table";
            
            if(ofd.ShowDialog() == DialogResult.OK)
            {
                Random rand = new Random();
                using (StreamReader sr = new StreamReader(ofd.FileName,Encoding.Default))
                {
                    sr.ReadLine();
                    while (!sr.EndOfStream)
                    {
                        var content = sr.ReadLine();
                        var elemId = content.Split(',')[0];
                        //randomly choose a task
                        var workIdx=rand.Next(0, workids.Count);
                        elemid_LinkingActivities.Add(elemId, workIdx);
                    }
                    sr.Close();
                }
                Dictionary<int,StringBuilder> workId_Elemids = new Dictionary<int,StringBuilder>();
                foreach(var kvp in elemid_LinkingActivities)
                {
                    var elemId=kvp.Key;
                    var workIdx = kvp.Value;
                    if(!workId_Elemids.ContainsKey(workIdx))
                    {
                        workId_Elemids.Add(workIdx, new StringBuilder(elemId));
                    }
                    else
                    {
                        workId_Elemids[workIdx].Append($";{elemId}");
                    }
                }
                //update work table
                foreach (var kvp in workId_Elemids)
                {
                    var workIdx = kvp.Key;
                    workids[workIdx][4] = kvp.Value.ToString();
                }
                using (StreamWriter  sr = new StreamWriter(strFileName,false,Encoding.Default))
                {
                    sr.WriteLine(strWorkHeder);
                    foreach (var work in workids)
                    {
                        sr.WriteLine(string.Join(",",work));
                    }
                    sr.Flush();
                    sr.Close();
                }
            }
            TaskDialog.Show("Revit", "Done!");
            Process.Start("explorer.exe", $"/select,\"{strFileName}\"");
            return Result.Succeeded;
            */
            //throw new NotImplementedException();
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class AttatchGeneralElemIds2Works : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var app = uiApp.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc.Document;
            var view = doc.ActiveView as View3D;

            List<ElementId> testBallIds = new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType().ToElementIds().ToList();

            //link elements
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "csv file|*.csv";
            ofd.Title = "Please select the work table";
            string strFileName = "";
            string strWorkHeder = "";
            Dictionary<string, List<string>> WorkIdGroup = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> wid_Elemids = new Dictionary<string, List<string>>();
            Dictionary<ElementId, List<string>> elemid_Wids = new Dictionary<ElementId, List<string>>();
            if (ofd.ShowDialog() == DialogResult.OK)
            {

                DataTable dtWBS = new DataTable();
                strFileName = ofd.FileName;
                using (StreamReader sr = new StreamReader(strFileName, Encoding.Default))
                {
                    strWorkHeder = sr.ReadLine();
                    foreach (var h in strWorkHeder.Split(','))
                    {
                        dtWBS.Columns.Add(h);
                    }
                    while (!sr.EndOfStream)
                    {
                        var content = sr.ReadLine();
                        dtWBS.Rows.Add(content.Split(','));
                        var workId = content.Split(',')[0];
                        wid_Elemids.Add(workId, new List<string>());
                        var fatherId = workId.Split('.')[0];
                        if (!WorkIdGroup.ContainsKey(fatherId))
                        {
                            WorkIdGroup.Add(fatherId, new List<string>());
                        }
                        WorkIdGroup[fatherId].Add(workId);
                    }
                    sr.Close();
                }
                //attatch work to elements
                int groupCount = WorkIdGroup.Count;
                var groupKeys = WorkIdGroup.Keys.ToArray();
                for (int i = 0; i <= testBallIds.Count - 1; i++)
                {
                    int elemid = testBallIds[i].IntegerValue;
                    int groupIndex = i % groupCount;
                    var wkGroups = WorkIdGroup[groupKeys[groupIndex]];
                    foreach (var wId in wkGroups)
                    {
                        wid_Elemids[wId].Add($"0${elemid}");
                        if (elemid_Wids.ContainsKey(testBallIds[i]))
                        {
                            elemid_Wids[testBallIds[i]].Add(wId);
                        }
                        else
                        {
                            elemid_Wids.Add(testBallIds[i], new List<string>() { wId });
                        }
                    }
                }
                //update works
                foreach (DataRow dr in dtWBS.Rows)
                {
                    var workId = dr[0].ToString();
                    var elemLinked = string.Join(";", wid_Elemids[workId]);
                    dr["ElementId"] = elemLinked;
                }
                //update file
                using (StreamWriter sw = new StreamWriter(strFileName, false, Encoding.Default))
                {
                    sw.WriteLine(strWorkHeder);
                    foreach (DataRow dr in dtWBS.Rows)
                    {
                        sw.WriteLine(string.Join(",", dr.ItemArray));
                    }
                    sw.Flush();
                    sw.Close();
                }
                //log work info to elements
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Log WBS Ids");
                    foreach (var elemId in testBallIds)
                    {
                        var elem = doc.GetElement(elemId);
                        var workIds = string.Join(",", elemid_Wids[elemId]);
                        //elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(workIds);
                    }
                    t.Commit();
                }
                TaskDialog.Show("Revit", "Done!");
                Process.Start("explorer.exe", $"/select,\"{strFileName}\"");
            }

            return Result.Succeeded;
            /*
            //load data
            Dictionary<string,List<int>>elemid_LinkingActivities = new Dictionary<string,List<int>>();
            List<string[]> workids = new List<string[]>();
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "csv file|*.csv";
            ofd.Title = "Please select the work table";
            string strFileName = "";
            string strWorkHeder = "";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                strFileName=ofd.FileName;
                using(StreamReader sr = new StreamReader(strFileName,Encoding.Default))
                {
                    strWorkHeder= sr.ReadLine();
                    while(!sr.EndOfStream)
                    {
                        var content = sr.ReadLine();
                        var workId = content.Split(',');
                        workids.Add(workId);
                    }
                    sr.Close();
                }
            }
            ofd.Title = "Please select the element table";
            
            if(ofd.ShowDialog() == DialogResult.OK)
            {
                Random rand = new Random();
                using (StreamReader sr = new StreamReader(ofd.FileName,Encoding.Default))
                {
                    sr.ReadLine();
                    while (!sr.EndOfStream)
                    {
                        var content = sr.ReadLine();
                        var elemId = content.Split(',')[0];
                        //randomly choose a task
                        var workIdx=rand.Next(0, workids.Count);
                        elemid_LinkingActivities.Add(elemId, workIdx);
                    }
                    sr.Close();
                }
                Dictionary<int,StringBuilder> workId_Elemids = new Dictionary<int,StringBuilder>();
                foreach(var kvp in elemid_LinkingActivities)
                {
                    var elemId=kvp.Key;
                    var workIdx = kvp.Value;
                    if(!workId_Elemids.ContainsKey(workIdx))
                    {
                        workId_Elemids.Add(workIdx, new StringBuilder(elemId));
                    }
                    else
                    {
                        workId_Elemids[workIdx].Append($";{elemId}");
                    }
                }
                //update work table
                foreach (var kvp in workId_Elemids)
                {
                    var workIdx = kvp.Key;
                    workids[workIdx][4] = kvp.Value.ToString();
                }
                using (StreamWriter  sr = new StreamWriter(strFileName,false,Encoding.Default))
                {
                    sr.WriteLine(strWorkHeder);
                    foreach (var work in workids)
                    {
                        sr.WriteLine(string.Join(",",work));
                    }
                    sr.Flush();
                    sr.Close();
                }
            }
            TaskDialog.Show("Revit", "Done!");
            Process.Start("explorer.exe", $"/select,\"{strFileName}\"");
            return Result.Succeeded;
            */
            //throw new NotImplementedException();
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class AttatchMeshElemIds2Works : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var app = uiApp.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc.Document;
            var view = doc.ActiveView as View3D;

            List<string> testBallIds = new List<string>();
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "mesh |*.fireRiskData";
            if(ofd.ShowDialog()==DialogResult.Cancel)
            {
                return Result.Cancelled;
            }
            FastZip fastZip = new FastZip();
            // 生成临时文件夹路径
            var voxFileName = ofd.FileName;
            string tempDirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            fastZip.ExtractZip(voxFileName, tempDirPath, null);
            fastZip = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var voxelPath = Path.Combine(tempDirPath, Path.GetFileNameWithoutExtension(ofd.FileName) + ".txt");
            var finalPath = Path.Combine(Path.GetDirectoryName(ofd.FileName), Path.GetFileName(ofd.FileName));

            //Obtain file path
            string strMesh = null;
            string strMaterial = null;
            string strElemMatRel = null;
            foreach (var file in Directory.GetFiles(tempDirPath))
            {
                if (Path.GetExtension(file) == ".txt")//mesh
                {
                    strMesh = file;
                }
                else if (Path.GetFileName(file) == "materials.csv")//mat
                {
                    strMaterial = file;
                }
                else if (Path.GetFileName(file) == "matElemRel.csv")//elemData
                {
                    strElemMatRel = file;
                }
                else if (Path.GetExtension(file) == ".matadata")//modeel info
                {
                    using (StreamReader sr = new StreamReader(file))
                    {
                        var modeldata = sr.ReadToEnd();
                        int numElems = int.Parse(modeldata.Split(':')[1]);
                        
                        sr.Close();
                    }
                }
                else
                {
                    throw new Exception("Reading file error");
                }
            }
            
            //Load elem-mat-real
            using (StreamReader sr = new StreamReader(strElemMatRel, Encoding.Default))
            {
                var content = sr.ReadLine();
                while (!sr.EndOfStream)
                {
                    content = sr.ReadLine();
                    var items = content.Split(',');
                    var elemid = items[0];
                    testBallIds.Add(elemid);
                }
                sr.Close();
            }
            //delete filre
            Directory.Delete(tempDirPath,true);
            //link elements
            ofd = new OpenFileDialog();
            ofd.Filter = "csv file|*.csv";
            ofd.Title = "Please select the work table";
            string strFileName = "";
            string strWorkHeder = "";
            Dictionary<string, List<string>> WorkIdGroup = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> wid_Elemids = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> elemid_Wids = new Dictionary<string, List<string>>();
            if (ofd.ShowDialog() == DialogResult.OK)
            {

                DataTable dtWBS = new DataTable();
                strFileName = ofd.FileName;
                using (StreamReader sr = new StreamReader(strFileName, Encoding.Default))
                {
                    strWorkHeder = sr.ReadLine();
                    foreach (var h in strWorkHeder.Split(','))
                    {
                        dtWBS.Columns.Add(h);
                    }
                    while (!sr.EndOfStream)
                    {
                        var content = sr.ReadLine();
                        dtWBS.Rows.Add(content.Split(','));
                        var workId = content.Split(',')[0];
                        wid_Elemids.Add(workId, new List<string>());
                        var fatherId = workId.Split('.')[0];
                        if (!WorkIdGroup.ContainsKey(fatherId))
                        {
                            WorkIdGroup.Add(fatherId, new List<string>());
                        }
                        WorkIdGroup[fatherId].Add(workId);
                    }
                    sr.Close();
                }
                //attatch work to elements
                int groupCount = WorkIdGroup.Count;
                var groupKeys = WorkIdGroup.Keys.ToArray();
                for (int i = 0; i <= testBallIds.Count - 1; i++)
                {
                    var elemid = testBallIds[i];
                    int groupIndex = i % groupCount;
                    var wkGroups = WorkIdGroup[groupKeys[groupIndex]];
                    foreach (var wId in wkGroups)
                    {
                        wid_Elemids[wId].Add(elemid);
                        if (elemid_Wids.ContainsKey(testBallIds[i]))
                        {
                            elemid_Wids[testBallIds[i]].Add(wId);
                        }
                        else
                        {
                            elemid_Wids.Add(testBallIds[i], new List<string>() { wId });
                        }
                    }
                }
                //update works
                foreach (DataRow dr in dtWBS.Rows)
                {
                    var workId = dr[0].ToString();
                    var elemLinked = string.Join(";", wid_Elemids[workId]);
                    dr["ElementId"] = elemLinked;
                }
                //update file
                using (StreamWriter sw = new StreamWriter(strFileName, false, Encoding.Default))
                {
                    sw.WriteLine(strWorkHeder);
                    foreach (DataRow dr in dtWBS.Rows)
                    {
                        sw.WriteLine(string.Join(",", dr.ItemArray));
                    }
                    sw.Flush();
                    sw.Close();
                }
                //log work info to elements
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Log WBS Ids");
                    foreach (var elemId in testBallIds)
                    {
                        var elem = doc.GetElement(elemId);
                        var workIds = string.Join(",", elemid_Wids[elemId]);
                        //elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(workIds);
                    }
                    t.Commit();
                }
                TaskDialog.Show("Revit", "Done!");
                Process.Start("explorer.exe", $"/select,\"{strFileName}\"");
            }
            return Result.Succeeded;

        }
    }


    [Transaction(TransactionMode.Manual)]
    public class ConverertSolidModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var app = uiApp.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc.Document;
            var view = doc.ActiveView;
            var elems = new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType().ToElements();
            List<ElementId> elem2Hide = new List<ElementId>();
            using (Transaction t = new Transaction(doc))
            {
                t.Start("Generate solid model");
                foreach (var elem in elems)
                {
                    elem2Hide.Add(elem.Id);
                    List<Solid> elemSlds = this.Get_Solid(elem, doc).ToList();
                    if (elemSlds.Count > 0)
                    {
                        var cat = elem.Category;
                        if (!DirectShape.IsValidCategoryId(cat.Id, doc))
                        {
                            cat = Category.GetCategory(doc, BuiltInCategory.OST_GenericModel);
                        }
                        DirectShape ds = null;
#if Revit2016
ds = DirectShape.CreateElement(doc, cat.Id,new Guid().ToString(),new Guid().ToString());
#else
                        ds = DirectShape.CreateElement(doc, cat.Id);
#endif
                        ds.AppendShape(elemSlds.Where(c => ds.IsValidGeometry(c)).ToArray());
                    }

                }
                foreach (var elem in elem2Hide)
                {
                    try
                    {
                        doc.Delete(elem);
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }
                }

                t.Commit();
            }
            return Result.Succeeded;
            //throw new NotImplementedException();
        }
        private IEnumerable<Solid> Get_Solid(Element elem, Document doc)
        {
            var view = doc.ActiveView;
            Options options = new Options();
            options.View = view;
            options.ComputeReferences = true;
            GeometryElement geoElem = elem.get_Geometry(options);
            if (geoElem != null)
            {
                foreach (GeometryObject geoObj in geoElem)
                {
                    if (geoObj is Solid)
                    {
                        var sld = geoObj as Solid;
                        if (sld.Faces.Size > 0)
                            yield return sld;
                    }
                    else if (geoObj is GeometryInstance)
                    {
                        var geoIns = geoObj as GeometryInstance;
                        foreach (var geoSld in Get_SolidInGeometryInstance(geoIns))
                        {
                            yield return geoSld;
                        }
                    }
                }
            }
        }
        private IEnumerable<Solid> Get_SolidInGeometryInstance(GeometryInstance instance)
        {
            var geoElem = instance.GetInstanceGeometry();
            if (geoElem != null)
            {
                foreach (GeometryObject geoObj in geoElem)
                {
                    if (geoObj is Solid)
                    {
                        var sld = geoObj as Solid;
                        if (sld.Faces.Size > 0)
                            yield return sld;
                    }
                    else if (geoObj is GeometryInstance)
                    {
                        foreach (var sld in Get_SolidInGeometryInstance((GeometryInstance)geoObj))
                        {
                            if (sld != null)
                            {
                                yield return sld;
                            }
                        }
                    }
                }
            }

        }
    }
    [Transaction(TransactionMode.Manual)]
    public class CopyElemFromLink : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var app = uiApp.Application;
            var UIDoc = uiApp.ActiveUIDocument;
            var doc = UIDoc.Document;
            var sel = UIDoc.Selection;
            var elemRefs = sel.PickObjects(ObjectType.Element, new RevitLinkSelector());
            Transaction t = new Transaction(doc);
            t.Start("Copy element");
            if (elemRefs.Count > 0)
            {
                foreach (var elemRef in elemRefs)
                {
                    RevitLinkInstance link = doc.GetElement(elemRef) as RevitLinkInstance;
                    if (link != null)
                    {
                        var linkDoc = link.Document;
                        HashSet<ElementId> elemCopied = new HashSet<ElementId>();
                        //copy elevation
                        List<ElementId> elems2Copy = new List<ElementId>();
                        //create a very big box
                        Outline outline = new Outline(new XYZ(double.MinValue, double.MinValue, double.MinValue), new XYZ(double.MaxValue, double.MaxValue, double.MaxValue));
                        int numCopyFailed = 0;
                        foreach (var elem in new FilteredElementCollector(linkDoc).WhereElementIsNotElementType().WherePasses(new BoundingBoxIntersectsFilter(outline)))
                        {

                            if (!elem.ViewSpecific)
                            {
                                elems2Copy.Add(elem.Id);
                                try
                                {
                                    ElementTransformUtils.CopyElement(linkDoc, elem.Id, XYZ.Zero);
                                }
                                catch (Exception ex)
                                {
                                    numCopyFailed += 1;
                                    continue;
                                }

                            }
                        }


                        //ElementTransformUtils.CopyElements(linkDoc, elems2Copy, doc,null,new CopyPasteOptions());
                        foreach (var elev in elems2Copy)
                        {
                            elemCopied.Add(elev);
                        }
                        MessageBox.Show($"{numCopyFailed} elem failed copy");
                        //copy other element


                    }
                }
            }
            t.Commit();
            return Result.Succeeded;
            //throw new NotImplementedException();
        }
        public class CopyHandler : IDuplicateTypeNamesHandler
        {
            public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
            {
                return DuplicateTypeAction.UseDestinationTypes;
            }
        }

        public class RevitLinkSelector : ISelectionFilter
        {
            bool ISelectionFilter.AllowElement(Element elem)
            {
                return (elem is RevitLinkInstance);
                //throw new NotImplementedException();
            }

            bool ISelectionFilter.AllowReference(Reference reference, XYZ position)
            {
                return true;
                //throw new NotImplementedException();
            }
        }
    }
}
