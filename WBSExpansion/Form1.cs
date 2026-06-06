using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WBSExpansion
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            //load template file
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "csv files|*.csv";
            HashSet<string> elemIds = new   HashSet<string>();
            List<string[]> itemsInTemplate = new List<string[]>();
            int numWorks = 0;
            int numWorksDetail = 0;
            int prevWorkIdx = -1;
            var header = "";
            if (ofd.ShowDialog()==DialogResult.OK)
            {
                header = string.Empty;
                using (var sw=new StreamReader(ofd.FileName,Encoding.Default))
                {
                    header = sw.ReadLine();
                    while(sw.EndOfStream==false)
                    {
                        var content = sw.ReadLine().Split(',');
                        numWorksDetail += 1;
                        itemsInTemplate.Add(content);
                        var id = content[0];
                        var wkNum = id.Substring(1);
                        int intWkNum = 0;
                        if(!int.TryParse(wkNum,out intWkNum))
                        {
                            intWkNum = int.Parse(wkNum.Split('.')[0]);
                        }
                        if(prevWorkIdx!=intWkNum)
                        {
                            numWorks += 1;
                            prevWorkIdx = intWkNum;
                        }
                        var elemIdsLink = content[4].Split(';');
                        foreach(var elemId in elemIdsLink)
                        {
                            elemIds.Add(elemId);
                        }
                    }
                }
                //attatch work
                int i = 0;
                int copyTimes = int.Parse(txtCopyNum.Text);
                List<string>[] workIdLink2WBS = new List<string>[copyTimes*numWorks];
                foreach (var elemId in elemIds)
                {
                    int workId2Assign = i % (numWorks*copyTimes);
                    
                    if (workIdLink2WBS[workId2Assign] == null)
                    {
                        workIdLink2WBS[workId2Assign] = new List<string>() { elemId };
                    }
                    else
                    {
                        workIdLink2WBS[workId2Assign].Add(elemId);
                    }
                    i += 1;
                }
                // generate new wbs table
                string strNewPath = Path.Combine(Path.GetDirectoryName(ofd.FileName), Path.GetFileNameWithoutExtension(ofd.FileName) + "-" + (copyTimes * numWorksDetail).ToString() + ".csv");
                var elemArr = elemIds.ToList();
                using (var sw = new StreamWriter(strNewPath, false, Encoding.Default))
                {
                    sw.WriteLine(header);
                    for(int pt=0;pt<= copyTimes * numWorksDetail - 1;pt++)
                    {
                        int baseWorkIdx = pt % numWorksDetail;
                        int repearTime = pt / numWorksDetail;
                        string[] workBaseInfo = itemsInTemplate[baseWorkIdx].ToArray();
                        string wkIdx = workBaseInfo[0];
                        int intWkIdx = GetIndex(wkIdx,out var rem);
                        int elemGroupIdx = intWkIdx-1+repearTime*numWorks;
                        string validElemLinked = string.Join(";", workIdLink2WBS[elemGroupIdx]);
                        int newIdx = intWkIdx + repearTime * numWorks;
                        string strNewIdx = ("A" + newIdx.ToString() + rem).Trim();
                        workBaseInfo[0] = strNewIdx;
                        workBaseInfo[4] = validElemLinked;
                        sw.WriteLine(string.Join(",", workBaseInfo));
                    }
                }
                Process.Start("explorer.exe", $"/select,\"{strNewPath}\"");
            }
            
        }
        private int GetIndex(string workId,out string rem)
        {
            var wkNum = workId.Substring(1);
            int intWkNum = 0;
            rem = string.Empty;
            if (!int.TryParse(wkNum, out intWkNum))
            {
                intWkNum = int.Parse(wkNum.Split('.')[0]);
                rem = wkNum.Substring(wkNum.IndexOf('.'));
            }
            return intWkNum;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
