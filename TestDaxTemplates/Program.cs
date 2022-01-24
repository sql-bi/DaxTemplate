namespace TestDaxTemplates
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new ApplyDaxTemplate());
        }
    }
}

/*
TODO

add description to config file
copy format original measure
combine different time intelligence into the same date table
wildcard for measure selection
override table for measures of template
DISPLAYFOLDER of original measure
Cascading match for IsoTranslation (support region only for translations files?)
Add flag to expose parameters to Bravo UI

CHeck DISPLAYFOLDERRULE for fixed measure (e.g. _ShowValueForDates)
Include multiple measure template files (calendar+fiscal) with different DisplayFolderRule
API: 
- List of available templates
    - Include config for each model
    - Localization of description - it should be a reference to UI names

Improve preview
- Gets all the modified table and define them as local tables with a __PREVIEW__ prefix
- Replace modified table names adding the __PREVIEW__ prefix in order to fix the dependencies
- Query the table using always all the modified tables as local tables during the preview
 * */