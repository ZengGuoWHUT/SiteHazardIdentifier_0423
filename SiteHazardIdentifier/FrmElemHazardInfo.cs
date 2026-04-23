using Autodesk.Revit.DB;
using Braincase.GanttChart;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Form = System.Windows.Forms.Form;

namespace SiteHazardIdentifier
{
    public partial class FrmElemHazardInfo : Form
    {
        private _4DElement elem;
        public FrmElemHazardInfo(_4DElement elem2Show)
        {
            this.elem = elem2Show;
            InitializeComponent();
        }

        private void FrmElemHazardInfo_Load(object sender, EventArgs e)
        {
            int combIdx = 0;
            List<string> hazardDescription = new List<string>();
            var highestLevel = CombinationHazardLevel.Low;
            List<Work> fires=new List<Work>();
            dgvCombo.AllowUserToAddRows = false;
            dgvCombo.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
            dgvCombo.Columns.Add("0","No");
            dgvCombo.Columns.Add("1","Ignition Activity");
            dgvCombo.Columns.Add("2","Combustible Activitis");
            dgvCombo.Columns.Add("3","Combustible Materials");
            dgvCombo.Columns.Add("4","Hazard Level");
            foreach (var combo in this.elem.Combinations)
            {
                hazardDescription.Add(combIdx.ToString() + "\r\n" + combo.Get_Description());
                if (highestLevel < combo.HazardLevel)
                {
                    highestLevel = combo.HazardLevel;
                }
                if(combo.IngntionSource!=null)
                {
                    fires.Add(combo.IngntionSource);
                }
                string strIgnitionName = combo.IngntionSource == null ? "N/A" :$"{combo.IngntionSource.Id}_{combo.IngntionSource.Name}";
                string strCombWork = "N/A";
                List<string> combWorks = new List<string>();
                foreach(var cw in combo.CombustibleWorks)
                {
                    combWorks.Add($"{cw.Id}_{cw.Name}");
                }
                if(combWorks.Count > 0)
                {
                    strCombWork=string.Join(";",combWorks);
                }
                string strCombMat = "N/A";
                List<string> combMat = new List<string>();
                foreach (var cm in combo.CombustibleMaterials)
                {
                    combMat.Add($"{cm.Id}_{cm.Name}");
                }
                if (combMat.Count > 0)
                {
                    strCombMat = string.Join(";", combMat);
                }

                int rowIdx = dgvCombo.Rows.Add(combIdx, strIgnitionName, strCombWork, strCombMat, combo.HazardLevel);
                DataGridViewRow row = dgvCombo.Rows[rowIdx];
                switch(combo.HazardLevel)
                {
                    case CombinationHazardLevel.Low:
                        row.DefaultCellStyle.BackColor = System.Drawing.Color.Green;
                        break;
                    case CombinationHazardLevel.Medium:
                        row.DefaultCellStyle.BackColor = System.Drawing.Color.Blue;
                        break;
                    case CombinationHazardLevel.High:
                        row.DefaultCellStyle.BackColor = System.Drawing.Color.Yellow;
                        break;
                    case CombinationHazardLevel.Extreme:
                        row.DefaultCellStyle.BackColor = System.Drawing.Color.Red;
                        break;

                }
                combIdx += 1;
            }
              
            var elemPhaseString = "Element Phases:\r\n" + string.Join("\r\n", elem.GetElementPhaseString());
            var comboText = elemPhaseString + "\r\n" + "Fire Hazard Combos:\r\n" + string.Join("\r\n", hazardDescription);
            this.txtElemInfo.Text = comboText;

           
            DateTime dtStart = DateTime.MaxValue;
            DateTime dtFininsh=DateTime.MinValue;
            foreach(var wk in elem.Works)
            {
                dtStart = (wk.Get_Start() < dtStart ? wk.Get_Start(): dtStart);
                dtFininsh =(wk.Get_Finish(true)>dtFininsh?wk.Get_Finish(true):dtFininsh);
            }
            foreach (var fire in fires)
            {
               
                var fireSt = fire.Get_Start();
                var fireEd = fire.Get_Finish(true);
                if (fireSt < dtStart)
                    dtStart = fireSt;
                if (fireEd > dtFininsh)
                    dtFininsh = fireEd;
            }
            dtStart -= new TimeSpan(7, 0, 0, 0);
            dtFininsh += new TimeSpan(7, 0, 0, 0);
            //add quiescent phases as a work
            ProjectManager wbsManager = new ProjectManager() { Start = dtStart };
            ProjectManager fireManager = new ProjectManager() { Start = dtStart };
            wbsManager.Start = dtStart;
            int i = 0;
            List<(DateTime start, DateTime end, string)> taskData = new List<(DateTime, DateTime, string)>();
            bool elemCombustible = elem.Materials.Any(c => c.Combustible);
            foreach (var phase in elem.ElemStausPeroid)
            {
                if(phase.Item3==ElementStatus.Quiescent)
                {
                    string strName = $"{phase.Item3}{i}  ";
                    if (elemCombustible)
                    {
                        strName += "-Combustible!";
                    }
                    var st = phase.Item1;
                    if(st==DateTime.MinValue)
                    {
                        st = dtStart;
                    }
                    var ed = phase.Item2;
                    if(ed==DateTime.MaxValue)
                    {
                        ed = dtFininsh;
                    }
                    taskData.Add((st, ed, strName));
                     i += 1;
                }
            }
            foreach (var wbs in elem.Works)
            {
                var st = wbs.Get_Start() ;
                var ed = wbs.Get_Finish(true) ;
                var name = $"{wbs.Id}_{wbs.Name}";
                if (wbs.Combustible || wbs.EmitGas)
                    name += "-Combustible!";
                taskData.Add((st, ed, name));

            }
           
            foreach(var data in taskData.OrderBy(c => c.start))
            {
                var st0 = data.start;
                var end0= data.end;
                var name = data.Item3;
                var st = st0 - dtStart;
                var ed = end0 - dtStart;
                var tsk = new MyTask(wbsManager);
                wbsManager.Add(tsk);
                tsk.Name = name;
                tsk.SetStart(st);
                tsk.SetFinish(ed);
            }


            foreach(var fire in fires)
            {
                var st = fire.Get_Start() - dtStart;
                var ed = fire.Get_Finish(true) - dtStart;
                var name = $"{fire.Id}_{fire.Name}";
                var tsk = new MyTask(fireManager);
                fireManager.Add(tsk);
                tsk.SetStart(st);
                tsk.SetFinish(ed);
                tsk.Name = name;
            }
            gcWBS.GetFireOverlay(fireManager);
            gcWBS.Init(wbsManager);

            //get ignition 
            
            
            
            gcIgnition.Init(fireManager);
            
        }
    }
    public class MyTask: Braincase.GanttChart.Task
    {
        private ProjectManager Manager { get; set; }
        public MyTask(ProjectManager manager)
        {
            this.Manager= manager;
        }
        public void SetStart(TimeSpan val)
        {
            Manager.SetStart(this, val);
        }
        public void SetFinish(TimeSpan val)
        {
            Manager.SetEnd(this, val);
        }
    }
}
