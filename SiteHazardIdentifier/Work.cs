using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
namespace SiteHazardIdentifier
{
    public class Work
    {
        public Work()
        {

        }
        public string Id { get; set; }
        public string Name {  get; set; }
        public List<string> Resources { get; set; }
        private DateTime start { get; set; }

        public DateTime Get_Start()
        {
            return this.start;
        }
        
       public void Set_Start(DateTime start)
        {
            this.start= start;
        }
        private DateTime finish { get; set; }

        public DateTime Get_Finish(bool considerBuffer)
        {
            if (considerBuffer)
            {
                return this.finish + this.TimeBuffer;
            }
            else
            {
                return this.finish;
            }
        }
        public void Set_Finish(DateTime finish)
        {
            this.finish= finish;
        }
        public List<string> ElementIds { get; set; }
        public double[] WorkRadius { get; set; } = { 5000, 0 };
        public double[] AffectRadius { get; set; } = {10000, 0};
        public double FireProtectionRange { get; set; } = 0;
        public WorkType WorkType { get; set; } = WorkType.Construct;
        public WorkStatus Status { get; set; } = WorkStatus.Unstart;
      
        public bool Combustible {  get; set; } = false; 
        public bool EmitSparks {  get; set; } = false;
        public bool EmitGas { get; internal set; }=false;

        public TimeSpan TimeBuffer{ get; set; } = TimeSpan.Zero;
        public string GetWorkDescription()
        {
            return $"{Name}," +
                $"资源:{string.Join(",", this.Resources)}";
                
        }
        public string GetWorkDescriptionIncludeTime()
        {
            return $"{Name}," +
                $"资源:{string.Join(",", this.Resources)},"+
                $"开始时间{this.start.ToShortDateString()},"+
                $"结束时间{this.finish.ToShortDateString()};";

        }
    }

    public class ValidDummyWork:Work
    {
        
        public override bool Equals(object obj)
        {
            if(obj.GetType()!=typeof(ValidDummyWork))
            {
                return false;
            }
            else
            {
                var other = obj as ValidDummyWork;
                return this.Name == other.Name && this.Get_Start() == other.Get_Start() && this.Get_Finish(false) == other.Get_Finish(false);
            }
        }
        public override int GetHashCode()
        {
            unchecked // 允许整数溢出，利用溢出混合位
            {
                int hash = 17;
                hash = hash * 31 + (Name?.GetHashCode() ?? 0);
                hash = hash * 31 + this.Get_Start().GetHashCode();
                hash = hash * 31 + this.Get_Finish(false).GetHashCode();
                return hash;
            }
        }
    }

    public enum WorkType
    {
        Construct=0,//开始前物体不存在，完工后物体存在
        Demolish=1,//开始前物体存在，完工后物体不存在
        Other=2//不影响物体的状态
    }
    public enum WorkStatus
    {
        Unstart=0,
        Working=1,
        Finished=2,
    }
}
