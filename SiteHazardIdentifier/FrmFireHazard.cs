using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.UI;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using RevitVoxelzation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using Document = Autodesk.Revit.DB.Document;
using Form = System.Windows.Forms.Form;
namespace SiteHazardIdentifier
{
    public partial class FrmFireHazard : Form
    {
        public List<VoxelElement> Elements { get; set; }
        public List<Work> Works { get; set; }
        public double VoxelSizes { get; set; }
        public string Model { get; set; }
        public string EndPoint { get; set; }
        public string APIKey { get; set; }
        public string SystemInfo { get; set; }
        public string MeshPath { get; set; }
        public Dictionary<string, string> ElemId_InternalId { get; set; }
        public DataTable MatId_MatInfo { get; set; }
        //private Dictionary<string, bool> MatId_Conbustible { get; set; } = new Dictionary<string, bool>();
        public RiskIdentifier Identifier { get; set; }
        public RevitMeshDocumenetConverter Voxelizer { get; set; }
        public List<Document> Documents { get; set; }
        public ExternalEvent VisualizeBox { get; internal set; }
        public bool ShowAllWorkStatus { get; set; } = false;
        public FrmFireHazard()
        {
            InitializeComponent();
            ServicePointManager.SecurityProtocol =
           SecurityProtocolType.Tls12 |
           SecurityProtocolType.Tls13;
        }
        public void initProgress(int maxValue)
        {
            this.prog.Maximum = maxValue;
            this.prog.Value = 0;

        }
        private void FrmFireHazard_Load(object sender, EventArgs e)
        {

        }
        public ExternalEvent GetMeshes { get; set; }
        public ExternalEvent LoadVoxels { get; internal set; }
        public ExternalEvent GenerateVoxels { get; internal set; }
        public IEnumerable<LightWeightVoxelElement> LightWeightVoxelElements { get=>this.boxElements; }

        private async void btnVoxelize_Click(object sender, EventArgs e)
        {
            if (this.MeshPath == null || MessageBox.Show("An existing mesh path found, rewrite it?", "Caution", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                OpenFileDialog opendiag = new OpenFileDialog();
                opendiag.Filter = "Mesh file|*.fireRiskData";
                if (opendiag.ShowDialog() == DialogResult.OK && opendiag.FileName != string.Empty)
                {
                    this.MeshPath = opendiag.FileName;
                }
                else
                {
                    return;
                }
            }
            SaveFileDialog sfg = new SaveFileDialog();
            sfg.Filter = "Compresed Voxel data|*.firelitevox";
            if (sfg.ShowDialog() == DialogResult.OK)
            {
                this.Voxelizer = new RevitMeshDocumenetConverter() { Origin = Vec3.Zero };
                int numElemVoxelized = 0;
                var voxeSize = double.Parse(this.txtVoxSize.Text);
                this.Voxelizer.VoxelSize = voxeSize;
                //unpack
                FastZip fastZip = new FastZip();
                // 生成临时文件夹路径
                string tempDirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                fastZip.ExtractZip(this.MeshPath, tempDirPath, null);
                fastZip = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var boxPath = Path.Combine(tempDirPath, Path.GetFileNameWithoutExtension(sfg.FileName) + ".txt");
                var finalPath = Path.Combine(Path.GetDirectoryName(sfg.FileName), Path.GetFileName(sfg.FileName));

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
                            initProgress(numElems);
                            sr.Close();
                        }
                    }
                    else
                    {
                        throw new Exception("Reading file error");
                    }
                }

                this.Voxelizer.Origin = Vec3.Zero;
                this.Voxelizer.VoxelSize = int.Parse(txtVoxSize.Text);

