namespace SiteHazardIdentifier
{
    partial class FrmFireHazard
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.txtTimeBuffer = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.txtRangeFire = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.dgvWBS = new System.Windows.Forms.DataGridView();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.dgvMat = new System.Windows.Forms.DataGridView();
            this.txtInfo = new System.Windows.Forms.TextBox();
            this.btnLoadVoxel = new System.Windows.Forms.Button();
            this.btnLoadWBS = new System.Windows.Forms.Button();
            this.prog = new System.Windows.Forms.ProgressBar();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.btnGetMesh = new System.Windows.Forms.Button();
            this.btnVoxelize = new System.Windows.Forms.Button();
            this.txtVoxSize = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            this.dgvChunks = new System.Windows.Forms.DataGridView();
            this.btnLLM = new System.Windows.Forms.Button();
            this.btnAnalysis2 = new System.Windows.Forms.Button();
            this.Visualize = new System.Windows.Forms.GroupBox();
            this.btnElemTemporalTest = new System.Windows.Forms.Button();
            this.btnGenAABB = new System.Windows.Forms.Button();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvWBS)).BeginInit();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMat)).BeginInit();
            this.groupBox4.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox6.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvChunks)).BeginInit();
            this.Visualize.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.txtTimeBuffer);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.txtRangeFire);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Location = new System.Drawing.Point(12, 80);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(876, 75);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Parameters";
            // 
            // txtTimeBuffer
            // 
            this.txtTimeBuffer.Location = new System.Drawing.Point(574, 34);
            this.txtTimeBuffer.Name = "txtTimeBuffer";
            this.txtTimeBuffer.Size = new System.Drawing.Size(70, 28);
            this.txtTimeBuffer.TabIndex = 1;
            this.txtTimeBuffer.Text = "7";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(343, 37);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(224, 18);
            this.label7.TabIndex = 0;
            this.label7.Text = "Hazardous Time Buffer(d)";
            // 
            // txtRangeFire
            // 
            this.txtRangeFire.Location = new System.Drawing.Point(254, 34);
            this.txtRangeFire.Name = "txtRangeFire";
            this.txtRangeFire.Size = new System.Drawing.Size(70, 28);
            this.txtRangeFire.TabIndex = 1;
            this.txtRangeFire.Text = "10000";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 37);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(242, 18);
            this.label1.TabIndex = 0;
            this.label1.Text = "Fire protection Ranges(mm)";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.tabControl1);
            this.groupBox3.Location = new System.Drawing.Point(9, 161);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(882, 443);
            this.groupBox3.TabIndex = 2;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "WBS";
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Location = new System.Drawing.Point(6, 27);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(876, 410);
            this.tabControl1.TabIndex = 1;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.dgvWBS);
            this.tabPage1.Location = new System.Drawing.Point(4, 28);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(868, 378);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "WBS";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // dgvWBS
            // 
            this.dgvWBS.AllowUserToAddRows = false;
            this.dgvWBS.AllowUserToDeleteRows = false;
            this.dgvWBS.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvWBS.ColumnHeadersHeight = 34;
            this.dgvWBS.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvWBS.Location = new System.Drawing.Point(3, 3);
            this.dgvWBS.Name = "dgvWBS";
            this.dgvWBS.RowHeadersWidth = 62;
            this.dgvWBS.RowTemplate.Height = 30;
            this.dgvWBS.Size = new System.Drawing.Size(862, 372);
            this.dgvWBS.TabIndex = 0;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.dgvMat);
            this.tabPage2.Location = new System.Drawing.Point(4, 28);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(868, 378);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Materials";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // dgvMat
            // 
            this.dgvMat.AllowUserToAddRows = false;
            this.dgvMat.AllowUserToDeleteRows = false;
            this.dgvMat.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvMat.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvMat.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvMat.Location = new System.Drawing.Point(3, 3);
            this.dgvMat.Name = "dgvMat";
            this.dgvMat.RowHeadersWidth = 62;
            this.dgvMat.RowTemplate.Height = 30;
            this.dgvMat.Size = new System.Drawing.Size(862, 372);
            this.dgvMat.TabIndex = 0;
            // 
            // txtInfo
            // 
            this.txtInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtInfo.Location = new System.Drawing.Point(3, 24);
            this.txtInfo.Multiline = true;
            this.txtInfo.Name = "txtInfo";
            this.txtInfo.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtInfo.Size = new System.Drawing.Size(873, 107);
            this.txtInfo.TabIndex = 1;
            // 
            // btnLoadVoxel
            // 
            this.btnLoadVoxel.Location = new System.Drawing.Point(9, 610);
            this.btnLoadVoxel.Name = "btnLoadVoxel";
            this.btnLoadVoxel.Size = new System.Drawing.Size(143, 41);
            this.btnLoadVoxel.TabIndex = 3;
            this.btnLoadVoxel.Text = "Load Voxel";
            this.btnLoadVoxel.UseVisualStyleBackColor = true;
            this.btnLoadVoxel.Click += new System.EventHandler(this.btnLoadVoxel_Click);
            // 
            // btnLoadWBS
            // 
            this.btnLoadWBS.Location = new System.Drawing.Point(161, 610);
            this.btnLoadWBS.Name = "btnLoadWBS";
            this.btnLoadWBS.Size = new System.Drawing.Size(143, 41);
            this.btnLoadWBS.TabIndex = 3;
            this.btnLoadWBS.Text = "Load WBS Data";
            this.btnLoadWBS.UseVisualStyleBackColor = true;
            this.btnLoadWBS.Click += new System.EventHandler(this.btnLoadWBS_Click);
            // 
            // prog
            // 
            this.prog.Location = new System.Drawing.Point(12, 657);
            this.prog.Name = "prog";
            this.prog.Size = new System.Drawing.Size(874, 38);
            this.prog.TabIndex = 4;
            this.prog.Click += new System.EventHandler(this.prog_Click);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.btnGenAABB);
            this.groupBox4.Controls.Add(this.btnGetMesh);
            this.groupBox4.Controls.Add(this.btnVoxelize);
            this.groupBox4.Controls.Add(this.txtVoxSize);
            this.groupBox4.Controls.Add(this.label6);
            this.groupBox4.Location = new System.Drawing.Point(11, 12);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(876, 68);
            this.groupBox4.TabIndex = 5;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Voxelization parameteers";
            // 
            // btnGetMesh
            // 
            this.btnGetMesh.Location = new System.Drawing.Point(307, 22);
            this.btnGetMesh.Name = "btnGetMesh";
            this.btnGetMesh.Size = new System.Drawing.Size(148, 30);
            this.btnGetMesh.TabIndex = 3;
            this.btnGetMesh.Text = "Get Meshes";
            this.btnGetMesh.UseVisualStyleBackColor = true;
            this.btnGetMesh.Click += new System.EventHandler(this.btnGetMesh_Click);
            // 
            // btnVoxelize
            // 
            this.btnVoxelize.Location = new System.Drawing.Point(461, 22);
            this.btnVoxelize.Name = "btnVoxelize";
            this.btnVoxelize.Size = new System.Drawing.Size(169, 30);
            this.btnVoxelize.TabIndex = 2;
            this.btnVoxelize.Text = "Voxelize";
            this.btnVoxelize.UseVisualStyleBackColor = true;
            this.btnVoxelize.Click += new System.EventHandler(this.btnVoxelize_Click);
            // 
            // txtVoxSize
            // 
            this.txtVoxSize.Location = new System.Drawing.Point(147, 27);
            this.txtVoxSize.Name = "txtVoxSize";
            this.txtVoxSize.Size = new System.Drawing.Size(154, 28);
            this.txtVoxSize.TabIndex = 1;
            this.txtVoxSize.Text = "200";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(7, 34);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(134, 18);
            this.label6.TabIndex = 0;
            this.label6.Text = "Voxel size(mm)";
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.txtInfo);
            this.groupBox5.Location = new System.Drawing.Point(9, 701);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(879, 134);
            this.groupBox5.TabIndex = 6;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Info";
            // 
            // groupBox6
            // 
            this.groupBox6.Controls.Add(this.dgvChunks);
            this.groupBox6.Location = new System.Drawing.Point(897, 12);
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.Size = new System.Drawing.Size(591, 738);
            this.groupBox6.TabIndex = 7;
            this.groupBox6.TabStop = false;
            this.groupBox6.Text = "Result";
            this.groupBox6.Enter += new System.EventHandler(this.groupBox6_Enter);
            // 
            // dgvChunks
            // 
            this.dgvChunks.AllowUserToAddRows = false;
            this.dgvChunks.AllowUserToDeleteRows = false;
            this.dgvChunks.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvChunks.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvChunks.Location = new System.Drawing.Point(3, 24);
            this.dgvChunks.Name = "dgvChunks";
            this.dgvChunks.RowHeadersWidth = 62;
            this.dgvChunks.RowTemplate.Height = 30;
            this.dgvChunks.Size = new System.Drawing.Size(585, 711);
            this.dgvChunks.TabIndex = 0;
            // 
            // btnLLM
            // 
            this.btnLLM.Location = new System.Drawing.Point(7, 27);
            this.btnLLM.Name = "btnLLM";
            this.btnLLM.Size = new System.Drawing.Size(177, 41);
            this.btnLLM.TabIndex = 1;
            this.btnLLM.Text = "Visualize";
            this.btnLLM.UseVisualStyleBackColor = true;
            this.btnLLM.Click += new System.EventHandler(this.btnLLM_Click);
            // 
            // btnAnalysis2
            // 
            this.btnAnalysis2.Location = new System.Drawing.Point(310, 610);
            this.btnAnalysis2.Name = "btnAnalysis2";
            this.btnAnalysis2.Size = new System.Drawing.Size(281, 41);
            this.btnAnalysis2.TabIndex = 9;
            this.btnAnalysis2.Text = "Fire Hazard Identification";
            this.btnAnalysis2.UseVisualStyleBackColor = true;
            this.btnAnalysis2.Click += new System.EventHandler(this.btnAnalysis2_Click);
            // 
            // Visualize
            // 
            this.Visualize.Controls.Add(this.btnElemTemporalTest);
            this.Visualize.Controls.Add(this.btnLLM);
            this.Visualize.Location = new System.Drawing.Point(903, 756);
            this.Visualize.Name = "Visualize";
            this.Visualize.Size = new System.Drawing.Size(585, 76);
            this.Visualize.TabIndex = 5;
            this.Visualize.TabStop = false;
            this.Visualize.Text = "Visualize";
            // 
            // btnElemTemporalTest
            // 
            this.btnElemTemporalTest.Location = new System.Drawing.Point(190, 27);
            this.btnElemTemporalTest.Name = "btnElemTemporalTest";
            this.btnElemTemporalTest.Size = new System.Drawing.Size(177, 41);
            this.btnElemTemporalTest.TabIndex = 4;
            this.btnElemTemporalTest.Text = "Temporal Check";
            this.btnElemTemporalTest.UseVisualStyleBackColor = true;
            this.btnElemTemporalTest.Click += new System.EventHandler(this.btnElemTemporalTest_Click);
            // 
            // btnGenAABB
            // 
            this.btnGenAABB.Location = new System.Drawing.Point(636, 22);
            this.btnGenAABB.Name = "btnGenAABB";
            this.btnGenAABB.Size = new System.Drawing.Size(169, 30);
            this.btnGenAABB.TabIndex = 4;
            this.btnGenAABB.Text = "Generate AABB";
            this.btnGenAABB.UseVisualStyleBackColor = true;
            this.btnGenAABB.Click += new System.EventHandler(this.btnGenAABB_Click);
            // 
            // FrmFireHazard
            // 
            this.ClientSize = new System.Drawing.Size(1502, 850);
            this.Controls.Add(this.Visualize);
            this.Controls.Add(this.btnAnalysis2);
            this.Controls.Add(this.groupBox5);
            this.Controls.Add(this.groupBox6);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.prog);
            this.Controls.Add(this.btnLoadWBS);
            this.Controls.Add(this.btnLoadVoxel);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Name = "FrmFireHazard";
            this.Text = "  ";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FrmFireHazard_FormClosing);
            this.Load += new System.EventHandler(this.FrmFireHazard_Load);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvWBS)).EndInit();
            this.tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvMat)).EndInit();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.groupBox6.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvChunks)).EndInit();
            this.Visualize.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox txtRangeFire;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.DataGridView dgvWBS;
        private System.Windows.Forms.Button btnLoadVoxel;
        private System.Windows.Forms.Button btnLoadWBS;
        private System.Windows.Forms.ProgressBar prog;
        private System.Windows.Forms.TextBox txtInfo;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Button btnVoxelize;
        private System.Windows.Forms.TextBox txtVoxSize;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Button btnGetMesh;
        private System.Windows.Forms.GroupBox groupBox6;
        private System.Windows.Forms.Button btnLLM;
        private System.Windows.Forms.DataGridView dgvChunks;
        private System.Windows.Forms.TextBox txtTimeBuffer;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.DataGridView dgvMat;
        private System.Windows.Forms.Button btnAnalysis2;
        private System.Windows.Forms.GroupBox Visualize;
        private System.Windows.Forms.Button btnElemTemporalTest;
        private System.Windows.Forms.Button btnGenAABB;
    }
}