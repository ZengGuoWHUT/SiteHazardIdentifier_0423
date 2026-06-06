using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ResultChecker
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Please select the ground truth";
            ofd.Filter = "csv files|*.csv";
            DataTable dtGroundTruth = null;
            DataTable dtTestData = null;
            Dictionary<string,(double,int)> strGroundTruth = new Dictionary<string, (double, int)>();
            Dictionary<string, (double,int)> strTestData = new Dictionary<string, (double, int)>();
            if(ofd.ShowDialog ()==DialogResult.OK)
            {
                dtGroundTruth= LoadCSV(ofd.FileName, out strGroundTruth);
            }
            ofd.Title = "Please select the test data";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                dtTestData= LoadCSV(ofd.FileName, out strTestData);
            }
            dgvGT.DataSource = dtGroundTruth;
            dgvT.DataSource = dtTestData;
            Dictionary<string, (double, int)> dicFN = new Dictionary<string, (double, int)>();
            Dictionary<string, (double, int)> dicFP = new Dictionary<string, (double, int)>();
            List<string> initFN = strGroundTruth.Keys.Except(strTestData.Keys).ToList();
            DataTable dtResult = new DataTable();
            dtResult.Columns.Add("No");
            dtResult.Columns.Add("Data In Ground Truth");
            dtResult.Columns.Add("Data in Test");
            dtResult.Columns.Add("Distance");
            dtResult.Columns.Add("Distance in Test");
            dtResult.Columns.Add("Level in Ground truth");
            dtResult.Columns.Add("Level in Test");
            dtResult.Columns.Add("Error Type");
            foreach (var item in initFN)
            {
                var truthData = strGroundTruth[item];
               
                dtResult.Rows.Add(dtResult.Rows.Count +1,  item,"Null" ,truthData.Item1, "Null", truthData.Item2, "Null",  "FN");
            }
            List<string> initFP = strTestData.Keys.Except(strGroundTruth.Keys).ToList();
            foreach (var item in initFP)
            {
                var testData = strTestData[item];
                dtResult.Rows.Add(dtResult.Rows.Count + 1, "Null", item,   "Null",testData.Item1,  "Null",testData.Item2, "FP");
            }
            var keyCommon= strGroundTruth.Keys.Intersect(strTestData.Keys).ToList();
            foreach(var key in keyCommon)
            {
                var truthData = strGroundTruth[key];
                var testData = strTestData[key];
                if(truthData.Item2 >testData.Item2)//FN
                {
                    dtResult.Rows.Add(dtResult.Rows.Count + 1, key,key, truthData.Item1, testData.Item1, truthData.Item2, testData.Item2, "FN");
                }
                else if(truthData.Item2 <testData.Item2)//FP
                {
                    dtResult.Rows.Add(dtResult.Rows.Count + 1, key,key, truthData.Item1, testData.Item1, truthData.Item2, testData.Item2, "FN");
                }
                else
                {

                }
                    
            }
            dgvResult.DataSource = dtResult;
        }

        private DataTable LoadCSV(string path,out Dictionary<string,(double,int)> dic)
        {
            DataTable result = new DataTable();
            dic = new Dictionary<string, (double,int)>();
            using(var sw =new StreamReader(path,Encoding.Default))
            {
                string header = sw.ReadLine();
                foreach(var item in header.Split(','))
                {
                    result.Columns.Add(item);
                }
                while(!sw.EndOfStream)
                {
                    string content = sw.ReadLine();
                    var split = content.Split(',');
                    result.Rows.Add(split);
                    string elemId = split[1];
                    double dblDIstance = double.MaxValue;
                    double.TryParse(split[2],out dblDIstance);
                    string ignitionSrc = (split[3]== "" ? "Empty" : split[3]);
                    string combActivity = (split[4]== "" ? "Empty" : split[4]);
                    string combMat = (split[5]== "" ? "Empty" : split[4]);
                    int level = 0;
                    switch(split[6])
                    {
                        case "Extreme":
                            level = 3;
                            break;
                        case "High":
                            level = 2;
                            break;
                        case "Medium":
                            level = 1;
                            break;
                        default:
                            level = 0;
                            break;
                    }
                    string key = string.Join(";", elemId, ignitionSrc, combActivity, combMat);
                    var value = (dblDIstance, level);
                    dic.Add(key, value);
                }
            }
            return result;
        }
    }
}