                // 获取插件的地址
                string pluginAssemblyPath = typeof(BoxElement).Assembly.Location;
                string pluginDirectory = System.IO.Path.GetDirectoryName(pluginAssemblyPath);
                string boxExePath = Path.Combine(pluginDirectory, "VoxelUI.exe");
                MessageBox.Show(boxExePath);
                string arguments = string.Join(" ", strMesh, this.txtVoxSize.Text.ToString(), "1",boxPath);
                //string arguments = string.Join(" ", string.Empty, this.txtVoxSize.Text.ToString(), "1");
                // 1. 开始异步等待客户端连接
                // 2. 启动子进程（此时服务器已经在监听）
                Process process = new Process();
                process.StartInfo.FileName = boxExePath;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.Start();
                //创建一个命名管道
                var serverStream = new NamedPipeServerStream("MyServer",
                           PipeDirection.InOut,
                           1,
                           PipeTransmissionMode.Byte,
                           PipeOptions.Asynchronous
                       );
                await serverStream.WaitForConnectionAsync();
                int numTriangles = 0;
                try
                {
                    // 4. 异步读取管道数据
                    using (var streamReader = new StreamReader(serverStream, Encoding.Default))
                    {
                        string line;
                        while ((line = await streamReader.ReadLineAsync()) != null)
                        {
                            numTriangles = int.Parse(line);
                        }
                    }
                    //等待窗体关闭
                    process.WaitForExit();
                    this.txtInfo.Text=$"Number of triangles:{numTriangles}";
                    //保存结果
                    //RevitMeshDocumenetConverter.WriteVoxelFile(voxelPath, this.Voxelizer.Origin, this.Voxelizer.VoxelSize, voxElements);
                    //generate a new file zip
                    var saveVoxPath = Path.GetDirectoryName(boxPath);
                    var voxFileName = Path.GetFileNameWithoutExtension(boxPath);
                    //delete mesh
                    File.Delete(strMesh);
                    //create a new zip
                    FastZip zip = new FastZip();
                    zip.CreateZip(finalPath, tempDirPath, true, "");
                    //remove temp file path
                    Directory.Delete(tempDirPath, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message + ex.StackTrace);
                }
                finally
                {
                    serverStream.Close();
                    //删除临时文件
                    if(File.Exists(strMesh))
                    {
                        File.Delete(strMesh);
                    }
                    
                }
            }
        }

        private void btnVoxelize_Click_Old(object sender, EventArgs e)
        {
            if (this.MeshPath == null || MessageBox.Show("An existing mesh path found, rewrite it?", "Caution", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                OpenFileDialog opendiag = new OpenFileDialog();
                opendiag.Filter = "Mesh file|*.fireRiskData";
                if (opendiag.ShowDialog() == DialogResult.OK && opendiag.FileName != string.Empty)
                {
                    this.MeshPath = opendiag.FileName;
                }
                else
                {
                    return;
                }
            }
            SaveFileDialog ofd = new SaveFileDialog();
            ofd.Title = "Choose a location to save voxels";
            ofd.Filter = "voxel text files|*.fireRiskVoxel";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                this.Voxelizer = new RevitMeshDocumenetConverter() { Origin = Vec3.Zero };
                int numElemVoxelized = 0;
                var voxeSize = double.Parse(this.txtVoxSize.Text);
                this.Voxelizer.VoxelSize = voxeSize;
                //unpack
                FastZip fastZip = new FastZip();
                // 生成临时文件夹路径
                string tempDirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                fastZip.ExtractZip(this.MeshPath, tempDirPath, null);
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
                            initProgress(numElems);
                            sr.Close();
                        }
                    }
                    else
                    {
                        throw new Exception("Reading file error");
                    }
                }
                Stopwatch sw = new Stopwatch();
                BackgroundWorker bw = new BackgroundWorker();
                bw.WorkerSupportsCancellation = true;
                bw.DoWork += ((a, h) =>
                {
                    sw.Start();
                    //voxelize
                    //foreach (var me in this.Voxelizer.Voxelize(strMesh, voxelPath))
                    foreach (var me in this.Voxelizer.VoxelizeReportTime(strMesh, voxelPath))
                    {
                        numElemVoxelized += 1;
                        if (numElemVoxelized % 10 == 0)
                        {
                            prog.BeginInvoke(new Action(() =>
                            {
                                prog.Value = numElemVoxelized;
                                this.Text = $"已完成{numElemVoxelized}个,剩余{prog.Maximum}个";
                            }));
                            //Thread.Sleep(1);
                        }
                    }
                });
                bw.ProgressChanged += ((a, b) =>
                {
                    prog.Value += 1;
                    this.Text = $"已完成{prog.Value}个,剩余{prog.Maximum}个";
                });
                bw.RunWorkerCompleted += ((a, b) =>
                {
                    try
                    {
                        sw.Stop();
                        prog.Value = prog.Maximum;
                        //generate a new file zip
                        var saveVoxPath = Path.GetDirectoryName(voxelPath);
                        var voxFileName = Path.GetFileNameWithoutExtension(voxelPath);
                        //delete mesh
                        File.Delete(strMesh);
                        //create a new zip
                        FastZip zip = new FastZip();
                        zip.CreateZip(finalPath, tempDirPath, true, "");
                        //remove temp file path
                        Directory.Delete(tempDirPath, true);
                        MessageBox.Show($"Voxelization Done,time elapsed:{sw.Elapsed.TotalSeconds} s");
                        txtInfo.Text += "\r\n";
                        txtInfo.Text += $"time Gen GridPts{this.Voxelizer.timeGenGridPts}," +
                        $"time Gen Vox{this.Voxelizer.timeGenVoxels}," +
                        $"mergeVox:{this.Voxelizer.timeMergeVoxels}," +
                        $"fillVox{this.Voxelizer.timeFillVoxels}";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message + ex.StackTrace);
                    }
                    //
                });
                try
                {
                    bw.RunWorkerAsync();

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message + ex.StackTrace);
                }
            }
        }
        private void btnGetMesh_Click(object sender, EventArgs e)
        {
            this.GetMeshes.Raise();
        }
        public RiskIdentifier identifier;
        private List<VoxelElement> voxelelements;
        private List<LightWeightVoxelElement> boxElements;

        private List<MeshElement> meshElements;
        private string tempMatPath;
        private string tempFolderPath;
        private string voxFileName;
        private string wbsPath;
        private void btnLoadVoxel_Click(object sender, EventArgs e)
        {
            //this.LoadVoxels.Raise();
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "voxel file|*.fireRiskVoxel|mesh file|*.fireRiskData|Lightweight Voxel file|*.firelitevox";
            if (ofd.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            voxFileName = ofd.FileName;
            //create a new path
            string strTempPath = Path.Combine(Path.GetTempPath(), "$TmpVox");
            tempFolderPath = strTempPath;
            try
            {
                if (Path.GetExtension(voxFileName) == ".fireRiskVoxel")
                {
                    if (this.Voxelizer == null)
                    {
                        this.Voxelizer = new RevitMeshDocumenetConverter();
                    }

                    if (Directory.Exists(strTempPath))
                    {
                        Directory.Delete(strTempPath, true);
                    }
                    var fastZip = new FastZip();
                    fastZip.ExtractZip(ofd.FileName, strTempPath, null);
                    int numElems = 0;
                    string voxPath = "";
                    string strMatPath = "";
                    string strElemMatPath = "";
                    foreach (var file in Directory.GetFiles(strTempPath))
                    {
                        if (Path.GetExtension(file) == ".txt")
                        {
                            voxPath = file;
                        }
                        else if (Path.GetExtension(file) == ".matadata")
                        {
                            using (var sw = new StreamReader(file, Encoding.Default, false))
                            {
                                numElems = int.Parse(sw.ReadToEnd().Split(':')[1]);
                                sw.Close();
                            }
                        }
                        else if (Path.GetFileName(file) == "materials.csv")
                        {
                            strMatPath = file;
                            tempMatPath = file;
                        }
                        else if (Path.GetFileName(file) == "matElemRel.csv")
                        {
                            strElemMatPath = file;
                        }
                    }
                    //load material
                    this.MatId_MatInfo = CSVHelper.ReadCSV(strMatPath);
                    var newCol = "Reason (Less than 50 words.Use semicolon to replace commas)";
                    if (!this.MatId_MatInfo.Columns.Contains(newCol))
                        this.MatId_MatInfo.Columns.Add(newCol);
                    dgvMat.DataSource = this.MatId_MatInfo;
                    this.MatId_MatInfo.PrimaryKey = new DataColumn[1] { this.MatId_MatInfo.Columns[0] };
                    this.ElemId_InternalId = new Dictionary<string, string>();
                    //Load elem-mat-real
                    using (StreamReader sr = new StreamReader(strElemMatPath, Encoding.Default))
                    {
                        var content = sr.ReadLine();
                        while (!sr.EndOfStream)
                        {
                            content = sr.ReadLine();
                            var items = content.Split(',');
                            var elemid = items[0];
                            var internalId = items[1];
                            this.ElemId_InternalId.Add(elemid, internalId);
                        }
                    }
                    //load voxel
                    voxelelements = this.Voxelizer.LoadVoxelizedElements(voxPath).ToList();
                    txtInfo.Text += $"load {voxelelements.Count} elements\r\n";
                    txtVoxSize.Text = this.Voxelizer.VoxelSize.ToString();
                }
                else if (Path.GetExtension(voxFileName) == ".firelitevox")//liteweight vox
                {

                    if (this.Voxelizer == null)
                    {
                        this.Voxelizer = new RevitMeshDocumenetConverter();
                    }

                    if (Directory.Exists(strTempPath))
                    {
                        Directory.Delete(strTempPath, true);
                    }
                    var fastZip = new FastZip();
                    fastZip.ExtractZip(ofd.FileName, strTempPath, null);
                    int numElems = 0;
                    string voxPath = "";
                    string strMatPath = "";
                    string strElemMatPath = "";
                    foreach (var file in Directory.GetFiles(strTempPath))
                    {
                        if (Path.GetExtension(file) == ".txt")
                        {
                            voxPath = file;
                        }
                        else if (Path.GetExtension(file) == ".matadata")
                        {
                            using (var sw = new StreamReader(file, Encoding.Default, false))
                            {
                                numElems = int.Parse(sw.ReadToEnd().Split(':')[1]);
                                sw.Close();
                            }
                        }
                        else if (Path.GetFileName(file) == "materials.csv")
                        {
                            strMatPath = file;
                            tempMatPath = file;
                        }
                        else if (Path.GetFileName(file) == "matElemRel.csv")
                        {
                            strElemMatPath = file;
                        }
                    }
                    //load material
                    this.MatId_MatInfo = CSVHelper.ReadCSV(strMatPath);
                    var newCol = "Reason (Less than 50 words.Use semicolon to replace commas)";
                    if (!this.MatId_MatInfo.Columns.Contains(newCol))
                        this.MatId_MatInfo.Columns.Add(newCol);
                    dgvMat.DataSource = this.MatId_MatInfo;
                    this.MatId_MatInfo.PrimaryKey = new DataColumn[1] { this.MatId_MatInfo.Columns[0] };
                    this.ElemId_InternalId = new Dictionary<string, string>();
                    //Load elem-mat-real
                    using (StreamReader sr = new StreamReader(strElemMatPath, Encoding.Default))
                    {
                        var content = sr.ReadLine();
                        while (!sr.EndOfStream)
                        {
                            content = sr.ReadLine();
                            var items = content.Split(',');
                            var elemid = items[0];
                            var internalId = items[1];
                            this.ElemId_InternalId.Add(elemid, internalId);
                        }
                    }
                    //load voxel
                    boxElements = this.Voxelizer.LoadBoxElements(voxPath).ToList();
                    txtInfo.Text += $"load {boxElements.Count} elements\r\n";
                    txtVoxSize.Text = this.Voxelizer.VoxelSize.ToString();

                    if (MessageBox.Show("visualize box?", "Hint", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        this.VisualizeBox.Raise();
                        //this.VisualizeBVHTree.Raise();
                    }

                }
                else if (Path.GetExtension(voxFileName) == ".fireRiskData") //mesh
                {
                    FastZip fastZip = new FastZip();
                    // 生成临时文件夹路径
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
                                initProgress(numElems);
                                sr.Close();
                            }
                        }
                        else
                        {
                            throw new Exception("Reading file error");
                        }
                    }
                    //load material
                    this.MatId_MatInfo = CSVHelper.ReadCSV(strMaterial);
                    var newCol = "Reason (Less than 50 words.Use semicolon to replace commas)";
                    if (!this.MatId_MatInfo.Columns.Contains(newCol))
                        this.MatId_MatInfo.Columns.Add(newCol);
                    dgvMat.DataSource = this.MatId_MatInfo;
                    this.MatId_MatInfo.PrimaryKey = new DataColumn[1] { this.MatId_MatInfo.Columns[0] };
                    this.ElemId_InternalId = new Dictionary<string, string>();
                    //Load elem-mat-real
                    using (StreamReader sr = new StreamReader(strElemMatRel, Encoding.Default))
                    {
                        var content = sr.ReadLine();
                        while (!sr.EndOfStream)
                        {
                            content = sr.ReadLine();
                            var items = content.Split(',');
                            var elemid = items[0];
                            var internalId = items[1];
                            this.ElemId_InternalId.Add(elemid, internalId);
                        }
                    }
                    int numTris = 0;
                    //load element
                    this.meshElements = new List<MeshElement>();
                    foreach (var me in RevitMeshDocumenetConverter.CreateMeshElement(strMesh))
                    {
                        numTris += me.GetTriangleNumber();
                        this.meshElements.Add(me);
                    }
                    txtInfo.Text += $"load {this.meshElements.Count} elements,Total triangles:{numTris}\r\n";
                }
                //this.identifier = new RiskIdentifier(voxelElems, new List<Work>(), this.Voxelizer.VoxelSize);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
            finally
            {
                if(Directory.Exists(strTempPath))
                    Directory.Delete(strTempPath, true);
            }
        }
        private IEnumerable<BoxElement> LoadBoxElements(string path)
        {
            using (var sr = new StreamReader(path))
            {

                //load voxel info
                while (!sr.EndOfStream)
                {
                    string elemId = sr.ReadLine();
                    string[] strVoxelData = sr.ReadLine().Split(',');
                    Vec3 min = new Vec3(double.Parse(strVoxelData[0]), double.Parse(strVoxelData[1]), double.Parse(strVoxelData[2]));
                    Vec3 max = new Vec3(double.Parse(strVoxelData[3]), double.Parse(strVoxelData[4]), double.Parse(strVoxelData[5]));
                    BoxElement be = new BoxElement() { ElementId = elemId, Min = min, Max = max };

                    yield return be;

                }
                sr.Close();

                //Read VoxelSize
            }
        }

        private List<Work> works;
        private void btnLoadWBS_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "csv file|*.csv";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                wbsPath = ofd.FileName;
                DataTable dt = CSVHelper.ReadCSV(ofd.FileName);
                string strNewCol0 = "Contains combustible substances(Yes/No)";
                string strNewCol1 = "Involve flames or Sparks or high-temperature(Yes/No)";
                string strNewCol2 = "Release flammable vapors or gases or liquids (Yes/No)";
                string strNewCol3 = "Reason(Less than 50 words.Use semicolons to replace commas)";
                string[] strNewRows = new string[4] { strNewCol0, strNewCol1, strNewCol2, strNewCol3 };
                foreach (var heder in strNewRows)
                {
                    if (!dt.Columns.Contains(heder))
                        dt.Columns.Add(heder);
                }
                dgvWBS.DataSource = dt;



                txtInfo.Text += $"load {dt.Rows.Count} works\r\n";
            }
        }

        public List<HazardCombination> elementChunks = new List<HazardCombination>();


        public List<int> SelectedIndexes = new List<int>();
        public Work SelectedWork = null;
        private void btnLLM_Click(object sender, EventArgs e)
        {
            this.GenerateVoxels.Raise();
        }

        private void UpdateWorkAndMaterials(out Dictionary<string, HazardMaterial> id_Mats)
        {
            //update work
            List<Work> works = new List<Work>();
            DataTable dt = dgvWBS.DataSource as DataTable;
            //dt.AcceptChanges();
            foreach (DataRow dr in dt.Rows)
            {
                var w = new Work();
                w.Id = dr[0].ToString();
                w.Name = dr[1].ToString();
                w.Resources = dr[2].ToString().Split(';').ToList();
                string strWorkType = dr[3].ToString();
                switch (strWorkType)
                {
                    case "Construction":
                        w.WorkType = WorkType.Construct;
                        break;
                    case "Demolition":
                        w.WorkType = WorkType.Demolish;
                        break;
                }
                w.ElementIds = dr[4].ToString().Split(';').ToList();
                w.Set_Start(Convert.ToDateTime(dr[5]));
                w.Set_Finish(Convert.ToDateTime(dr[6]));
                //set work fire info
                double fireProtection = Convert.ToDouble(txtRangeFire.Text);
                double gasProtection = Convert.ToDouble(txtRangeFire.Text);
                w.Combustible = (dr[7].ToString() == "Yes" || dr[9].ToString() == "Yes" ? true : false);
                w.FireProtectionRange = fireProtection;
                if (dr[8].ToString() == "Yes")//work emit sparks
                {
                    w.EmitSparks = true;
                    w.FireProtectionRange = Math.Max(w.FireProtectionRange, fireProtection);
                }
                if (dr[9].ToString() == "Yes")//work contains gas
                {
                    w.EmitGas = true;
                    w.FireProtectionRange = Math.Max(w.FireProtectionRange, gasProtection);
                    w.TimeBuffer = new TimeSpan(int.Parse(this.txtTimeBuffer.Text), 0, 0, 0);
                }
                //set work protection range
                works.Add(w);
            }
            this.works = works;
            //Update material
            id_Mats = new Dictionary<string, HazardMaterial>();
            var dtMat = dgvMat.DataSource as DataTable;
            dtMat.AcceptChanges();
            foreach (DataRow dr in dtMat.Rows)
            {
                var data = dr.ItemArray;
                var id = data[0].ToString();
                var name = data[1].ToString();
                var isConbustable = (dr.ItemArray[2].ToString() == "Yes" ? true : false);
                var mat = new HazardMaterial(id, name, isConbustable);
                id_Mats.Add(id, mat);
            }
            this.identifier = new RiskIdentifier(this.voxelelements, this.works, this.Voxelizer.VoxelSize);
            //attatch material to elements
            foreach (var elemInfo in this.identifier.ElemVoxRel.Values)
            {
                var elemId = elemInfo.ElementId;
                elemInfo.Materials = new List<HazardMaterial>();
                var eleminternalName = ElemId_InternalId[elemId];
                var matIds = eleminternalName.Split('_');
                foreach (var matId in matIds)
                {
                    var mat = id_Mats[matId];
                    elemInfo.Materials.Add(mat);
                }
            }

        }
        private void UpdateWorkAndMaterials_Mesh(out Dictionary<string, HazardMaterial> id_Mats)
        {
            //update work
            List<Work> works = new List<Work>();
            DataTable dt = dgvWBS.DataSource as DataTable;
            //dt.AcceptChanges();
            foreach (DataRow dr in dt.Rows)
            {
                var w = new Work();
                w.Id = dr[0].ToString();
                w.Name = dr[1].ToString();
                w.Resources = dr[2].ToString().Split(';').ToList();
                string strWorkType = dr[3].ToString();
                switch (strWorkType)
                {
                    case "Construction":
                        w.WorkType = WorkType.Construct;
                        break;
                    case "Demolition":
                        w.WorkType = WorkType.Demolish;
                        break;
                }
                w.ElementIds = dr[4].ToString().Split(';').ToList();
                w.Set_Start(Convert.ToDateTime(dr[5]));
                w.Set_Finish(Convert.ToDateTime(dr[6]));
                //set work fire info
                double fireProtection = Convert.ToDouble(txtRangeFire.Text);
                double gasProtection = Convert.ToDouble(txtRangeFire.Text);
                w.Combustible = (dr[7].ToString() == "Yes" || dr[9].ToString() == "Yes" ? true : false);
                w.FireProtectionRange = fireProtection;
                if (dr[8].ToString() == "Yes")//work emit sparks
                {
                    w.EmitSparks = true;
                    w.FireProtectionRange = Math.Max(w.FireProtectionRange, fireProtection);
                }
                if (dr[9].ToString() == "Yes")//work contains gas
                {
                    w.EmitGas = true;
                    w.FireProtectionRange = Math.Max(w.FireProtectionRange, gasProtection);
                    w.TimeBuffer = new TimeSpan(int.Parse(this.txtTimeBuffer.Text), 0, 0, 0);
                }
                //set work protection range
                works.Add(w);
            }
            this.works = works;
            //Update material
            id_Mats = new Dictionary<string, HazardMaterial>();
            var dtMat = dgvMat.DataSource as DataTable;
            dtMat.AcceptChanges();
            foreach (DataRow dr in dtMat.Rows)
            {
                var data = dr.ItemArray;
                var id = data[0].ToString();
                var name = data[1].ToString();
                var isConbustable = (dr.ItemArray[2].ToString() == "Yes" ? true : false);
                var mat = new HazardMaterial(id, name, isConbustable);
                id_Mats.Add(id, mat);
            }
            this.identifier = new RiskIdentifier(this.meshElements, this.works, 200);
            //attatch material to elements
            foreach (var elemInfo in this.identifier.ElemMeshRel.Values)
            {
                var elemId = elemInfo.ElementId;
                elemInfo.Materials = new List<HazardMaterial>();
                var eleminternalName = ElemId_InternalId[elemId];
                var matIds = eleminternalName.Split('_');
                foreach (var matId in matIds)
                {
                    var mat = id_Mats[matId];
                    elemInfo.Materials.Add(mat);
                }
            }
        }
        private void UpdateWorkAndMaterials_VoxBox(out Dictionary<string, HazardMaterial> id_Mats)
        {
            //update work
            List<Work> works = new List<Work>();
            DataTable dt = dgvWBS.DataSource as DataTable;
            //dt.AcceptChanges();
            foreach (DataRow dr in dt.Rows)
            {
                var w = new Work();
                w.Id = dr[0].ToString();
                w.Name = dr[1].ToString();
                w.Resources = dr[2].ToString().Split(';').ToList();
                string strWorkType = dr[3].ToString();
                switch (strWorkType)
                {
                    case "Construction":
                        w.WorkType = WorkType.Construct;
                        break;
                    case "Demolition":
                        w.WorkType = WorkType.Demolish;
                        break;
                }
                w.ElementIds = dr[4].ToString().Split(';').ToList();
                w.Set_Start(Convert.ToDateTime(dr[5]));
                w.Set_Finish(Convert.ToDateTime(dr[6]));
                //set work fire info
                double fireProtection = Convert.ToDouble(txtRangeFire.Text);
                double gasProtection = Convert.ToDouble(txtRangeFire.Text);
                w.Combustible = (dr[7].ToString() == "Yes" || dr[9].ToString() == "Yes" ? true : false);
                w.FireProtectionRange = fireProtection;
                if (dr[8].ToString() == "Yes")//work emit sparks
                {
                    w.EmitSparks = true;
                    w.FireProtectionRange = Math.Max(w.FireProtectionRange, fireProtection);
                }
                if (dr[9].ToString() == "Yes")//work contains gas
                {
                    w.EmitGas = true;
                    w.FireProtectionRange = Math.Max(w.FireProtectionRange, gasProtection);
                    w.TimeBuffer = new TimeSpan(int.Parse(this.txtTimeBuffer.Text), 0, 0, 0);
                }
                //set work protection range
                works.Add(w);
            }
            this.works = works;
            //Update material
            id_Mats = new Dictionary<string, HazardMaterial>();
            var dtMat = dgvMat.DataSource as DataTable;
            dtMat.AcceptChanges();
            foreach (DataRow dr in dtMat.Rows)
            {
                var data = dr.ItemArray;
                var id = data[0].ToString();
                var name = data[1].ToString();
                var isConbustable = (dr.ItemArray[2].ToString() == "Yes" ? true : false);
                var mat = new HazardMaterial(id, name, isConbustable);
                id_Mats.Add(id, mat);
            }
            this.identifier = new RiskIdentifier(this.boxElements, this.works, this.Voxelizer.VoxelSize);
            //attatch material to elements
            foreach (var elemInfo in this.identifier.ElemBoxRel.Values)
            {
                var elemId = elemInfo.ElementId;
                elemInfo.Materials = new List<HazardMaterial>();
                var eleminternalName = ElemId_InternalId[elemId];
                var matIds = eleminternalName.Split('_');
                foreach (var matId in matIds)
                {
                    var mat = id_Mats[matId];
                    elemInfo.Materials.Add(mat);
                }
            }
        }


        private async void btnAnalysis2_Click(object sender, EventArgs e)
        {
            var sw = Stopwatch.StartNew();
            double totalTime = 0;
            Dictionary<string, HazardMaterial> id_Mats = null;

            if (this.voxelelements != null && this.voxelelements.Count != 0)
            {
                UpdateWorkAndMaterials(out id_Mats);
            }
            else if (this.meshElements != null && this.meshElements.Count != 0)
            {
                UpdateWorkAndMaterials_Mesh(out id_Mats);
            }
            else
            {
                UpdateWorkAndMaterials_VoxBox(out id_Mats);
            }
            sw.Stop();
            totalTime += sw.Elapsed.TotalSeconds;
            this.txtInfo.Text += $"\r\n Initialized completed, time elapsed(s):{sw.Elapsed.TotalSeconds}";
            sw.Restart();
            this.identifier.MaterialMap = id_Mats;

            //generate chunk
            IProgress<(int, string)> progress = new Progress<(int, string)>(parcent =>
            {
                prog.Value = parcent.Item1;
                this.Text = parcent.Item2;
            });
            int numFireWork = this.works.Where(c => c.EmitSparks).Count();
            this.prog.Value = 0;
            this.prog.Maximum = 100;
            //var combId_Elems = identifier.IdentifyGlobalFireHazard(double.Parse(txtRangeFire.Text), this.ElemId_InternalId, progress); ;
            Task tks = Task.Run(() =>
            {
                if (this.voxelelements != null && this.voxelelements.Count != 0)
                {
                    identifier.IdentifyGlobalFireHazard_Voxel(double.Parse(txtRangeFire.Text), this.ElemId_InternalId, progress);

                }
                else if (this.meshElements != null && this.meshElements.Count != 0)
                {
                    identifier.IdentifyGlobalFireHazard_Mesh(double.Parse(txtRangeFire.Text), this.ElemId_InternalId, progress);
                }
                else
                {
                    identifier.IdentifyGlobalFireHazard_VoxelBox2(double.Parse(txtRangeFire.Text), this.ElemId_InternalId, progress);
                }
            });
            await tks;
            sw.Stop();
            totalTime = sw.Elapsed.TotalSeconds;
            DataTable dtElemHazardLevel = new DataTable();
            dtElemHazardLevel.Columns.Add("Hazard level");
            dtElemHazardLevel.Columns.Add("Number of elements");
            int elemExtreme = 0;
            int elemHigh = 0;
            int elemMedium = 0;
            int elemLow = 0;
            foreach (var elem in identifier.IternateElements())
            {
                if (elem.Combinations.Any(c => c.HazardLevel == CombinationHazardLevel.Extreme))
                {
                    elemExtreme++;
                }
                else if (elem.Combinations.Any(c => c.HazardLevel == CombinationHazardLevel.High))
                {
                    elemHigh++;
                }
                else if (elem.Combinations.Any(c => c.HazardLevel == CombinationHazardLevel.Medium))
                {
                    elemMedium++;
                }
                else
                {
                    elemLow++;
                }
            }
            dtElemHazardLevel.Rows.Add("Extreme", elemExtreme);
            dtElemHazardLevel.Rows.Add("High", elemHigh);
            dtElemHazardLevel.Rows.Add("Medium", elemMedium);
            dtElemHazardLevel.Rows.Add("Low", elemLow);
            dgvChunks.DataSource = dtElemHazardLevel;
            txtInfo.Text += $"\r\n Time elapsed for fire hazard analysis: {sw.Elapsed.TotalSeconds}";
            txtInfo.Text += $"\r\n Totla elapsed time: {totalTime}";
            if (MessageBox.Show("Output element data", "Hint", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                int index = 0;
                var dtResult = new DataTable();
                dtResult.Columns.Add("Hazard level");
                dtResult.Columns.Add("Id");
                var dcDis = dtResult.Columns.Add("Distance");
                dcDis.DataType = typeof(double);
                dtResult.Columns.Add("Ignition Source");
                dtResult.Columns.Add("Combustible Activities");
                dtResult.Columns.Add("Combustible Materials");
                dtResult.Columns.Add("Hazardous level");
                foreach (var elem in identifier.IternateElements())
                {
                    var elemId = elem.ElementId;
                    foreach (var combo in elem.Combinations)
                    {
                        string strIgnitionSource = "";
                        if (combo.IngntionSource != null)
                        {
                            strIgnitionSource = $"{combo.IngntionSource.Id}_{combo.IngntionSource.Name}";
                        }
                        string strCombustibleWorks = "";
                        if (combo.CombustibleWorks.Count != 0)
                        {
                            List<string> combWorkInfo = new List<string>();
                            foreach (var wc in combo.CombustibleWorks)
                            {
                                combWorkInfo.Add($"{wc.Id}_{wc.Name}");
                            }
                            strCombustibleWorks = string.Join(";", combWorkInfo);
                        }
                        string strCombustibleMaterial = "";
                        if (combo.CombustibleMaterials.Count != 0)
                        {
                            List<string> combMat = new List<string>();
                            foreach (var m in combo.CombustibleMaterials)
                            {
                                combMat.Add($"{m.Id}_{m.Name}");
                            }
                            strCombustibleMaterial = string.Join(";", combMat);
                        }
                        dtResult.Rows.Add(index, elemId, combo.Distance, strIgnitionSource, strCombustibleWorks, strCombustibleMaterial, combo.HazardLevel);
                    }
                    index += 1;
                }
                SaveFileDialog sfg = new SaveFileDialog();
                sfg.Filter = "csv file|*.csv";
                if (sfg.ShowDialog() == DialogResult.OK)
                {
                    using (var writer = new StreamWriter(sfg.FileName, false, Encoding.Default))
                    {
                        writer.Write(CSVHelper.table2CSV(dtResult));
                        writer.Flush();
                        writer.Close();
                    }
                    Process.Start("explorer.exe", $"/select,\"{sfg.FileName}\"");
                }
            }
        }





        private void btnAnalysisAll_Click(object sender, EventArgs e)
        {

        }

        private void FrmFireHazard_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (this.tempMatPath != null || this.wbsPath != null)
                {
                    if (MessageBox.Show("Save result to original voxel files?", "Caution", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        UpdateModelAndWBS();
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
            finally
            {
                if (this.tempFolderPath != null && Directory.Exists(this.tempFolderPath))
                    Directory.Delete(this.tempFolderPath, true);
            }

        }

        private void UpdateModelAndWBS()
        {
            if (this.tempMatPath != null)
            {
                var strNewMat = CSVHelper.table2CSV(dgvMat.DataSource as DataTable);
                using (var sw = new StreamWriter(tempMatPath, false, Encoding.Default))
                {
                    sw.Write(strNewMat);
                    sw.Flush();
                    sw.Close();
                }
                //delete existingg files
                if (File.Exists(voxFileName))
                {
                    File.Delete(voxFileName);
                }
                var fastZip = new FastZip();
                fastZip.CreateZip(voxFileName, tempFolderPath, true, "");
            }
            //update wbs
            if (this.wbsPath != null)
            {
                var strNewWBS = CSVHelper.table2CSV(dgvWBS.DataSource as DataTable);
                using (var sw = new StreamWriter(this.wbsPath, false, Encoding.Default))
                {
                    sw.Write(strNewWBS);
                    sw.Flush();
                    sw.Close();
                }
            }
        }


        private void groupBox6_Enter(object sender, EventArgs e)
        {

        }

        private void prog_Click(object sender, EventArgs e)
        {

        }





        private void rdbGlobal_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void btnGetAABB_Click(object sender, EventArgs e)
        {
            if (this.MeshPath == null || MessageBox.Show("An existing mesh path found, rewrite it?", "Caution", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                OpenFileDialog opendiag = new OpenFileDialog();
                opendiag.Filter = "Mesh file|*.fireRiskData";
                if (opendiag.ShowDialog() == DialogResult.OK && opendiag.FileName != string.Empty)
                {
                    this.MeshPath = opendiag.FileName;
                }
                else
                {
                    return;
                }
            }
            SaveFileDialog ofd = new SaveFileDialog();
            ofd.Title = "Choose a location to save AABBs";
            ofd.Filter = "AABB text files|*.fireRiskAABB";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                this.Voxelizer = new RevitMeshDocumenetConverter() { Origin = Vec3.Zero };
                int numElemVoxelized = 0;
                var voxeSize = double.Parse(this.txtVoxSize.Text);
                this.Voxelizer.VoxelSize = voxeSize;
                //unpack
                FastZip fastZip = new FastZip();
                // 生成临时文件夹路径
                string tempDirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                fastZip.ExtractZip(this.MeshPath, tempDirPath, null);
                fastZip = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var boxPath = Path.Combine(tempDirPath, Path.GetFileNameWithoutExtension(ofd.FileName) + ".txt");
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
                            initProgress(numElems);
                            sr.Close();
                        }
                    }
                    else
                    {
                        throw new Exception("Reading file error");
                    }
                }
                BackgroundWorker bw = new BackgroundWorker();
                bw.WorkerSupportsCancellation = true;
                bw.DoWork += ((a, h) =>
                {
                    //voxelize
                    foreach (var me in GenerateAABB(strMesh, boxPath))
                    {
                        numElemVoxelized += 1;
                        if (numElemVoxelized % 10 == 0)
                        {
                            prog.BeginInvoke(new Action(() =>
                            {
                                prog.Value = numElemVoxelized;
                                this.Text = $"已完成{numElemVoxelized}个,剩余{prog.Maximum}个";
                            }));
                            Thread.Sleep(1);
                        }
                    }
                });
                bw.ProgressChanged += ((a, b) =>
                {
                    prog.Value += 1;
                    this.Text = $"已完成{prog.Value}个,剩余{prog.Maximum}个";
                });
                bw.RunWorkerCompleted += ((a, b) =>
                {
                    try
                    {
                        prog.Value = prog.Maximum;
                        //generate a new file zip
                        var saveBoxPath = Path.GetDirectoryName(boxPath);
                        var boxFileName = Path.GetFileNameWithoutExtension(boxPath);
                        //delete mesh
                        File.Delete(strMesh);
                        //create a new zip
                        FastZip zip = new FastZip();
                        zip.CreateZip(finalPath, tempDirPath, true, "");
                        //remove temp file path
                        Directory.Delete(tempDirPath, true);
                        MessageBox.Show("AABB convert Done");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message + ex.StackTrace);
                    }
                    //
                });
                try
                {
                    bw.RunWorkerAsync();

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message + ex.StackTrace);
                }
            }
        }

        public IEnumerable<(string elemId, Vec3 min, Vec3 max)> GenerateAABB(string meshPath, string savePath)
        {
            FileStream fs = new FileStream(savePath, FileMode.Create);
            using (StreamWriter sw = new StreamWriter(fs, Encoding.Default, 1024 * 1024))
            {
                //var elems = CreateMeshElement(meshPath).ToList();
                foreach (var elem in RevitMeshDocumenetConverter.CreateMeshElement(meshPath))
                {
                    if (elem.TryGetAABB(out Vec3 min, out Vec3 max))
                    {
                        yield return (elem.ElementId, min, max);
                        sw.WriteLine(elem.ElementId);
                        string strBoxData = $"{min.ToString()},{max.ToString()}";
                        sw.WriteLine(strBoxData);
                    }

                }
                sw.Flush();
                sw.Close();
            }
        }

        private void btnElemTemporalTest_Click(object sender, EventArgs e)
        {
            if (this.identifier == null || this.identifier.ElemVoxRel == null)
            {
                MessageBox.Show("Please run the hazard identification first");
                return;
            }
            DataTable dtResult = new DataTable();
            dtResult.Columns.Add("ElementId");
            dtResult.Columns.Add("Number of wrong status");
            dtResult.Columns.Add("Number of wrong combos");
            foreach (var elem in this.identifier.ElemVoxRel.Values)
            {
                var elemId = elem.ElementId;
                int numWrongStatus = 0;
                int numWrongCombos = 0;
                //check phase
                foreach (var phasee in elem.ElemStausPeroid)
                {
                    bool phaseWrong = false;
                    var phaseSt = phasee.Item1;
                    var phaseEd = phasee.Item2;
                    var status = phasee.Item3;
                    foreach (var work in elem.Works)
                    {
                        var wkSt = work.Get_Start();
                        var wkEd = work.Get_Finish(true);
                        if (phasee.Item3 == ElementStatus.Quiescent || phasee.Item3 == ElementStatus.Void)
                        {
                            if (wkSt < phaseEd && wkEd > phaseSt)
                            {
                                phaseWrong = true;
                            }
                        }
                    }
                    switch (status)
                    {
                        case ElementStatus.Quiescent:
                            if (phaseSt == DateTime.MinValue)
                            {
                                //find the work whose start is phase End
                                var worksEnd = elem.Works.Where(c => c.Get_Start() == phaseEd).ToList();
                                if (!works.Any(c => c.WorkType == WorkType.Demolish))
                                {
                                    phaseWrong = true;
                                }
                            }
                            else if (phaseEd == DateTime.MaxValue)
                            {
                                //find the work whose finish is phaseSt
                                var worksStart = elem.Works.Where(c => c.Get_Finish(true) == phaseEd).ToList();
                                if (!works.Any(c => c.WorkType == WorkType.Construct))
                                {
                                    phaseWrong = true;
                                }
                            }
                            else
                            {
                                //find the work whose finish is phaseSt
                                var worksStart = elem.Works.Where(c => c.Get_Finish(true) == phaseEd).ToList();
                                if (!works.Any(c => c.WorkType == WorkType.Construct))
                                {
                                    phaseWrong = true;
                                }
                                var worksEnd = elem.Works.Where(c => c.Get_Start() == phaseEd).ToList();
                                if (!works.Any(c => c.WorkType == WorkType.Demolish))
                                {
                                    phaseWrong = true;
                                }
                            }
                            break;
                        case ElementStatus.Void:
                            if (phaseSt == DateTime.MinValue)
                            {
                                //find the work whose start is phase End
                                var worksEnd = elem.Works.Where(c => c.Get_Start() == phaseEd).ToList();
                                if (!works.Any(c => c.WorkType == WorkType.Construct))
                                {
                                    phaseWrong = true;
                                }
                            }
                            else if (phaseEd == DateTime.MaxValue)
                            {
                                //find the work whose finish is phaseSt
                                var worksStart = elem.Works.Where(c => c.Get_Finish(true) == phaseEd).ToList();
                                if (!works.Any(c => c.WorkType == WorkType.Demolish))
                                {
                                    phaseWrong = true;
                                }
                            }
                            else
                            {
                                //find the work whose finish is phaseSt
                                var worksStart = elem.Works.Where(c => c.Get_Finish(true) == phaseEd).ToList();
                                if (!works.Any(c => c.WorkType == WorkType.Demolish))
                                {
                                    phaseWrong = true;
                                }
                                var worksEnd = elem.Works.Where(c => c.Get_Start() == phaseEd).ToList();
                                if (!works.Any(c => c.WorkType == WorkType.Construct))
                                {
                                    phaseWrong = true;
                                }
                            }
                            break;
                    }
                    if (phaseWrong)
                    {
                        numWrongStatus += 1;
                    }

                }
                //check combo
                foreach (var combo in elem.Combinations)
                {
                    bool comboWrong = false;
                    if (combo.IngntionSource != null)
                    {
                        if (elem.IsElementVoidDuringWork(combo.IngntionSource))
                        {
                            comboWrong = true; ;
                        }
                    }
                    if (combo.CombustibleWorks != null && combo.CombustibleWorks.Count != 0)
                    {
                        foreach (var combWork in combo.CombustibleWorks)
                        {
                            if (!elem.Works.Contains(combWork))
                            {
                                comboWrong = true;
                            }
                        }
                    }
                    if (comboWrong)
                        numWrongCombos += 1;
                }
                dtResult.Rows.Add(elemId, numWrongStatus, numWrongCombos);
            }
            //save result
            SaveFileDialog sfg = new SaveFileDialog();
            sfg.Filter = "csv files|*.csv";
            if (sfg.ShowDialog() == DialogResult.OK)
            {
                using (var streamWriter = new StreamWriter(sfg.FileName, false, Encoding.Default))
                {
                    streamWriter.Write(CSVHelper.table2CSV(dtResult));
                    streamWriter.Flush();
                    streamWriter.Close();
                }
                Process.Start("explorer.exe", $"/select,\"{sfg.FileName}\"");
            }
        }
    }

    internal class BoxElement
    {
        public string ElementId { get; set; }
        public Vec3 Min { get; set; } = new Vec3();
        public Vec3 Max { get; set; } = new Vec3();
        public List<Work> Works { get; set; }

    }

    public static class CSVHelper
    {
        public static DataTable ReadCSV(string filePath)
        {
            DataTable dt = new DataTable();
            using (StreamReader sr = new StreamReader(filePath, Encoding.Default))
            {
                //read heder
                string header = sr.ReadLine();
                foreach (var item in header.Split(','))
                {
                    dt.Columns.Add(item);
                }
                while (!sr.EndOfStream)
                {
                    dt.Rows.Add(sr.ReadLine().Split(','));
                }
                sr.Close();
            }
            return dt;
        }
        public static string table2CSV(DataTable table)
        {
            StringWriter sb = new StringWriter();
            List<string> heders = new List<string>();
            foreach (DataColumn dc in table.Columns)
            {
                heders.Add(dc.ColumnName);
            }
            sb.WriteLine(string.Join(",", heders));
            foreach (DataRow dr in table.Rows)
            {
                sb.WriteLine(string.Join(",", dr.ItemArray));
            }
            return sb.ToString();
        }
    }

}
