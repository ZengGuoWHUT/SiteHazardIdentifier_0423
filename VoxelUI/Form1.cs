using ClassLibrary1;
using RevitVoxelzation;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Web;

namespace VoxelUI
{
    public partial class Form1 : Form
    {
        public string modelPath = @"C:\Users\ipmpg\Desktop\WhiteWall.txt";
        private int voxSize = 200;
        private int voxHeight = 1;
        private NamedPipeClientStream pipeClient=null;
        private string filePath = string.Empty;
        private string saveFilepath = string.Empty;
        public Form1(string filePath,string voxelSize,string voxelHeight,string saveFilepath)
        {
            InitializeComponent();
            this. filePath = filePath;
            this.saveFilepath = saveFilepath;
            if(filePath!=string.Empty)
            {
                this.voxSize=int.Parse(voxelSize);
                this.voxHeight=int.Parse(voxelHeight);
                modelPath = filePath;
                this.Text = modelPath;
                //构建一个命名管道
                pipeClient = new NamedPipeClientStream(".", "MyServer", PipeDirection.InOut, PipeOptions.Asynchronous);
            }
        }

        private  async void Form1_LoadAsync(object sender, EventArgs e)
        {
            if(this.filePath!=string.Empty)
            {
                await pipeClient.ConnectAsync();
            }

            CreateBoxElement();
            //CreateVoxelElement();
        }

