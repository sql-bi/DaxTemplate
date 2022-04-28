namespace Dax.Template.Exceptions
{
    public class InvalidConfigurationException : TemplateException
    {
        public InvalidConfigurationException(string message)
            : base(message) { }

        public InvalidConfigurationException(string variableName, string value)
            : base($"Global variable {variableName} not found to assign the default value {value}") { }
    }
}
