namespace Dax.Template.Exceptions
{
    public class InvalidAttributeException : TemplateException
    {
        public InvalidAttributeException(string attributeValue, string entitymessage)
            : base($"Invalid attribute type {attributeValue} in entity {entitymessage}") { }
    }
}
