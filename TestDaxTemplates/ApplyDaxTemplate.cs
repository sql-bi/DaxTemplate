using Microsoft.AnalysisServices.Tabular;
using Microsoft.Extensions.Configuration;
using Dax.Template;
using Dax.Template.Tables;
using Dax.Template.Tables.Dates;
using Dax.Template.Measures;
using Dax.Template.Exceptions;
using System.Text.Json;
using TabularJsonSerializer = Microsoft.AnalysisServices.Tabular.JsonSerializer;
using SystemJsonSerializer = System.Text.Json.JsonSerializer;
using Dax.Template.Interfaces;
using System.Reflection;
using System.Collections;
using Microsoft.AnalysisServices.AdomdClient;
using System.Text.Encodings.Web;

namespace TestDaxTemplates
{
    public partial class ApplyDaxTemplate : Form
    {
        public ApplyDaxTemplate()
        {
            InitializeComponent();
        }

        private void Update_Click(object sender, EventArgs e)
        {
            // TODO: evaluate why a quoted identifier in the Source Column 
            //       is removed in PBI Desktop - now it only works when
            //       the hidden table name does not require a quoted identifier
            string dateTableNameTemplate = "DateAutoTemplate";
            string dateTableName = "Date";
            Server server = new();
            server.Connect(txtServer.Text);
            Database db = server.Databases[txtDatabase.Text];
            Model model = db.Model;

            try
            {
                //CreateDateTable(dateTableName, model, false);

                var hiddenDateTemplate = CreateDateTable(dateTableNameTemplate, model, true, useIsoFormat: chkCustomTranslation.Checked);
                var visibleDateTemplate = CreateDateTable(dateTableName, model, false, referenceTable: dateTableNameTemplate, applyTranslations: chkCustomTranslation.Checked);

                // Generate BIM for debug purposes
                //string modelBim = TabularJsonSerializer.SerializeDatabase(db);
                //File.WriteAllText(@"c:\temp\changes1.json", modelBim);

                // Show DAX for debug purposes
                txtDax.Text = hiddenDateTemplate.GetDaxTableExpression(model);

                model.SaveChanges();
            }
            catch (TemplateException ex)
            {
                MessageBox.Show(ex.Message, "Template Exception");

                // TODO: add a parameter to create a different table copying the existing relationships
            }
            server.Disconnect();
            MessageBox.Show($"Applied template {dateTableNameTemplate}");
        }

        private CalculatedTableTemplateBase CreateHolidaysDefinitionTable(string dateTableName, Model model, bool hideTable)
        {
            Table tableHolidays = model.Tables.Find(dateTableName);
            if (tableHolidays == null)
            {
                tableHolidays = new Table { Name = dateTableName };
                model.Tables.Add(tableHolidays);
            }
            CalculatedTableTemplateBase template;
            template = new HolidaysDefinitionTable(ReadHolidaysDefinitionConfig());
            template.ApplyTemplate(tableHolidays, hideTable);
            tableHolidays.RequestRefresh(RefreshType.Full);

            return template;
        }

        private CalculatedTableTemplateBase CreateHolidaysTable(string dateTableName, Model model, bool hideTable)
        {
            Table tableHolidays = model.Tables.Find(dateTableName);
            if (tableHolidays == null)
            {
                tableHolidays = new Table { Name = dateTableName };
                model.Tables.Add(tableHolidays);
            }
            CalculatedTableTemplateBase template;
            template = new HolidaysTable(ReadConfig<TemplateConfiguration>());
            template.ApplyTemplate(tableHolidays, hideTable);
            tableHolidays.RequestRefresh(RefreshType.Full);

            return template;
        }

