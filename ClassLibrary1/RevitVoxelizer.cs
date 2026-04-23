
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
namespace SiteHazardIdentifier
{


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

        public static void WriteVoxelFile(string savePath,Vec3 origin,double voxelSize, List<VoxelElement> voxElems)
        {
            FileStream fs = new FileStream(savePath, FileMode.Create);
            using (StreamWriter sw = new StreamWriter(fs, Encoding.Default, 1024 * 1024))
            {
                sw.WriteLine(origin.ToString());
                sw.WriteLine(voxelSize.ToString());
                //var elems = CreateMeshElement(meshPath).ToList();
                foreach (var ve in voxElems)
                {
                    
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

      
       



    }
}
