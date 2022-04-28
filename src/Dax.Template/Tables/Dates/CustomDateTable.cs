using System;
using System.Linq;
using Microsoft.AnalysisServices.Tabular;
using TabularModel = Microsoft.AnalysisServices.Tabular.Model;
using Column = Dax.Template.Model.Column;
using Dax.Template.Exceptions;
using Dax.Template.Interfaces;
using Dax.Template.Constants;

namespace Dax.Template.Tables.Dates
{
    public class CustomDateTemplateDefinition : CustomTemplateDefinition
    {
        /// <summary>
        /// Define the calendar type for time intelligence calculations
        /// </summary>
        public string? CalendarType { get; set; }
        public string[]? CalendarTypes { get; set; }
    }
    public class CustomDateTable : BaseDateTemplate<IDateTemplateConfig>
    {
        // TODO: this could be localized (as other column names)
        const string DATE_COLUMN_NAME = "Date";

        public CustomDateTable(IDateTemplateConfig config, CustomDateTemplateDefinition template, TabularModel? model, string? referenceTable = null)
            : base(config, template, model)
        {
            HiddenTable = referenceTable;
            Annotations.Add(Attributes.SQLBI_TEMPLATE_ATTRIBUTE, Attributes.SQLBI_TEMPLATE_DATES);
            Annotations.Add(
                Attributes.SQLBI_TEMPLATETABLE_ATTRIBUTE, 
                (referenceTable == null) ? Attributes.SQLBI_TEMPLATETABLE_DATEAUTOTEMPLATE : Attributes.SQLBI_TEMPLATETABLE_DATE );

            if (!string.IsNullOrWhiteSpace(template.CalendarType)) {
                CalendarType = new string[] { template.CalendarType };
            }
            else
            {
                CalendarType = template.CalendarTypes;
            }
        }
        protected override void InitTemplate(IDateTemplateConfig config, CustomTemplateDefinition template, Predicate<CustomTemplateDefinition.Column> skipColumn, TabularModel? model)
        {
            bool hasHolidays = HolidaysConfig.HasHolidays(config.HolidaysReference);
            if (hasHolidays)
            {
                if (model?.Tables.FirstOrDefault(t => t.Name == config.HolidaysReference?.TableName) == null)
                {
                    throw new TemplateException($"Holidays table '{config.HolidaysReference?.TableName}' not found.");
                }
            }
            base.InitTemplate(
                config,
                template,
                // Skip columns related to holidays if no holidays configuration available
                ((columnDefinition) => columnDefinition.RequiresHolidays && !hasHolidays),
                model);
        }
        protected override Column CreateColumn(string name, DataType dataType)
        {
            if (name == DATE_COLUMN_NAME)
            {
                return new Model.DateColumn()
                {
                    Name = name,
                    DataType = dataType
                };
            }
            else return base.CreateColumn(name, dataType);
        }
    }
}