        private ReferenceCalculatedTable CreateDateTable(string dateTableName, Model model, bool hideTable, string? referenceTable = null, bool useIsoFormat = false, bool applyTranslations = false)
        {
            Table tableDate = model.Tables.Find(dateTableName);
            if (tableDate == null)
            {
                tableDate = new Table { Name = dateTableName };
                model.Tables.Add(tableDate);
            }
            Translations? translations = null;
            if (applyTranslations)
            {
                translations = new(ReadTranslations());
                translations.DefaultIso = "it-IT";
            }
            ReferenceCalculatedTable template;
            if (chkCustomTemplate.Checked)
            {
                template = new CustomDateTable(ReadConfig<TemplateConfiguration>(), ReadTemplateDefinition(), model)
                {
                    Translation = translations,
                    HiddenTable = referenceTable,
                    IsoFormat = useIsoFormat ? "it-IT" : null
                };
            }
            else
            {
                template = new SimpleDateTable(ReadConfig<SimpleDateTemplateConfig>(), model)
                {
                    HiddenTable = referenceTable
                };
            }

            template.ApplyTemplate(tableDate, hideTable);

            tableDate.RequestRefresh(RefreshType.Full);

            return template;
        }

        private void ApplyDate_Load(object sender, EventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();
            var builder = new ConfigurationBuilder();
            builder.AddCommandLine(args);
            var config = builder.Build();
            txtServer.Text = config["server"];
            txtDatabase.Text = config["database"];

            fileSystemWatcher.Path = txtPath.Text;
            UpdateTemplateList();
            comboTemplates.SelectedIndex = 0;
        }

        private void GenerateDax_Click(object sender, EventArgs e)
        {
            string? result;
            if (chkCustomTemplate.Checked)
            {
                var template = new CustomDateTable(ReadConfig<TemplateConfiguration>(), ReadTemplateDefinition(), null);
                result = template?.GetDaxTableExpression(null);
            }
            else { 
                var template = new SimpleDateTable(ReadConfig<SimpleDateTemplateConfig>(),null);
                result = template.GetDaxTableExpression(null);
            }
            txtDax.Text = result;
        }

        private void ReadConfig_Click(object sender, EventArgs e)
        {
            var config = ReadConfig<SimpleDateTemplateConfig>();
            var result = SystemJsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            txtDax.Text = result;
            MessageBox.Show($"Read config {txtConfig.Text} completed");
        }

        private T ReadConfig<T>() where T: TemplateConfiguration
        {
            return ReadConfig<T>(txtConfig.Text);
        }
        private static T ReadConfig<T>(string path) where T : TemplateConfiguration
        {
            string configJson = File.ReadAllText(path);
            var configUnchecked = SystemJsonSerializer.Deserialize(configJson, typeof(T));
            if (configUnchecked is not T config) throw new Exception("Invalid configuration");
            return config;
        }

        private CustomDateTemplateDefinition ReadTemplateDefinition()
        {
            string configJsonFilename = txtCustomTemplate.Text;
            string configJson = File.ReadAllText(configJsonFilename);
            if (SystemJsonSerializer.Deserialize(configJson, typeof(CustomDateTemplateDefinition)) is not CustomDateTemplateDefinition config) throw new Exception("Invalid configuration");
            return config;
        }

        private Translations.Definitions ReadTranslations()
        {
            string translationsJsonFilename = txtCustomTranslation.Text;
            string translationsJson = File.ReadAllText(translationsJsonFilename);
            if (SystemJsonSerializer.Deserialize(translationsJson, typeof(Translations.Definitions)) is not Translations.Definitions definitions) throw new Exception("Invalid translations");
            return definitions;
        }

        private void ReadTemplate_Click(object sender, EventArgs e)
        {
            ReadTemplateDefinition();
            MessageBox.Show($"Read template {txtCustomTemplate.Text} completed");
        }

