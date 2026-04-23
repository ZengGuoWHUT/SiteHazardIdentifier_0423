namespace SiteHazardIdentifier
{
    partial class FrmElemHazardInfo
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
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.txtElemInfo = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.dgvCombo = new System.Windows.Forms.DataGridView();
            this.gcIgnition = new Braincase.GanttChart.Chart();
            this.gcWBS = new Braincase.GanttChart.Chart();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCombo)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.gcWBS);
            this.groupBox2.Location = new System.Drawing.Point(472, 318);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(776, 307);
            this.groupBox2.TabIndex = 0;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "WBS";
            // 
            // groupBox3
            // 
            this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox3.Controls.Add(this.gcIgnition);
            this.groupBox3.Location = new System.Drawing.Point(472, 631);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(776, 314);
            this.groupBox3.TabIndex = 0;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Ignition Activities";
            // 
            // groupBox5
            // 
            this.groupBox5.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.groupBox5.Controls.Add(this.txtElemInfo);
            this.groupBox5.Location = new System.Drawing.Point(11, 318);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(455, 627);
            this.groupBox5.TabIndex = 1;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Description";
            // 
            // txtElemInfo
            // 
            this.txtElemInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtElemInfo.Location = new System.Drawing.Point(3, 24);
            this.txtElemInfo.Multiline = true;
            this.txtElemInfo.Name = "txtElemInfo";
            this.txtElemInfo.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtElemInfo.Size = new System.Drawing.Size(449, 600);
            this.txtElemInfo.TabIndex = 0;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.dgvCombo);
            this.groupBox1.Location = new System.Drawing.Point(11, 11);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(1244, 301);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Combo Info";
            // 
            // dgvCombo
            // 
            this.dgvCombo.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvCombo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvCombo.Location = new System.Drawing.Point(3, 24);
            this.dgvCombo.Name = "dgvCombo";
            this.dgvCombo.RowHeadersWidth = 62;
            this.dgvCombo.RowTemplate.Height = 30;
            this.dgvCombo.Size = new System.Drawing.Size(1238, 274);
            this.dgvCombo.TabIndex = 0;
            // 
            // gcIgnition
            // 
            this.gcIgnition.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gcIgnition.Location = new System.Drawing.Point(3, -38);
            this.gcIgnition.Margin = new System.Windows.Forms.Padding(0);
            this.gcIgnition.Name = "gcIgnition";
            this.gcIgnition.Size = new System.Drawing.Size(770, 349);
            this.gcIgnition.TabIndex = 0;
            // 
            // gcWBS
            // 
            this.gcWBS.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gcWBS.Location = new System.Drawing.Point(3, 24);
            this.gcWBS.Margin = new System.Windows.Forms.Padding(0);
            this.gcWBS.Name = "gcWBS";
            this.gcWBS.Size = new System.Drawing.Size(770, 280);
            this.gcWBS.TabIndex = 0;
            // 
            // FrmElemHazardInfo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1260, 957);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.groupBox5);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Name = "FrmElemHazardInfo";
            this.Text = "FrmElemHazardInfo";
            this.Load += new System.EventHandler(this.FrmElemHazardInfo_Load);
            this.groupBox2.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvCombo)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.GroupBox groupBox2;
        private Braincase.GanttChart.Chart gcWBS;
        private System.Windows.Forms.GroupBox groupBox3;
        private Braincase.GanttChart.Chart gcIgnition;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.TextBox txtElemInfo;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.DataGridView dgvCombo;
    }
}