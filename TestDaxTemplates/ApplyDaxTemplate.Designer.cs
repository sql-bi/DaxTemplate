namespace TestDaxTemplates
{
    partial class ApplyDaxTemplate
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
            this.label2 = new System.Windows.Forms.Label();
            this.txtDatabase = new System.Windows.Forms.TextBox();
            this.txtDax = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.txtPath = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.fileSystemWatcher = new System.IO.FileSystemWatcher();
            this.comboTemplates = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.btnApplyTemplate = new System.Windows.Forms.Button();
            this.btnCopyDebug = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnMeasureTemplate = new System.Windows.Forms.Button();
            this.btnReadTemplate = new System.Windows.Forms.Button();
            this.btnReadConfig = new System.Windows.Forms.Button();
            this.txtConfig = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.chkCustomTemplate = new System.Windows.Forms.CheckBox();
            this.chkCustomTranslation = new System.Windows.Forms.CheckBox();
            this.txtCustomTranslation = new System.Windows.Forms.TextBox();
            this.btnUpdateHolidays = new System.Windows.Forms.Button();
            this.btnGenHolidaysDax = new System.Windows.Forms.Button();
            this.txtCustomTemplate = new System.Windows.Forms.TextBox();
            this.btnGenDate = new System.Windows.Forms.Button();
            this.btnUpdate = new System.Windows.Forms.Button();
            this.btnPackage = new System.Windows.Forms.Button();
            this.btnPreviewTemplate = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.fileSystemWatcher)).BeginInit();
            this.panel1.SuspendLayout();
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
            // txtDax
            // 
            this.txtDax.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtDax.Location = new System.Drawing.Point(35, 327);
            this.txtDax.Multiline = true;
            this.txtDax.Name = "txtDax";
            this.txtDax.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDax.Size = new System.Drawing.Size(1497, 679);
            this.txtDax.TabIndex = 6;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label4.Location = new System.Drawing.Point(35, 286);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(50, 25);
            this.label4.TabIndex = 11;
            this.label4.Text = "DAX";
            // 
            // txtPath
            // 
            this.txtPath.Location = new System.Drawing.Point(131, 108);
            this.txtPath.Name = "txtPath";
            this.txtPath.Size = new System.Drawing.Size(719, 31);
            this.txtPath.TabIndex = 23;
            this.txtPath.Text = "C:\\Users\\MarcoRusso\\source\\repos\\sql-bi\\DaxTemplate\\TestDaxTemplates\\Templates\\";
            this.txtPath.TextChanged += new System.EventHandler(this.Path_TextChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(35, 111);
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
            this.comboTemplates.Location = new System.Drawing.Point(130, 149);
            this.comboTemplates.Name = "comboTemplates";
            this.comboTemplates.Size = new System.Drawing.Size(531, 33);
            this.comboTemplates.TabIndex = 24;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(35, 152);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(87, 25);
            this.label6.TabIndex = 25;
            this.label6.Text = "Template:";
            // 
            // btnApplyTemplate
            // 
            this.btnApplyTemplate.Location = new System.Drawing.Point(684, 149);
            this.btnApplyTemplate.Name = "btnApplyTemplate";
            this.btnApplyTemplate.Size = new System.Drawing.Size(166, 38);
            this.btnApplyTemplate.TabIndex = 26;
            this.btnApplyTemplate.Text = "Apply Template";
            this.btnApplyTemplate.UseVisualStyleBackColor = true;
            this.btnApplyTemplate.Click += new System.EventHandler(this.ApplyTemplate_Click);
            // 
            // btnCopyDebug
            // 
            this.btnCopyDebug.Location = new System.Drawing.Point(494, 22);
            this.btnCopyDebug.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnCopyDebug.Name = "btnCopyDebug";
            this.btnCopyDebug.Size = new System.Drawing.Size(90, 70);
            this.btnCopyDebug.TabIndex = 27;
            this.btnCopyDebug.Text = "Copy debug";
            this.btnCopyDebug.UseVisualStyleBackColor = true;
            this.btnCopyDebug.Click += new System.EventHandler(this.CopyDebug_Click);
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.panel1.Controls.Add(this.btnMeasureTemplate);
            this.panel1.Controls.Add(this.btnReadTemplate);
            this.panel1.Controls.Add(this.btnReadConfig);
            this.panel1.Controls.Add(this.txtConfig);
            this.panel1.Controls.Add(this.label5);
            this.panel1.Controls.Add(this.chkCustomTemplate);
            this.panel1.Controls.Add(this.chkCustomTranslation);
            this.panel1.Controls.Add(this.txtCustomTranslation);
            this.panel1.Controls.Add(this.btnUpdateHolidays);
            this.panel1.Controls.Add(this.btnGenHolidaysDax);
            this.panel1.Controls.Add(this.txtCustomTemplate);
            this.panel1.Controls.Add(this.btnGenDate);
            this.panel1.Controls.Add(this.btnUpdate);
            this.panel1.Location = new System.Drawing.Point(864, 25);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(658, 286);
            this.panel1.TabIndex = 28;
            // 
            // btnMeasureTemplate
            // 
            this.btnMeasureTemplate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnMeasureTemplate.Location = new System.Drawing.Point(433, 230);
            this.btnMeasureTemplate.Name = "btnMeasureTemplate";
            this.btnMeasureTemplate.Size = new System.Drawing.Size(194, 41);
            this.btnMeasureTemplate.TabIndex = 34;
            this.btnMeasureTemplate.Text = "Measure Template";
            this.btnMeasureTemplate.UseVisualStyleBackColor = true;
            // 
            // btnReadTemplate
            // 
            this.btnReadTemplate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnReadTemplate.Location = new System.Drawing.Point(194, 230);
            this.btnReadTemplate.Name = "btnReadTemplate";
            this.btnReadTemplate.Size = new System.Drawing.Size(233, 41);
            this.btnReadTemplate.TabIndex = 33;
            this.btnReadTemplate.Text = "Read Template";
            this.btnReadTemplate.UseVisualStyleBackColor = true;
            // 
            // btnReadConfig
            // 
            this.btnReadConfig.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnReadConfig.Location = new System.Drawing.Point(15, 230);
            this.btnReadConfig.Name = "btnReadConfig";
            this.btnReadConfig.Size = new System.Drawing.Size(167, 41);
            this.btnReadConfig.TabIndex = 32;
            this.btnReadConfig.Text = "Read Config";
            this.btnReadConfig.UseVisualStyleBackColor = true;
            // 
            // txtConfig
            // 
            this.txtConfig.Location = new System.Drawing.Point(231, 20);
            this.txtConfig.Name = "txtConfig";
            this.txtConfig.Size = new System.Drawing.Size(396, 31);
            this.txtConfig.TabIndex = 31;
            this.txtConfig.Text = "..\\..\\..\\Templates\\Config-01 - Standard.template.json";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(135, 23);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(69, 25);
            this.label5.TabIndex = 30;
            this.label5.Text = "Config:";
            // 
            // chkCustomTemplate
            // 
            this.chkCustomTemplate.AutoSize = true;
            this.chkCustomTemplate.Checked = true;
            this.chkCustomTemplate.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCustomTemplate.Location = new System.Drawing.Point(15, 66);
            this.chkCustomTemplate.Name = "chkCustomTemplate";
            this.chkCustomTemplate.Size = new System.Drawing.Size(210, 29);
            this.chkCustomTemplate.TabIndex = 24;
            this.chkCustomTemplate.Text = "Use custom template:";
            this.chkCustomTemplate.UseVisualStyleBackColor = true;
            // 
            // chkCustomTranslation
            // 
            this.chkCustomTranslation.AutoSize = true;
            this.chkCustomTranslation.Location = new System.Drawing.Point(15, 105);
            this.chkCustomTranslation.Name = "chkCustomTranslation";
            this.chkCustomTranslation.Size = new System.Drawing.Size(167, 29);
            this.chkCustomTranslation.TabIndex = 29;
            this.chkCustomTranslation.Text = "Use translations:";
            this.chkCustomTranslation.UseVisualStyleBackColor = true;
            // 
            // txtCustomTranslation
            // 
            this.txtCustomTranslation.Location = new System.Drawing.Point(231, 103);
            this.txtCustomTranslation.Name = "txtCustomTranslation";
            this.txtCustomTranslation.Size = new System.Drawing.Size(396, 31);
            this.txtCustomTranslation.TabIndex = 28;
            this.txtCustomTranslation.Text = "..\\..\\..\\Templates\\DateLocalization-04.json";
            // 
            // btnUpdateHolidays
            // 
            this.btnUpdateHolidays.Location = new System.Drawing.Point(194, 185);
            this.btnUpdateHolidays.Name = "btnUpdateHolidays";
            this.btnUpdateHolidays.Size = new System.Drawing.Size(233, 37);
            this.btnUpdateHolidays.TabIndex = 27;
            this.btnUpdateHolidays.Text = "Update holidays";
            this.btnUpdateHolidays.UseVisualStyleBackColor = true;
            // 
            // btnGenHolidaysDax
            // 
            this.btnGenHolidaysDax.Location = new System.Drawing.Point(433, 185);
            this.btnGenHolidaysDax.Name = "btnGenHolidaysDax";
            this.btnGenHolidaysDax.Size = new System.Drawing.Size(194, 37);
            this.btnGenHolidaysDax.TabIndex = 26;
            this.btnGenHolidaysDax.Text = "Gen. Holidays DAX";
            this.btnGenHolidaysDax.UseVisualStyleBackColor = true;
            // 
            // txtCustomTemplate
            // 
            this.txtCustomTemplate.Location = new System.Drawing.Point(231, 66);
            this.txtCustomTemplate.Name = "txtCustomTemplate";
            this.txtCustomTemplate.Size = new System.Drawing.Size(396, 31);
            this.txtCustomTemplate.TabIndex = 25;
            this.txtCustomTemplate.Text = "..\\..\\..\\Templates\\DateTemplate-04.json";
            // 
            // btnGenDate
            // 
            this.btnGenDate.Location = new System.Drawing.Point(433, 142);
            this.btnGenDate.Name = "btnGenDate";
            this.btnGenDate.Size = new System.Drawing.Size(194, 37);
            this.btnGenDate.TabIndex = 23;
            this.btnGenDate.Text = "Generate DAX";
            this.btnGenDate.UseVisualStyleBackColor = true;
            // 
            // btnUpdate
            // 
            this.btnUpdate.Location = new System.Drawing.Point(194, 142);
            this.btnUpdate.Name = "btnUpdate";
            this.btnUpdate.Size = new System.Drawing.Size(233, 37);
            this.btnUpdate.TabIndex = 22;
            this.btnUpdate.Text = "Update date";
            this.btnUpdate.UseVisualStyleBackColor = true;
            // 
            // btnPackage
            // 
            this.btnPackage.Location = new System.Drawing.Point(684, 255);
            this.btnPackage.Name = "btnPackage";
            this.btnPackage.Size = new System.Drawing.Size(166, 38);
            this.btnPackage.TabIndex = 29;
            this.btnPackage.Text = "Create Package";
            this.btnPackage.UseVisualStyleBackColor = true;
            this.btnPackage.Click += new System.EventHandler(this.CreatePackage_Click);
            // 
            // btnPreviewTemplate
            // 
            this.btnPreviewTemplate.Location = new System.Drawing.Point(684, 193);
            this.btnPreviewTemplate.Name = "btnPreviewTemplate";
            this.btnPreviewTemplate.Size = new System.Drawing.Size(166, 38);
            this.btnPreviewTemplate.TabIndex = 30;
            this.btnPreviewTemplate.Text = "Preview Template";
            this.btnPreviewTemplate.UseVisualStyleBackColor = true;
            this.btnPreviewTemplate.Click += new System.EventHandler(this.PreviewTemplate_Click);
            // 
            // ApplyDaxTemplate
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1548, 1030);
            this.Controls.Add(this.btnPreviewTemplate);
            this.Controls.Add(this.btnPackage);
            this.Controls.Add(this.btnCopyDebug);
            this.Controls.Add(this.btnApplyTemplate);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.comboTemplates);
            this.Controls.Add(this.txtPath);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.txtDax);
            this.Controls.Add(this.txtDatabase);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtServer);
            this.Controls.Add(this.panel1);
            this.Name = "ApplyDaxTemplate";
            this.Text = "Test DAX Templates";
            this.Load += new System.EventHandler(this.ApplyDate_Load);
            ((System.ComponentModel.ISupportInitialize)(this.fileSystemWatcher)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private TextBox txtServer;
        private Label label1;
        private Label label2;
        private TextBox txtDatabase;
        private TextBox txtDax;
        private Label label4;
        private TextBox txtPath;
        private Label label3;
        private FileSystemWatcher fileSystemWatcher;
        private Label label6;
        private ComboBox comboTemplates;
        private Button btnApplyTemplate;
        private Button btnCopyDebug;
        private Panel panel1;
        private Button btnMeasureTemplate;
        private Button btnReadTemplate;
        private Button btnReadConfig;
        private TextBox txtConfig;
        private Label label5;
        private CheckBox chkCustomTemplate;
        private CheckBox chkCustomTranslation;
        private TextBox txtCustomTranslation;
        private Button btnUpdateHolidays;
        private Button btnGenHolidaysDax;
        private TextBox txtCustomTemplate;
        private Button btnGenDate;
        private Button btnUpdate;
        private Button btnPackage;
        private Button btnPreviewTemplate;
    }
}