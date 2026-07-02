namespace Dax.Template.Exceptions;

public class InvalidAttributeException(string attributeValue, string entitymessage)
    : TemplateException($"Invalid attribute type {attributeValue} in entity {entitymessage}");