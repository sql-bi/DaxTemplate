namespace TestDaxTemplates
{
    partial class ApplyDate
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.txtServer = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btnUpdate = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.txtDatabase = new System.Windows.Forms.TextBox();
            this.btnGenDate = new System.Windows.Forms.Button();
            this.txtDax = new System.Windows.Forms.TextBox();
            this.btnReadConfig = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.chkCustomTemplate = new System.Windows.Forms.CheckBox();
            this.txtCustomTemplate = new System.Windows.Forms.TextBox();
            this.btnReadTemplate = new System.Windows.Forms.Button();
            this.btnGenHolidaysDax = new System.Windows.Forms.Button();
            this.btnUpdateHolidays = new System.Windows.Forms.Button();
            this.txtCustomTranslation = new System.Windows.Forms.TextBox();
            this.chkCustomTranslation = new System.Windows.Forms.CheckBox();
            this.btnMeasureTemplate = new System.Windows.Forms.Button();
            this.txtConfig = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.txtPath = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.fileSystemWatcher = new System.IO.FileSystemWatcher();
            this.comboTemplates = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.btnApplyTemplate = new System.Windows.Forms.Button();
            this.btnCopyDebug = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.fileSystemWatcher)).BeginInit();
            this.SuspendLayout();
            // 
            // txtServer
            // 
            this.txtServer.Location = new System.Drawing.Point(131, 22);
            this.txtServer.Name = "txtServer";
            this.txtServer.Size = new System.Drawing.Size(359, 31);
            this.txtServer.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(35, 25);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(65, 25);
            this.label1.TabIndex = 1;
            this.label1.Text = "Server:";
            // 
            // btnUpdate
            // 
            this.btnUpdate.Location = new System.Drawing.Point(844, 101);
            this.btnUpdate.Name = "btnUpdate";
            this.btnUpdate.Size = new System.Drawing.Size(233, 37);
            this.btnUpdate.TabIndex = 2;
            this.btnUpdate.Text = "Update date";
            this.btnUpdate.UseVisualStyleBackColor = true;
            this.btnUpdate.Click += new System.EventHandler(this.Update_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(35, 65);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(90, 25);
            this.label2.TabIndex = 3;
            this.label2.Text = "Database:";
            // 
            // txtDatabase
            // 
            this.txtDatabase.Location = new System.Drawing.Point(131, 62);
            this.txtDatabase.Name = "txtDatabase";
            this.txtDatabase.Size = new System.Drawing.Size(359, 31);
            this.txtDatabase.TabIndex = 4;
            // 
            // btnGenDate
            // 
            this.btnGenDate.Location = new System.Drawing.Point(1083, 101);
            this.btnGenDate.Name = "btnGenDate";
            this.btnGenDate.Size = new System.Drawing.Size(194, 37);
            this.btnGenDate.TabIndex = 5;
            this.btnGenDate.Text = "Generate DAX";
            this.btnGenDate.UseVisualStyleBackColor = true;
            this.btnGenDate.Click += new System.EventHandler(this.GenerateDax_Click);
            // 
            // txtDax
            // 
            this.txtDax.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtDax.Location = new System.Drawing.Point(193, 236);
            this.txtDax.Multiline = true;
            this.txtDax.Name = "txtDax";
            this.txtDax.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDax.Size = new System.Drawing.Size(1118, 465);
            this.txtDax.TabIndex = 6;
            // 
            // btnReadConfig
            // 
            this.btnReadConfig.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnReadConfig.Location = new System.Drawing.Point(35, 261);
            this.btnReadConfig.Name = "btnReadConfig";
            this.btnReadConfig.Size = new System.Drawing.Size(141, 41);
            this.btnReadConfig.TabIndex = 8;
            this.btnReadConfig.Text = "Read Config";
            this.btnReadConfig.UseVisualStyleBackColor = true;
            this.btnReadConfig.Click += new System.EventHandler(this.ReadConfig_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(131, 233);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(48, 25);
            this.label4.TabIndex = 11;
            this.label4.Text = "DAX";
            // 
            // chkCustomTemplate
            // 
            this.chkCustomTemplate.AutoSize = true;
            this.chkCustomTemplate.Checked = true;
            this.chkCustomTemplate.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCustomTemplate.Location = new System.Drawing.Point(665, 22);
            this.chkCustomTemplate.Name = "chkCustomTemplate";
            this.chkCustomTemplate.Size = new System.Drawing.Size(210, 29);
            this.chkCustomTemplate.TabIndex = 12;
            this.chkCustomTemplate.Text = "Use custom template:";
            this.chkCustomTemplate.UseVisualStyleBackColor = true;
            // 
            // txtCustomTemplate
            // 
            this.txtCustomTemplate.Location = new System.Drawing.Point(881, 25);
            this.txtCustomTemplate.Name = "txtCustomTemplate";
            this.txtCustomTemplate.Size = new System.Drawing.Size(396, 31);
            this.txtCustomTemplate.TabIndex = 13;
            this.txtCustomTemplate.Text = "..\\..\\..\\Templates\\DateTemplate-05.json";
            // 
            // btnReadTemplate
            // 
            this.btnReadTemplate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnReadTemplate.Location = new System.Drawing.Point(35, 308);
            this.btnReadTemplate.Name = "btnReadTemplate";
            this.btnReadTemplate.Size = new System.Drawing.Size(141, 41);
            this.btnReadTemplate.TabIndex = 14;
            this.btnReadTemplate.Text = "Read Template";
            this.btnReadTemplate.UseVisualStyleBackColor = true;
            this.btnReadTemplate.Click += new System.EventHandler(this.ReadTemplate_Click);
            // 
            // btnGenHolidaysDax
            // 
            this.btnGenHolidaysDax.Location = new System.Drawing.Point(1083, 144);
            this.btnGenHolidaysDax.Name = "btnGenHolidaysDax";
            this.btnGenHolidaysDax.Size = new System.Drawing.Size(194, 37);
            this.btnGenHolidaysDax.TabIndex = 15;
            this.btnGenHolidaysDax.Text = "Gen. Holidays DAX";
            this.btnGenHolidaysDax.UseVisualStyleBackColor = true;
            this.btnGenHolidaysDax.Click += new System.EventHandler(this.GenerateHolidays_Click);
            // 
            // btnUpdateHolidays
            // 
            this.btnUpdateHolidays.Location = new System.Drawing.Point(844, 144);
            this.btnUpdateHolidays.Name = "btnUpdateHolidays";
            this.btnUpdateHolidays.Size = new System.Drawing.Size(233, 37);
            this.btnUpdateHolidays.TabIndex = 16;
            this.btnUpdateHolidays.Text = "Update holidays";
            this.btnUpdateHolidays.UseVisualStyleBackColor = true;
            this.btnUpdateHolidays.Click += new System.EventHandler(this.UpdateHolidays_Click);
            // 
            // txtCustomTranslation
            // 
            this.txtCustomTranslation.Location = new System.Drawing.Point(881, 62);
            this.txtCustomTranslation.Name = "txtCustomTranslation";
            this.txtCustomTranslation.Size = new System.Drawing.Size(396, 31);
            this.txtCustomTranslation.TabIndex = 17;
            this.txtCustomTranslation.Text = "..\\..\\..\\Templates\\DateLocalization-04.json";
            // 
            // chkCustomTranslation
            // 
            this.chkCustomTranslation.AutoSize = true;
            this.chkCustomTranslation.Location = new System.Drawing.Point(665, 64);
            this.chkCustomTranslation.Name = "chkCustomTranslation";
            this.chkCustomTranslation.Size = new System.Drawing.Size(167, 29);
            this.chkCustomTranslation.TabIndex = 18;
            this.chkCustomTranslation.Text = "Use translations:";
            this.chkCustomTranslation.UseVisualStyleBackColor = true;
            // 
            // btnMeasureTemplate
            // 
            this.btnMeasureTemplate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnMeasureTemplate.Location = new System.Drawing.Point(35, 355);
            this.btnMeasureTemplate.Name = "btnMeasureTemplate";
            this.btnMeasureTemplate.Size = new System.Drawing.Size(141, 41);
            this.btnMeasureTemplate.TabIndex = 19;
            this.btnMeasureTemplate.Text = "Measure Tem.";
            this.btnMeasureTemplate.UseVisualStyleBackColor = true;
            this.btnMeasureTemplate.Click += new System.EventHandler(this.MeasureTemplate_Click);
            // 
            // txtConfig
            // 
            this.txtConfig.Location = new System.Drawing.Point(131, 107);
            this.txtConfig.Name = "txtConfig";
            this.txtConfig.Size = new System.Drawing.Size(454, 31);
            this.txtConfig.TabIndex = 21;
            this.txtConfig.Text = "..\\..\\..\\Templates\\Config-05.template.json";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(35, 110);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(69, 25);
            this.label5.TabIndex = 20;
            this.label5.Text = "Config:";
            // 
            // txtPath
            // 
            this.txtPath.Location = new System.Drawing.Point(131, 150);
            this.txtPath.Name = "txtPath";
            this.txtPath.Size = new System.Drawing.Size(701, 31);
            this.txtPath.TabIndex = 23;
            this.txtPath.Text = "C:\\Users\\MarcoRusso\\source\\repos\\sql-bi\\DaxTemplate\\TestDaxTemplates\\Templates\\";
            this.txtPath.TextChanged += new System.EventHandler(this.Path_TextChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(35, 153);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(50, 25);
            this.label3.TabIndex = 22;
            this.label3.Text = "Path:";
            // 
            // fileSystemWatcher
            // 
            this.fileSystemWatcher.EnableRaisingEvents = true;
            this.fileSystemWatcher.Filter = "*.template.json";
            this.fileSystemWatcher.SynchronizingObject = this;
            this.fileSystemWatcher.Changed += new System.IO.FileSystemEventHandler(this.Watcher_Changed);
            this.fileSystemWatcher.Created += new System.IO.FileSystemEventHandler(this.Watcher_Created);
            this.fileSystemWatcher.Deleted += new System.IO.FileSystemEventHandler(this.Watcher_Deleted);
            this.fileSystemWatcher.Error += new System.IO.ErrorEventHandler(this.Watcher_Error);
            this.fileSystemWatcher.Renamed += new System.IO.RenamedEventHandler(this.Watcher_Renamed);
            // 
            // comboTemplates
            // 
            this.comboTemplates.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTemplates.FormattingEnabled = true;
            this.comboTemplates.Location = new System.Drawing.Point(130, 191);
            this.comboTemplates.Name = "comboTemplates";
            this.comboTemplates.Size = new System.Drawing.Size(455, 33);
            this.comboTemplates.TabIndex = 24;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(35, 194);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(87, 25);
            this.label6.TabIndex = 25;
            this.label6.Text = "Template:";
            // 
            // btnApplyTemplate
            // 
            this.btnApplyTemplate.Location = new System.Drawing.Point(602, 189);
            this.btnApplyTemplate.Name = "btnApplyTemplate";
            this.btnApplyTemplate.Size = new System.Drawing.Size(148, 34);
            this.btnApplyTemplate.TabIndex = 26;
            this.btnApplyTemplate.Text = "Apply Template";
            this.btnApplyTemplate.UseVisualStyleBackColor = true;
            this.btnApplyTemplate.Click += new System.EventHandler(this.ApplyTemplate_Click);
            // 
            // btnCopyDebug
            // 
            this.btnCopyDebug.Location = new System.Drawing.Point(494, 22);
            this.btnCopyDebug.Margin = new System.Windows.Forms.Padding(2);
            this.btnCopyDebug.Name = "btnCopyDebug";
            this.btnCopyDebug.Size = new System.Drawing.Size(90, 70);
            this.btnCopyDebug.TabIndex = 27;
            this.btnCopyDebug.Text = "Copy debug";
            this.btnCopyDebug.UseVisualStyleBackColor = true;
            this.btnCopyDebug.Click += new System.EventHandler(this.CopyDebug_Click);
            // 
            // ApplyDate
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1336, 736);
            this.Controls.Add(this.btnCopyDebug);
            this.Controls.Add(this.btnApplyTemplate);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.comboTemplates);
            this.Controls.Add(this.txtPath);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.txtConfig);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.btnMeasureTemplate);
            this.Controls.Add(this.chkCustomTranslation);
            this.Controls.Add(this.txtCustomTranslation);
            this.Controls.Add(this.btnUpdateHolidays);
            this.Controls.Add(this.btnGenHolidaysDax);
            this.Controls.Add(this.btnReadTemplate);
            this.Controls.Add(this.txtCustomTemplate);
            this.Controls.Add(this.chkCustomTemplate);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnReadConfig);
            this.Controls.Add(this.txtDax);
            this.Controls.Add(this.btnGenDate);
            this.Controls.Add(this.txtDatabase);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnUpdate);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtServer);
            this.Name = "ApplyDate";
            this.Text = "Power BI Date";
            this.Load += new System.EventHandler(this.ApplyDate_Load);
            ((System.ComponentModel.ISupportInitialize)(this.fileSystemWatcher)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private TextBox txtServer;
        private Label label1;
        private Button btnUpdate;
        private Label label2;
        private TextBox txtDatabase;
        private Button btnGenDate;
        private TextBox txtDax;
        private Button btnReadConfig;
        private Label label4;
        private CheckBox chkCustomTemplate;
        private TextBox txtCustomTemplate;
        private Button btnReadTemplate;
        private Button btnGenHolidaysDax;
        private Button btnUpdateHolidays;
        private TextBox txtCustomTranslation;
        private CheckBox chkCustomTranslation;
        private Button btnMeasureTemplate;
        private TextBox txtConfig;
        private Label label5;
        private TextBox txtPath;
        private Label label3;
        private FileSystemWatcher fileSystemWatcher;
        private Label label6;
        private ComboBox comboTemplates;
        private Button btnApplyTemplate;
        private Button btnCopyDebug;
    }
}