        private HolidaysDefinitionTable.HolidaysDefinitions ReadHolidaysDefinitionConfig()
        {
            IHolidaysConfig config = ReadConfig<TemplateConfiguration>();
            string holidaysJsonFilename = $@"..\..\..\{config.HolidaysDefinitionTemplate}";
            string holidaysJson = File.ReadAllText(holidaysJsonFilename);
            if (SystemJsonSerializer.Deserialize(holidaysJson, typeof(HolidaysDefinitionTable.HolidaysDefinitions)) is not HolidaysDefinitionTable.HolidaysDefinitions holidaysDefinition) throw new Exception("Invalid configuration");
            return holidaysDefinition;
        }

        private void GenerateHolidays_Click(object sender, EventArgs e)
        {
            var template = new HolidaysDefinitionTable(ReadHolidaysDefinitionConfig());
            var result = template.GetDaxTableExpression(null);
            txtDax.Text = result;
        }

        private void UpdateHolidays_Click(object sender, EventArgs e)
        {
            var config = ReadConfig<TemplateConfiguration>();

            // TODO: evaluate why a quoted identifier in the Source Column 
            //       is removed in PBI Desktop - now it only works when
            //       the hidden table name does not require a quoted identifier
            string holidaysDefinitionTemplateName = config.HolidaysDefinitionTable ?? "HolidaysDefinition";
            string holidaysTemplateName = config.HolidaysReference?.TableName ?? "Holidays";
            Server server = new();
            server.Connect(txtServer.Text);
            Database db = server.Databases[txtDatabase.Text];
            Model model = db.Model;

            try
            {
                //CreateDateTable(dateTableName, model, false);

                var hiddenHolidaysDefinitionTemplate = CreateHolidaysDefinitionTable(holidaysDefinitionTemplateName, model, true);
                var hiddenHolidaysTemplate = CreateHolidaysTable(holidaysTemplateName, model, true);

                // Generate BIM for debug purposes
                string modelBim = TabularJsonSerializer.SerializeDatabase(db);
                File.WriteAllText(@"c:\temp\changes.json", modelBim);

                // Show DAX for debug purposes
                txtDax.Text = hiddenHolidaysDefinitionTemplate.GetDaxTableExpression(model);

                model.SaveChanges();
            }
            catch (TemplateException ex)
            {
                MessageBox.Show(ex.Message, "Template Exception");

                // TODO: add a parameter to create a different table copying the existing relationships
            }
            server.Disconnect();
            MessageBox.Show($"Applied template {holidaysDefinitionTemplateName} and {holidaysTemplateName}");
        }

        private void MeasureTemplate_Click(object sender, EventArgs e)
        {
            string templateJsonFilename = @"..\..\..\Templates\TimeIntelligence-05.json";
            string templateJson = File.ReadAllText(templateJsonFilename);
            if (SystemJsonSerializer.Deserialize(templateJson, typeof(MeasuresTemplateDefinition)) is not MeasuresTemplateDefinition measuresTemplate) throw new Exception("Invalid configuration");

            var config = ReadConfig<TemplateConfiguration>();
            var template = new MeasuresTemplate(config, measuresTemplate,new Dictionary<string, object>());

            Server server = new();
            server.Connect(txtServer.Text);
            Database db = server.Databases[txtDatabase.Text];
            Model model = db.Model;

            try
            {
                template.ApplyTemplate(model);
                model.SaveChanges();
            }
            catch (TemplateException ex)
            {
                MessageBox.Show(ex.Message, "Template Exception");
            }
            server.Disconnect();
            MessageBox.Show($"Applied measure template {templateJsonFilename}");
        }

        private void Path_TextChanged(object sender, EventArgs e)
        {
            fileSystemWatcher.Path = txtPath.Text;
        }

