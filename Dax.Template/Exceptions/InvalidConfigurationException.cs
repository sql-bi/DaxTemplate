namespace Dax.Template.Exceptions
{
    public class InvalidConfigurationException : TemplateException
    {
        public InvalidConfigurationException(string variableName, string value)
            : base($"Global variable {variableName} not found to assign the default value {value}") { }
        public InvalidConfigurationException(string undefinedConfiguration)
            : base($"Undefined configuration {undefinedConfiguration}") { }
    }
}
