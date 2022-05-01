namespace Dax.Template.TestUI
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
            this.btnBravoConfig = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.fileSystemWatcher)).BeginInit();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // txtServer
            // 
            this.txtServer.Location = new System.Drawing.Point(170, 28);
            this.txtServer.Margin = new System.Windows.Forms.Padding(4);
            this.txtServer.Name = "txtServer";
            this.txtServer.Size = new System.Drawing.Size(465, 39);
            this.txtServer.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(46, 32);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(86, 32);
            this.label1.TabIndex = 1;
            this.label1.Text = "Server:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(46, 83);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(117, 32);
            this.label2.TabIndex = 3;
            this.label2.Text = "Database:";
            // 
            // txtDatabase
            // 
            this.txtDatabase.Location = new System.Drawing.Point(170, 79);
            this.txtDatabase.Margin = new System.Windows.Forms.Padding(4);
            this.txtDatabase.Name = "txtDatabase";
            this.txtDatabase.Size = new System.Drawing.Size(465, 39);
            this.txtDatabase.TabIndex = 4;
            // 
            // txtDax
            // 
            this.txtDax.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtDax.Location = new System.Drawing.Point(46, 419);
            this.txtDax.Margin = new System.Windows.Forms.Padding(4);
            this.txtDax.Multiline = true;
            this.txtDax.Name = "txtDax";
            this.txtDax.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDax.Size = new System.Drawing.Size(1945, 868);
            this.txtDax.TabIndex = 6;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label4.Location = new System.Drawing.Point(46, 366);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(65, 32);
            this.label4.TabIndex = 11;
            this.label4.Text = "DAX";
            // 
            // txtPath
            // 
            this.txtPath.Location = new System.Drawing.Point(170, 138);
            this.txtPath.Margin = new System.Windows.Forms.Padding(4);
            this.txtPath.Name = "txtPath";
            this.txtPath.Size = new System.Drawing.Size(933, 39);
            this.txtPath.TabIndex = 23;
            this.txtPath.TextChanged += new System.EventHandler(this.Path_TextChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(46, 142);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(65, 32);
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
            this.comboTemplates.Location = new System.Drawing.Point(169, 191);
            this.comboTemplates.Margin = new System.Windows.Forms.Padding(4);
            this.comboTemplates.Name = "comboTemplates";
            this.comboTemplates.Size = new System.Drawing.Size(689, 40);
            this.comboTemplates.TabIndex = 24;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(46, 195);
            this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(117, 32);
            this.label6.TabIndex = 25;
            this.label6.Text = "Template:";
            // 
            // btnApplyTemplate
            // 
            this.btnApplyTemplate.Location = new System.Drawing.Point(889, 191);
            this.btnApplyTemplate.Margin = new System.Windows.Forms.Padding(4);
            this.btnApplyTemplate.Name = "btnApplyTemplate";
            this.btnApplyTemplate.Size = new System.Drawing.Size(216, 49);
            this.btnApplyTemplate.TabIndex = 26;
            this.btnApplyTemplate.Text = "Apply Template";
            this.btnApplyTemplate.UseVisualStyleBackColor = true;
            this.btnApplyTemplate.Click += new System.EventHandler(this.ApplyTemplate_Click);
            // 
            // btnCopyDebug
            // 
            this.btnCopyDebug.Location = new System.Drawing.Point(642, 28);
            this.btnCopyDebug.Name = "btnCopyDebug";
            this.btnCopyDebug.Size = new System.Drawing.Size(117, 90);
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
            this.panel1.Location = new System.Drawing.Point(1123, 32);
            this.panel1.Margin = new System.Windows.Forms.Padding(4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(855, 366);
            this.panel1.TabIndex = 28;
            // 
            // btnMeasureTemplate
            // 
            this.btnMeasureTemplate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnMeasureTemplate.Location = new System.Drawing.Point(563, 294);
            this.btnMeasureTemplate.Margin = new System.Windows.Forms.Padding(4);
            this.btnMeasureTemplate.Name = "btnMeasureTemplate";
            this.btnMeasureTemplate.Size = new System.Drawing.Size(252, 52);
            this.btnMeasureTemplate.TabIndex = 34;
            this.btnMeasureTemplate.Text = "Measure Template";
            this.btnMeasureTemplate.UseVisualStyleBackColor = true;
            // 
            // btnReadTemplate
            // 
            this.btnReadTemplate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnReadTemplate.Location = new System.Drawing.Point(252, 294);
            this.btnReadTemplate.Margin = new System.Windows.Forms.Padding(4);
            this.btnReadTemplate.Name = "btnReadTemplate";
            this.btnReadTemplate.Size = new System.Drawing.Size(303, 52);
            this.btnReadTemplate.TabIndex = 33;
            this.btnReadTemplate.Text = "Read Template";
            this.btnReadTemplate.UseVisualStyleBackColor = true;
            // 
            // btnReadConfig
            // 
            this.btnReadConfig.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnReadConfig.Location = new System.Drawing.Point(20, 294);
            this.btnReadConfig.Margin = new System.Windows.Forms.Padding(4);
            this.btnReadConfig.Name = "btnReadConfig";
            this.btnReadConfig.Size = new System.Drawing.Size(217, 52);
            this.btnReadConfig.TabIndex = 32;
            this.btnReadConfig.Text = "Read Config";
            this.btnReadConfig.UseVisualStyleBackColor = true;
            // 
            // txtConfig
            // 
            this.txtConfig.Location = new System.Drawing.Point(300, 26);
            this.txtConfig.Margin = new System.Windows.Forms.Padding(4);
            this.txtConfig.Name = "txtConfig";
            this.txtConfig.Size = new System.Drawing.Size(514, 39);
            this.txtConfig.TabIndex = 31;
            this.txtConfig.Text = "..\\..\\..\\Templates\\Config-01 - Standard.template.json";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(176, 29);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(90, 32);
            this.label5.TabIndex = 30;
            this.label5.Text = "Config:";
            // 
            // chkCustomTemplate
            // 
            this.chkCustomTemplate.AutoSize = true;
            this.chkCustomTemplate.Checked = true;
            this.chkCustomTemplate.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCustomTemplate.Location = new System.Drawing.Point(20, 84);
            this.chkCustomTemplate.Margin = new System.Windows.Forms.Padding(4);
            this.chkCustomTemplate.Name = "chkCustomTemplate";
            this.chkCustomTemplate.Size = new System.Drawing.Size(277, 36);
            this.chkCustomTemplate.TabIndex = 24;
            this.chkCustomTemplate.Text = "Use custom template:";
            this.chkCustomTemplate.UseVisualStyleBackColor = true;
            // 
            // chkCustomTranslation
            // 
            this.chkCustomTranslation.AutoSize = true;
            this.chkCustomTranslation.Location = new System.Drawing.Point(20, 134);
            this.chkCustomTranslation.Margin = new System.Windows.Forms.Padding(4);
            this.chkCustomTranslation.Name = "chkCustomTranslation";
            this.chkCustomTranslation.Size = new System.Drawing.Size(219, 36);
            this.chkCustomTranslation.TabIndex = 29;
            this.chkCustomTranslation.Text = "Use translations:";
            this.chkCustomTranslation.UseVisualStyleBackColor = true;
            // 
            // txtCustomTranslation
            // 
            this.txtCustomTranslation.Location = new System.Drawing.Point(300, 132);
            this.txtCustomTranslation.Margin = new System.Windows.Forms.Padding(4);
            this.txtCustomTranslation.Name = "txtCustomTranslation";
            this.txtCustomTranslation.Size = new System.Drawing.Size(514, 39);
            this.txtCustomTranslation.TabIndex = 28;
            this.txtCustomTranslation.Text = "..\\..\\..\\Templates\\DateLocalization-04.json";
            // 
            // btnUpdateHolidays
            // 
            this.btnUpdateHolidays.Location = new System.Drawing.Point(252, 237);
            this.btnUpdateHolidays.Margin = new System.Windows.Forms.Padding(4);
            this.btnUpdateHolidays.Name = "btnUpdateHolidays";
            this.btnUpdateHolidays.Size = new System.Drawing.Size(303, 47);
            this.btnUpdateHolidays.TabIndex = 27;
            this.btnUpdateHolidays.Text = "Update holidays";
            this.btnUpdateHolidays.UseVisualStyleBackColor = true;
            // 
            // btnGenHolidaysDax
            // 
            this.btnGenHolidaysDax.Location = new System.Drawing.Point(563, 237);
            this.btnGenHolidaysDax.Margin = new System.Windows.Forms.Padding(4);
            this.btnGenHolidaysDax.Name = "btnGenHolidaysDax";
            this.btnGenHolidaysDax.Size = new System.Drawing.Size(252, 47);
            this.btnGenHolidaysDax.TabIndex = 26;
            this.btnGenHolidaysDax.Text = "Gen. Holidays DAX";
            this.btnGenHolidaysDax.UseVisualStyleBackColor = true;
            // 
            // txtCustomTemplate
            // 
            this.txtCustomTemplate.Location = new System.Drawing.Point(300, 84);
            this.txtCustomTemplate.Margin = new System.Windows.Forms.Padding(4);
            this.txtCustomTemplate.Name = "txtCustomTemplate";
            this.txtCustomTemplate.Size = new System.Drawing.Size(514, 39);
            this.txtCustomTemplate.TabIndex = 25;
            this.txtCustomTemplate.Text = "..\\..\\..\\Templates\\DateTemplate-04.json";
            // 
            // btnGenDate
            // 
            this.btnGenDate.Location = new System.Drawing.Point(563, 182);
            this.btnGenDate.Margin = new System.Windows.Forms.Padding(4);
            this.btnGenDate.Name = "btnGenDate";
            this.btnGenDate.Size = new System.Drawing.Size(252, 47);
            this.btnGenDate.TabIndex = 23;
            this.btnGenDate.Text = "Generate DAX";
            this.btnGenDate.UseVisualStyleBackColor = true;
            // 
            // btnUpdate
            // 
            this.btnUpdate.Location = new System.Drawing.Point(252, 182);
            this.btnUpdate.Margin = new System.Windows.Forms.Padding(4);
            this.btnUpdate.Name = "btnUpdate";
            this.btnUpdate.Size = new System.Drawing.Size(303, 47);
            this.btnUpdate.TabIndex = 22;
            this.btnUpdate.Text = "Update date";
            this.btnUpdate.UseVisualStyleBackColor = true;
            // 
            // btnPackage
            // 
            this.btnPackage.Location = new System.Drawing.Point(889, 326);
            this.btnPackage.Margin = new System.Windows.Forms.Padding(4);
            this.btnPackage.Name = "btnPackage";
            this.btnPackage.Size = new System.Drawing.Size(216, 49);
            this.btnPackage.TabIndex = 29;
            this.btnPackage.Text = "Create Package";
            this.btnPackage.UseVisualStyleBackColor = true;
            this.btnPackage.Click += new System.EventHandler(this.CreatePackage_Click);
            // 
            // btnPreviewTemplate
            // 
            this.btnPreviewTemplate.Location = new System.Drawing.Point(889, 247);
            this.btnPreviewTemplate.Margin = new System.Windows.Forms.Padding(4);
            this.btnPreviewTemplate.Name = "btnPreviewTemplate";
            this.btnPreviewTemplate.Size = new System.Drawing.Size(216, 49);
            this.btnPreviewTemplate.TabIndex = 30;
            this.btnPreviewTemplate.Text = "Preview Template";
            this.btnPreviewTemplate.UseVisualStyleBackColor = true;
            this.btnPreviewTemplate.Click += new System.EventHandler(this.PreviewTemplate_Click);
            // 
            // btnBravoConfig
            // 
            this.btnBravoConfig.Location = new System.Drawing.Point(805, 32);
            this.btnBravoConfig.Margin = new System.Windows.Forms.Padding(4);
            this.btnBravoConfig.Name = "btnBravoConfig";
            this.btnBravoConfig.Size = new System.Drawing.Size(217, 83);
            this.btnBravoConfig.TabIndex = 31;
            this.btnBravoConfig.Text = "Bravo Config";
            this.btnBravoConfig.UseVisualStyleBackColor = true;
            this.btnBravoConfig.Click += new System.EventHandler(this.BravoConfig_Click);
            // 
            // ApplyDaxTemplate
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(13F, 32F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(2012, 1318);
            this.Controls.Add(this.btnBravoConfig);
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
            this.Margin = new System.Windows.Forms.Padding(4);
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
        private Button btnBravoConfig;
    }
}