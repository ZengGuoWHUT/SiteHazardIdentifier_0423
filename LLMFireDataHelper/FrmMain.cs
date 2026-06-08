using ICSharpCode.SharpZipLib.Zip;
using OpenAI;
using OpenAI.Chat;
using SiteHazardIdentifier;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
namespace LLMFireDataHelper
{
    public partial class FrmMain : Form
    {
        public string Key { get; set; }
        public Uri EndPoint { get; set; }
        public string model { get; set; }
        public DataTable dtMat { get; set; } = null;
        public DataTable dtWBS { get; set; } = null;
        public string MatPath { get; set; } = null;
        public string WbsPath { get; set; } = null;
        private string llmConfigPath { get; set; } = null;
        private ChatClient Client { get; set; } = null;
        private string Prompt { get; set; } = null;
        private string promptPath = null;
        public FrmMain()
        {
            InitializeComponent();
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            var curTab = tbDataPreview.SelectedTab;
            DataTable targetTable = null;
            List<int> dataRef = new List<int>();
            List<int> data2Fill = new List<int>();
            if (curTab.Text == "Material")
            {
                targetTable = dtMat;
                dataRef = new List<int>() { 0, 1 };
                data2Fill = new List<int>() { 0, 2, 3 };
            }
            else
            {
                targetTable = dtWBS;
                dataRef = new List<int>() { 0, 1, 2 };
                data2Fill = new List<int>() { 0, 7, 8, 9, 10 };
            }
            var model = txtModel.Text;
            var endPt = txtEndPt.Text;
            var key = txtKey.Text;
            this.Client = new ChatClient(model, new ApiKeyCredential(key), new OpenAIClientOptions() { Endpoint = new Uri(endPt) });
            var prompt = this.rtPrompt.Text;
            this.Prompt = prompt;
            var helper = new LLMCSVHelper();

            var dt_TokenCount = await helper.Response(this.Client, this.Prompt, targetTable, dataRef, data2Fill);
            //this.dgvMat.DataSource = dt_TokenCount.Item1;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void InitChatClient(LLMConfigData data)
        {
            var model = data.ModelName;
            var key = data.Key;
            var endPt = data.EndPoint;
            this.Client = new ChatClient(model, new ApiKeyCredential(key), new OpenAIClientOptions() { Endpoint = new Uri(endPt) });
            txtModel.Text = model;
            txtModel.Text = endPt;
            txtKey.Text = key;
        }

        private void btnSaveLLMInfo_Click(object sender, EventArgs e)
        {
            string strModel = txtModel.Text;
            string strURL = txtEndPt.Text;
            string strKey = txtKey.Text;
            var llmData = new LLMConfigData() { EndPoint = strURL, Key = strKey, ModelName = strModel };
            string strData = JsonSerializer.Serialize(llmData);
            if (string.IsNullOrEmpty(this.llmConfigPath))
            {
                SaveFileDialog sfg = new SaveFileDialog();
                sfg.Filter = "json file|*.json";
                if (sfg.ShowDialog() == DialogResult.OK)
                {
                    this.llmConfigPath = sfg.FileName;
                }
            }

            using (var streamWriter = new StreamWriter(this.llmConfigPath, false, Encoding.UTF8))
            {
                streamWriter.Write(strData);
                streamWriter.Flush();
                streamWriter.Close();
                Process.Start("explorer.exe", $"/select,\"{this.llmConfigPath}\"");
            }
        }

        private void btnLoadLLMInfo_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "json file|*.json";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                this.llmConfigPath = ofd.FileName;
                using (var sr = new StreamReader(ofd.FileName, Encoding.UTF8))
                {
                    string strData = sr.ReadToEnd();
                    LLMConfigData data = JsonSerializer.Deserialize<LLMConfigData>(strData);
                    txtModel.Text = data.ModelName;
                    txtEndPt.Text = data.EndPoint;
                    txtKey.Text = data.Key;
                }
            }
        }

