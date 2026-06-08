
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SiteHazardIdentifier
{
    public class LLMCSVHelper
    {
        private string colName { get; set; }

        public string GetFullPrompt(string systemPrompt, DataTable tableOriginal, List<int> columns2Remain, List<int> columnIndex2Fill)
        {
            List<string> colNames2Fill = new List<string>();
            foreach (var i in columnIndex2Fill)
            {
                colNames2Fill.Add(tableOriginal.Columns[i].ColumnName);
            }

            List<string> ColumnNames2Remain = new List<string>();
            foreach (var colIdx in columns2Remain)
            {
                ColumnNames2Remain.Add(tableOriginal.Columns[colIdx].ColumnName);
            }
            DataTable tableReference = tableOriginal.DefaultView.ToTable(false, ColumnNames2Remain.ToArray());
            string tableReference2String = table2CSV(tableReference);
            systemPrompt += "\r\n" + tableReference2String;
            //MessageBox.Show(systemPrompt);
            DataTable table2Fill = tableOriginal.DefaultView.ToTable(false, colNames2Fill.ToArray());
            //reset table2fill
            foreach (DataRow dr in table2Fill.Rows)
            {
                for (int j = 1; j <= table2Fill.Columns.Count - 1; j++)
                {
                    dr[j] = "";
                }
            }
            table2Fill.AcceptChanges();
            string table2FillString = table2CSV(table2Fill);
            systemPrompt += "\r\n 请完善:\r\n" + table2FillString;
            systemPrompt += "\r\n " + "**输出要求**：\r\n- 1.请以纯CSV格式输出，不需要任何额外格式、空行或解释。\r\n- 2、不要输出与CSV无关的任何解释说明。\r\n- 3、你的判断是保守的，仅仅根据给定数据判断，不要擅自揣测。对于无法确定的内容，请填写：N/A\r\n-4、严格保留所有换行符，禁用空白归一化。\r\n-5、每一行数据必须使用标准的回车换行（\\n）进行分隔。\r\n**禁止**\r\n- 1.用户CSV内容以外的额外的文本或说明\r\n- 2.CSV单元格内部出现逗号";
            return systemPrompt;
        }

        public void RestoreDataTable(DataTable tableOriginal, string strContents, List<int> columnIndex2Fill)
        {
            if (!string.IsNullOrEmpty(strContents))
            {
                var stringReader = new StringReader(strContents);
                //MessageBox.Show(answer.Item1);
                stringReader.ReadLine();
                string strContent = stringReader.ReadLine();
                int rowPointer = 0;
                while (strContent != null)
                {
                    if (strContent != string.Empty)//有的时候CSV会用空行间隔
                    {
                        string[] strItem = strContent.Split(',');
                        for (int i = 0; i < strItem.Length; i++)
                        {
                            if (columnIndex2Fill[i] == 0)
                            {
                                continue;
                            }
                            tableOriginal.Rows[rowPointer][columnIndex2Fill[i]] = strItem[i];
                        }
                        rowPointer += 1;
                    }
                    strContent = stringReader.ReadLine();
                }
            }
        }
        public async Task<Tuple<DataTable, int>> Response(ChatClient client, string systemPrompt, DataTable tableOriginal, List<int> columns2Remain, List<int> columnIndex2Fill)
        {
            try
            {
                var strSysMsg = GetFullPrompt(systemPrompt, tableOriginal, columns2Remain, columnIndex2Fill);
                var answer = client.CompleteChat(strSysMsg);
                var val = answer.Value.Content;
                var tokens = answer.Value.Usage.TotalTokenCount;
                string strContents = "";
                foreach (var v in val)
                {
                    strContents += v.Text;
                }
                int tokenTotal = 0;
                RestoreDataTable(tableOriginal, strContents, columnIndex2Fill);
                /*
                 * 
                if (!string.IsNullOrEmpty(strContents))
                {
                   
                    var stringReader = new StringReader(strContents);
                    //MessageBox.Show(answer.Item1);
                    stringReader.ReadLine();
                    string strContent = stringReader.ReadLine();
                    int rowPointer = 0;
                    while (strContent != null)
                    {
                        if(strContent!=string.Empty)//有的时候CSV会用空行间隔
                        {
                            string[] strItem = strContent.Split(',');
                            for (int i = 0; i < strItem.Length; i++)
                            {
                                if (columnIndex2Fill[i] == 0)
                                {
                                    continue;
                                }
                                tableOriginal.Rows[rowPointer][columnIndex2Fill[i]] = strItem[i];
                            }
                            rowPointer += 1;
                        }
                        strContent = stringReader.ReadLine();
                    }
                }
                */
                return new Tuple<DataTable, int>(tableOriginal, tokenTotal);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
                return null;
            }
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

        public static DataTable CSV2Table(string csvContent)
        {
            DataTable dt = new DataTable();
            using (var stringReader = new StringReader(csvContent))
            {
                string header = stringReader.ReadLine();
                foreach (var c in header.Split(','))
                {
                    dt.Columns.Add(c);
                }
                var item = stringReader.ReadLine();
                while (item != null)
                {
                    dt.Rows.Add(item.Split(','));
                    item = stringReader.ReadLine();
                }
            }
            return dt;
        }
    }
}