        private async void CreateBoxElement()
        {
            var progresser = new Progress<int>((c) =>
            {
                this.progressBar1.Value = c;
                this.Text = $"{c}个已经完成";
            });
            var boxUtils = new BoxBuilder(modelPath);
            await Task.Yield();
            //将三角形个数传递给Server端
            if(pipeClient!=null)
            {
                using (var writer = new StreamWriter(pipeClient, Encoding.Default, 1024))
                {
                    await writer.WriteLineAsync(boxUtils.NumTriangles.ToString());
                    writer.Flush();
                }
            }
            
            
            this.progressBar1.Value = 0;
            this.progressBar1.Maximum = boxUtils.NumElements;
            Stopwatch sw = Stopwatch.StartNew();
            //var boxElems = await Task.Run(() => boxUtils.GenerateBoxElementParallel(100, 1, progresser));
            
            var boxElems = new List<LightWeightVoxelElement>() { Capacity =boxUtils.NumElements};
            await foreach(var elem in boxUtils.GenerateBoxElemnsAsync(voxSize, voxHeight, progresser))
            {
                boxElems.Add(elem);
            }
           
            sw.Stop();
            MessageBox.Show($"done,time elapsed:" + sw.Elapsed.TotalSeconds + "s");
            if (saveFilepath == string.Empty) //不用保存，尝试将数据传给Server，在服务器端处理
            {
                if (pipeClient != null)//命名管道在用
                {
                    JsonSerializerOptions options = new JsonSerializerOptions();
                    options.ReferenceHandler = System.Text.Json.
                        Serialization.ReferenceHandler.IgnoreCycles;//忽略循环引用
                    StreamWriter writer = new StreamWriter(pipeClient) { AutoFlush = false }; ;
                    Task tsk = Task.Run(async () =>
                    {
                        foreach (var boxElem in boxElems)
                        {
                            string jsonElem = JsonSerializer.Serialize(boxElem, options);
                            await writer.WriteLineAsync(jsonElem);
                        }
                        await writer.FlushAsync();
                    });
                    await tsk;
                    pipeClient.Flush();
                    pipeClient.Close();
                    pipeClient.Dispose();
                    this.Close();
                }
            }
            else //尝试保存数据到saveFilePath
            {
                using (StreamWriter saver = new StreamWriter(saveFilepath, false, Encoding.Default))
                {
                    saver.WriteLine(Vec3.Zero.ToString());
                    saver.WriteLine(this.voxSize.ToString());
                    //var elems = CreateMeshElement(meshPath).ToList();
                    foreach (var liteVe in boxElems)
                    {
                        saver.WriteLine(liteVe.ElementId);
                        List<int> boxSize = new List<int>();
                        foreach (var box in liteVe.Boxes)
                        {
                            boxSize.Add(box.Min.Col);
                            boxSize.Add(box.Min.Row);
                            boxSize.Add(box.Min.Layer);
                            boxSize.Add(box.Max.Col);
                            boxSize.Add(box.Max.Row);
                            boxSize.Add(box.Max.Layer);
                        }
                        saver.WriteLine(string.Join(",", boxSize));
                    }
                    saver.Flush();
                    saver.Close();
                }
                if (this.pipeClient != null && this.pipeClient.IsConnected)//命名管道在用
                {
                    pipeClient.Flush();
                    pipeClient.Close();
                    pipeClient.Dispose();
                }
                this.Close();
            }
        }
        private async void CreateVoxelElement()
        {
            var progresser = new Progress<int>((c) =>
            {
                this.progressBar1.Value = c;
                this.Text = $"{c}个已经完成";
            });
            var boxUtils = new BoxBuilder(modelPath);
            await Task.Yield();
            this.progressBar1.Value = 0;
            this.progressBar1.Maximum = boxUtils.NumElements;
            Stopwatch sw = Stopwatch.StartNew();
           
            var voxElems = new List<VoxelElement>() { Capacity = boxUtils.NumElements };
            int i = 0;
            int numEleems = boxUtils.NumElements;
            int reportThreeshold = (int)Math.Ceiling((double)numEleems / 100);
            await foreach (var elem in boxUtils.GenerateVoxelElementsAsync(voxSize, voxHeight))
            {
                voxElems.Add(elem);
                i++;
                if(i%reportThreeshold==0 || i==numEleems) 
                    ((IProgress<int>)progresser).Report(i);
                
            }

            sw.Stop();
            MessageBox.Show($"done,time elapsed:" + sw.Elapsed.TotalSeconds + "s");
            if(saveFilepath ==string.Empty) //不用保存，尝试将数据传给Server，在服务器端处理
            {
                if (pipeClient != null)//命名管道在用
                {

                    JsonSerializerOptions options = new JsonSerializerOptions();
                    options.ReferenceHandler = System.Text.Json.
                        Serialization.ReferenceHandler.IgnoreCycles;//忽略循环引用
                    StreamWriter writer = new StreamWriter(pipeClient) { AutoFlush = false }; ;
                    Task tsk = Task.Run(async () =>
                    {
                        foreach (var vox in voxElems)
                        {
                            string jsonElem = JsonSerializer.Serialize(vox, options);
                            await writer.WriteLineAsync(jsonElem);
                        }
                        await writer.FlushAsync();
                    });
                    await tsk;
                    pipeClient.Flush();
                    pipeClient.Close();
                    pipeClient.Dispose();
                    this.Close();
                }
            }
            else //尝试保存数据到saveFilePath
            {
                using(StreamWriter saver=new StreamWriter(saveFilepath,false,Encoding.Default))
                {
                    saver.WriteLine(Vec3.Zero.ToString());
                    saver.WriteLine(this.voxSize.ToString());
                    //var elems = CreateMeshElement(meshPath).ToList();
                    foreach (var ve in voxElems)
                    {

                        saver.WriteLine(ve.ElementId);
                        int[] strVoxelData = new int[ve.Voxels.Count * 4];
                        for (int j = 0; j < ve.Voxels.Count; j++)
                        {
                            var vox = ve.Voxels[j];
                            var pointer = j * 4;
                            strVoxelData[pointer] = vox.ColIndex;
                            strVoxelData[pointer + 1] = vox.RowIndex;
                            strVoxelData[pointer + 2] = (int)vox.BottomElevation;
                            strVoxelData[pointer + 3] = (int)vox.TopElevation;
                        }
                        saver.WriteLine(string.Join(",", strVoxelData));
                    }
                    saver.Flush();
                    saver.Close();
                }
                if (this.pipeClient != null)//命名管道在用
                {
                    pipeClient.Flush();
                    pipeClient.Close();
                    pipeClient.Dispose();
                    this.Close();
                }
            }
        }
    }
}