        private void btnLoadPrompt_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "text file|*.txt";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                this.promptPath = ofd.FileName;
                using (var sr = new StreamReader(ofd.FileName, Encoding.UTF8))
                {
                    string strPrompt = sr.ReadToEnd();
                    rtPrompt.Text = strPrompt;
                }
            }
        }

        private void btnSavePrompt_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(this.promptPath))
            {
                SaveFileDialog sfg = new SaveFileDialog();
                sfg.Filter = "text file|*.txt";
                if (sfg.ShowDialog() == DialogResult.OK)
                {
                    this.promptPath = sfg.FileName;
                }
            }

            using (var streamWriter = new StreamWriter(this.promptPath, false, Encoding.UTF8))
            {
                streamWriter.Write(rtPrompt.Text);
                streamWriter.Flush();
                streamWriter.Close();
                Process.Start("explorer.exe", $"/select,\"{this.promptPath}\"");
            }
        }
        public string strTempFile = "";
        private void btnLoadData_Click(object sender, EventArgs e)
        {
            //Load boxVox,mesh, or AABBs
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "BIM-based fire hazard files|*.fireRiskData;*.firelitevox;*.fireRiskAABB|csv file|*.csv";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var extension = Path.GetExtension(ofd.FileName);
                switch (extension)
                {
                    case ".csv":
                        this.WbsPath = ofd.FileName;
                        break;
                    default:
                        this.MatPath = ofd.FileName;
                        break;
                }
            }
            //extract file
            if (this.MatPath != null)
            {
                ////create a temporay file
                strTempFile = Path.GetTempFileName();
                using (var oldFile = new ZipFile(this.MatPath))
                {
                    ZipEntry targetEntry = null;
                    using (var newFileStream = new ZipOutputStream(File.Create(strTempFile)))
                    {
                        foreach (ZipEntry file in oldFile)
                        {
                            var fileName = file.Name;
                            if (fileName == "materials.csv")
                            {
                                targetEntry = file;
                                continue;
                            }
                            // copy file
                            newFileStream.PutNextEntry(new ZipEntry(fileName));
                            using (Stream entryStream = oldFile.GetInputStream(file))
                            {
                                entryStream.CopyTo(newFileStream);
                            }
                        }
                    }
                    if (targetEntry != null)
                    {
                        byte[] originalData;
                        using (MemoryStream tempMs = new MemoryStream())
                        {
                            using (var tarFileStream = oldFile.GetInputStream(targetEntry))
                            {
                                tarFileStream.CopyTo(tempMs);
                            }
                            originalData = tempMs.ToArray();
                        }
                        //convert data to string
                        string csvMatFile = Encoding.Default.GetString(originalData);
                        this.dtMat = LLMCSVHelper.CSV2Table(csvMatFile);
                        this.dgvMat.DataSource = dtMat;
                        //for test
                        //File.Delete(strTempFile);
                    }
                }
            }
            //get wbs
            if (this.WbsPath != null)
            {
                using (var sw = new StreamReader(this.WbsPath, Encoding.Default))
                {
                    string strCSVContent = sw.ReadToEnd();
                    this.dtWBS = LLMCSVHelper.CSV2Table(strCSVContent);
                    this.dgvWBS.DataSource = dtWBS;
                }
            }
        }

        private void btnFillByOuterLLM_Click(object sender, EventArgs e)
        {
            try
            {
                var curTab = tbDataPreview.SelectedTab;
                DataTable targetTable = null;
                List<int> data2Fill = new List<int>();
                if (curTab.Text == "Material")
                {
                    targetTable = dtMat;
                    data2Fill = new List<int>() { 0, 2, 3 };
                }
                else
                {
                    targetTable = dtWBS;
                    data2Fill = new List<int>() { 0, 7, 8, 9, 10 };
                }
                var helper = new LLMCSVHelper();
                var resultFromOtherLLM = rtOtherLLMResponse.Text;
                helper.RestoreDataTable(targetTable, resultFromOtherLLM, data2Fill);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            var curTab = tbDataPreview.SelectedTab;
            DataTable targetTable = null;
            List<int> dataRef = new List<int>();
            List<int> data2Fill = new List<int>();
            if(curTab.Text =="Material")
            {
                targetTable = dtMat;
                dataRef = new List<int>() { 0, 1 };
                data2Fill = new List<int>() { 0, 2, 3 };
            }
            else
            {
                targetTable = dtWBS;
                dataRef = new List<int>() { 0, 1,2 };
                data2Fill = new List<int>() { 0, 7, 8,9,10 };
            }
            var helper = new LLMCSVHelper();
            this.Prompt= rtPrompt.Text;
            var promptFull = helper.GetFullPrompt(this.Prompt, targetTable, dataRef, data2Fill);
            rtLLMResponse.Text = promptFull;
        }

        private void btnSaveResult_Click(object sender, EventArgs e)
        {
            //update mat
            if (!string.IsNullOrEmpty(strTempFile))
            {
                // 1. 准备要添加的 CSV 数据
                string strData = LLMCSVHelper.table2CSV(dtMat);
                byte[] byteData = Encoding.Default.GetBytes(strData);
                string entryName = "materials.csv";

                // 2. 用 ZipFile 打开现有的压缩包
                using (ZipFile zipFile = new ZipFile(this.MatPath))
                {
                    // 开始更新会话
                    zipFile.BeginUpdate();

                    // 如果已存在同名文件，可以先删除（实现覆盖效果）
                    ZipEntry existingEntry = zipFile.GetEntry(entryName);
                    if (existingEntry != null)
                    {
                        zipFile.Delete(existingEntry);
                    }

                    // 将内存中的 byte[] 添加为新条目
                    zipFile.Add(new StaticDiskDataSource(byteData), entryName);

                    // 提交更新（写回原文件）
                    zipFile.CommitUpdate();
                }

                // 无需再手动替换文件，因为 ZipFile 已直接修改了 this.MatPath
            }
            //update wbs
            if (!string.IsNullOrEmpty( this.WbsPath))
            {
                using(var streamWriter =new StreamWriter(this.WbsPath,false,Encoding.Default))
                {
                    string strData = LLMCSVHelper.table2CSV(dtWBS);
                    streamWriter.Write(strData);
                    streamWriter.Flush();
                }
            }
            MessageBox.Show("Save successfully!");
        }
    }
    public class LLMConfigData
    {
        public string Key { get; set; }
        public string ModelName { get; set; }
        public string EndPoint { get; set; }

    }

    public class StaticDiskDataSource : IStaticDataSource
    {
        private readonly byte[] _data;

        public StaticDiskDataSource(byte[] data)
        {
            _data = data;
        }

        public Stream GetSource()
        {
            return new MemoryStream(_data);
        }
    }
}
