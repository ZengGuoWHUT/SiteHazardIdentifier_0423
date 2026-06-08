namespace LLMFireDataHelper
{
    partial class FrmMain
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnLoadLLMInfo = new System.Windows.Forms.Button();
            this.btnSaveLLMInfo = new System.Windows.Forms.Button();
            this.txtModel = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.txtKey = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.txtEndPt = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.rtPrompt = new System.Windows.Forms.RichTextBox();
            this.btnLoadPrompt = new System.Windows.Forms.Button();
            this.btnSavePrompt = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.btnExport = new System.Windows.Forms.Button();
            this.tbDataPreview = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.dgvMat = new System.Windows.Forms.DataGridView();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.dgvWBS = new System.Windows.Forms.DataGridView();
            this.btnLoadData = new System.Windows.Forms.Button();
            this.btnFillByOuterLLM = new System.Windows.Forms.Button();
            this.btnRunLLM = new System.Windows.Forms.Button();
            this.btnSaveResult = new System.Windows.Forms.Button();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.rtLLMResponse = new System.Windows.Forms.RichTextBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.rtOtherLLMResponse = new System.Windows.Forms.RichTextBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.tbDataPreview.SuspendLayout();
            this.tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMat)).BeginInit();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvWBS)).BeginInit();
            this.groupBox4.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.btnLoadLLMInfo);
            this.groupBox1.Controls.Add(this.btnSaveLLMInfo);
            this.groupBox1.Controls.Add(this.txtModel);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.txtKey);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.txtEndPt);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Location = new System.Drawing.Point(12, 25);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(879, 181);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "LLMConfig";
            // 
            // btnLoadLLMInfo
            // 
            this.btnLoadLLMInfo.Location = new System.Drawing.Point(198, 131);
            this.btnLoadLLMInfo.Name = "btnLoadLLMInfo";
            this.btnLoadLLMInfo.Size = new System.Drawing.Size(184, 40);
            this.btnLoadLLMInfo.TabIndex = 2;
            this.btnLoadLLMInfo.Text = "Load";
            this.btnLoadLLMInfo.UseVisualStyleBackColor = true;
            this.btnLoadLLMInfo.Click += new System.EventHandler(this.btnLoadLLMInfo_Click);
            // 
            // btnSaveLLMInfo
            // 
            this.btnSaveLLMInfo.Location = new System.Drawing.Point(9, 131);
            this.btnSaveLLMInfo.Name = "btnSaveLLMInfo";
            this.btnSaveLLMInfo.Size = new System.Drawing.Size(184, 40);
            this.btnSaveLLMInfo.TabIndex = 2;
            this.btnSaveLLMInfo.Text = "Save";
            this.btnSaveLLMInfo.UseVisualStyleBackColor = true;
            this.btnSaveLLMInfo.Click += new System.EventHandler(this.btnSaveLLMInfo_Click);
            // 
            // txtModel
            // 
            this.txtModel.Location = new System.Drawing.Point(101, 24);
            this.txtModel.Name = "txtModel";
            this.txtModel.Size = new System.Drawing.Size(646, 28);
            this.txtModel.TabIndex = 1;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(10, 27);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(53, 18);
            this.label3.TabIndex = 0;
            this.label3.Text = "Model";
            // 
            // txtKey
            // 
            this.txtKey.Location = new System.Drawing.Point(101, 97);
            this.txtKey.Name = "txtKey";
            this.txtKey.PasswordChar = '*';
            this.txtKey.Size = new System.Drawing.Size(646, 28);
            this.txtKey.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 100);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(89, 18);
            this.label2.TabIndex = 0;
            this.label2.Text = "txtAPIKey";
            // 
            // txtEndPt
            // 
            this.txtEndPt.Location = new System.Drawing.Point(101, 58);
            this.txtEndPt.Name = "txtEndPt";
            this.txtEndPt.Size = new System.Drawing.Size(646, 28);
            this.txtEndPt.TabIndex = 1;
            this.txtEndPt.Text = "txtEndPoint";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 66);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(80, 18);
            this.label1.TabIndex = 0;
            this.label1.Text = "EndPoint";
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.rtPrompt);
            this.groupBox2.Controls.Add(this.btnLoadPrompt);
            this.groupBox2.Controls.Add(this.btnSavePrompt);
            this.groupBox2.Location = new System.Drawing.Point(12, 212);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(879, 301);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Prompt Setting";
            // 
            // rtPrompt
            // 
            this.rtPrompt.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.rtPrompt.Location = new System.Drawing.Point(9, 30);
            this.rtPrompt.Name = "rtPrompt";
            this.rtPrompt.Size = new System.Drawing.Size(864, 219);
            this.rtPrompt.TabIndex = 3;
            this.rtPrompt.Text = "";
            // 
            // btnLoadPrompt
            // 
            this.btnLoadPrompt.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnLoadPrompt.Location = new System.Drawing.Point(198, 255);
            this.btnLoadPrompt.Name = "btnLoadPrompt";
            this.btnLoadPrompt.Size = new System.Drawing.Size(184, 40);
            this.btnLoadPrompt.TabIndex = 2;
            this.btnLoadPrompt.Text = "Load";
            this.btnLoadPrompt.UseVisualStyleBackColor = true;
            this.btnLoadPrompt.Click += new System.EventHandler(this.btnLoadPrompt_Click);
            // 
            // btnSavePrompt
            // 
            this.btnSavePrompt.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnSavePrompt.Location = new System.Drawing.Point(9, 255);
            this.btnSavePrompt.Name = "btnSavePrompt";
            this.btnSavePrompt.Size = new System.Drawing.Size(184, 40);
            this.btnSavePrompt.TabIndex = 2;
            this.btnSavePrompt.Text = "Save";
            this.btnSavePrompt.UseVisualStyleBackColor = true;
            this.btnSavePrompt.Click += new System.EventHandler(this.btnSavePrompt_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox3.Controls.Add(this.btnExport);
            this.groupBox3.Controls.Add(this.tbDataPreview);
            this.groupBox3.Controls.Add(this.btnLoadData);
            this.groupBox3.Controls.Add(this.btnFillByOuterLLM);
            this.groupBox3.Controls.Add(this.btnRunLLM);
            this.groupBox3.Controls.Add(this.btnSaveResult);
            this.groupBox3.Location = new System.Drawing.Point(12, 519);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(879, 351);
            this.groupBox3.TabIndex = 1;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Load Model Data or WBS";
            // 
            // btnExport
            // 
            this.btnExport.Location = new System.Drawing.Point(138, 27);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(171, 40);
            this.btnExport.TabIndex = 5;
            this.btnExport.Text = "Generate Prompts";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // tbDataPreview
            // 
            this.tbDataPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbDataPreview.Controls.Add(this.tabPage1);
            this.tbDataPreview.Controls.Add(this.tabPage2);
            this.tbDataPreview.Location = new System.Drawing.Point(6, 73);
            this.tbDataPreview.Name = "tbDataPreview";
            this.tbDataPreview.SelectedIndex = 0;
            this.tbDataPreview.Size = new System.Drawing.Size(872, 272);
            this.tbDataPreview.TabIndex = 4;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.dgvMat);
            this.tabPage1.Location = new System.Drawing.Point(4, 28);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(864, 240);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Material";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // dgvMat
            // 
            this.dgvMat.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvMat.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvMat.Location = new System.Drawing.Point(3, 3);
            this.dgvMat.Name = "dgvMat";
            this.dgvMat.RowHeadersWidth = 62;
            this.dgvMat.RowTemplate.Height = 30;
            this.dgvMat.Size = new System.Drawing.Size(858, 234);
            this.dgvMat.TabIndex = 3;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.dgvWBS);
            this.tabPage2.Location = new System.Drawing.Point(4, 28);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(864, 240);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "WBS";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // dgvWBS
            // 
            this.dgvWBS.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvWBS.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvWBS.Location = new System.Drawing.Point(3, 3);
            this.dgvWBS.Name = "dgvWBS";
            this.dgvWBS.RowHeadersWidth = 62;
            this.dgvWBS.RowTemplate.Height = 30;
            this.dgvWBS.Size = new System.Drawing.Size(858, 234);
            this.dgvWBS.TabIndex = 0;
            // 
            // btnLoadData
            // 
            this.btnLoadData.Location = new System.Drawing.Point(8, 27);
            this.btnLoadData.Name = "btnLoadData";
            this.btnLoadData.Size = new System.Drawing.Size(124, 40);
            this.btnLoadData.TabIndex = 2;
            this.btnLoadData.Text = "Load";
            this.btnLoadData.UseVisualStyleBackColor = true;
            this.btnLoadData.Click += new System.EventHandler(this.btnLoadData_Click);
            // 
            // btnFillByOuterLLM
            // 
            this.btnFillByOuterLLM.Location = new System.Drawing.Point(483, 27);
            this.btnFillByOuterLLM.Name = "btnFillByOuterLLM";
            this.btnFillByOuterLLM.Size = new System.Drawing.Size(191, 40);
            this.btnFillByOuterLLM.TabIndex = 2;
            this.btnFillByOuterLLM.Text = "Fill from other LLM";
            this.btnFillByOuterLLM.UseVisualStyleBackColor = true;
            this.btnFillByOuterLLM.Click += new System.EventHandler(this.btnFillByOuterLLM_Click);
            // 
            // btnRunLLM
            // 
            this.btnRunLLM.Location = new System.Drawing.Point(315, 27);
            this.btnRunLLM.Name = "btnRunLLM";
            this.btnRunLLM.Size = new System.Drawing.Size(162, 40);
            this.btnRunLLM.TabIndex = 2;
            this.btnRunLLM.Text = "Run by API";
            this.btnRunLLM.UseVisualStyleBackColor = true;
            this.btnRunLLM.Click += new System.EventHandler(this.button2_Click);
            // 
            // btnSaveResult
            // 
            this.btnSaveResult.Location = new System.Drawing.Point(680, 27);
            this.btnSaveResult.Name = "btnSaveResult";
            this.btnSaveResult.Size = new System.Drawing.Size(145, 40);
            this.btnSaveResult.TabIndex = 2;
            this.btnSaveResult.Text = "Save";
            this.btnSaveResult.UseVisualStyleBackColor = true;
            this.btnSaveResult.Click += new System.EventHandler(this.btnSaveResult_Click);
            // 
            // groupBox4
            // 
            this.groupBox4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox4.Controls.Add(this.rtLLMResponse);
            this.groupBox4.Location = new System.Drawing.Point(897, 25);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(516, 436);
            this.groupBox4.TabIndex = 2;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Full Prompt";
            // 
            // rtLLMResponse
            // 
            this.rtLLMResponse.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtLLMResponse.Location = new System.Drawing.Point(3, 24);
            this.rtLLMResponse.Name = "rtLLMResponse";
            this.rtLLMResponse.Size = new System.Drawing.Size(510, 409);
            this.rtLLMResponse.TabIndex = 0;
            this.rtLLMResponse.Text = "";
            // 
            // groupBox5
            // 
            this.groupBox5.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox5.Controls.Add(this.rtOtherLLMResponse);
            this.groupBox5.Location = new System.Drawing.Point(897, 467);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(517, 406);
            this.groupBox5.TabIndex = 2;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "LLM Response";
            // 
            // rtOtherLLMResponse
            // 
            this.rtOtherLLMResponse.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtOtherLLMResponse.Location = new System.Drawing.Point(3, 24);
            this.rtOtherLLMResponse.Name = "rtOtherLLMResponse";
            this.rtOtherLLMResponse.Size = new System.Drawing.Size(511, 379);
            this.rtOtherLLMResponse.TabIndex = 0;
            this.rtOtherLLMResponse.Text = "";
            // 
            // FrmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1426, 885);
            this.Controls.Add(this.groupBox5);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Name = "FrmMain";
            this.Text = "LLMAssistant";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.tbDataPreview.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvMat)).EndInit();
            this.tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvWBS)).EndInit();
            this.groupBox4.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox txtKey;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtEndPt;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnLoadLLMInfo;
        private System.Windows.Forms.Button btnSaveLLMInfo;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.RichTextBox rtPrompt;
        private System.Windows.Forms.Button btnLoadPrompt;
        private System.Windows.Forms.Button btnSavePrompt;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Button btnLoadData;
        private System.Windows.Forms.Button btnRunLLM;
        private System.Windows.Forms.TabControl tbDataPreview;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.DataGridView dgvMat;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.DataGridView dgvWBS;
        private System.Windows.Forms.Button btnSaveResult;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.RichTextBox rtLLMResponse;
        private System.Windows.Forms.TextBox txtModel;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.RichTextBox rtOtherLLMResponse;
        private System.Windows.Forms.Button btnFillByOuterLLM;
    }
}