        private void UpdateTemplateList()
        {
            var currentSelection = comboTemplates.SelectedItem;
            string path = txtPath.Text;
            var templateFiles =
                from file in Directory.EnumerateFiles(path, fileSystemWatcher.Filter)
                select Path.GetFileNameWithoutExtension(file).Replace(".template","");
            comboTemplates.Items.Clear();
            if (templateFiles != null)
            {
                comboTemplates.Items.AddRange(templateFiles.ToArray());
            }
            comboTemplates.SelectedItem = currentSelection;
        }
        private string GetSelectedTemplatePath()
        {
            string? templateSelection = comboTemplates.SelectedItem.ToString();
            if (templateSelection == null)
            {
                throw new Exception("Template not selected");
            }
            return Path.Combine(txtPath.Text, $"{templateSelection}.template.json");
        }
        private void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            UpdateTemplateList();
        }

        private void Watcher_Error(object sender, ErrorEventArgs e)
        {
            UpdateTemplateList();
        }

        private void Watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            UpdateTemplateList();
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            UpdateTemplateList();
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            UpdateTemplateList();
        }

        private void DisplayChanges(Dax.Template.Model.ModelChanges modelChanges) 
        {
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };
            var result = SystemJsonSerializer.Serialize(modelChanges, options);
            txtDax.Text = result;
        }

        private void ApplyTemplate(bool commitChanges)
        {
            string templatePath = GetSelectedTemplatePath();
            var package = Package.LoadPackage(templatePath);
            if (package?.Configuration == null)
            {
                throw new Exception($"Configuration {templatePath} not loaded.");
            }

            Engine templateEngine = new(package);

            Server server = new();
            server.Connect(txtServer.Text);
            Database db = server.Databases[txtDatabase.Text];
            Model model = db.Model;

            try
            {
                templateEngine.ApplyTemplates(model);
                var modelChanges = Engine.GetModelChanges(model);


                if (commitChanges)
                {
                    model.SaveChanges();
                }
                else
                {
                    // Only for preview data
                    string adomdConnectionString = $"Data Source={txtServer.Text};Catalog={txtDatabase.Text};";
                    AdomdConnection connection = new(adomdConnectionString);
                    int previewRows = 5;

                    modelChanges.PopulatePreview(connection, model, previewRows);
                    DisplayChanges(modelChanges);
                }
            }
            catch (TemplateException ex)
            {
                MessageBox.Show(ex.Message, "Template Exception");
            }
            server.Disconnect();
        }

        private void ApplyTemplate_Click(object sender, EventArgs e)
        {
            ApplyTemplate(commitChanges:true);
        }

        private void CopyDebug_Click(object sender, EventArgs e)
        {
            string debugLine = $"--server=\"{txtServer.Text}\" --database=\"{txtDatabase.Text}\"";
            Clipboard.SetText(debugLine);
        }

        private void CreatePackage_Click(object sender, EventArgs e)
        {
            string templatePath = GetSelectedTemplatePath();
            var package = Package.LoadPackage(templatePath);

            Engine templateEngine = new(package);
            templateEngine.SavePackage(@"c:\temp\test.json");
        }

        private void PreviewTemplate_Click(object sender, EventArgs e)
        {
            ApplyTemplate(commitChanges: false);
        }

        private void BravoConfig_Click(object sender, EventArgs e)
        {
            #region read all available template configurations
            string templatePath = txtPath.Text;
            var bravoTemplates = Bravo.BravoDaxTemplate.GetTemplates(templatePath);
            var result = SystemJsonSerializer.Serialize(bravoTemplates, new JsonSerializerOptions { WriteIndented = true });
            txtDax.Text = result;
            #endregion

            #region Loop over all configuration and apply preview
            Server server = new();
            server.Connect(txtServer.Text);
            try
            {
                // loop preview
                foreach (var config in bravoTemplates)
                {
                    Database db = server.Databases[txtDatabase.Text];
                    Model model = db.Model;

                    var modelChanges = Bravo.BravoDaxTemplate.ApplyTemplate(config, model, $"Data Source={txtServer.Text};Catalog={txtDatabase.Text};", false);
                    if (modelChanges != null)
                    {
                        DisplayChanges(modelChanges);
                    }
                }
            }
            finally
            {
                server.Disconnect();
            }
            #endregion
        }
    }
}
