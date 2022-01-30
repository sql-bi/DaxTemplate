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

combine different time intelligence into the same date table
wildcard for measure selection
DISPLAYFOLDER of original measure
Cascading match for IsoTranslation (support region only for translations files?)
Add flag to expose parameters to Bravo UI

Check DISPLAYFOLDERRULE for fixed measure (e.g. _ShowValueForDates)
Include multiple measure template files (calendar+fiscal) with different DisplayFolderRule
API: 
- List of available templates
    - Include config for each model
    - Localization of description - it should be a reference to UI names
*/