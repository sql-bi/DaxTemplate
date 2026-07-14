namespace Dax.Template.Exceptions;

public class InvalidAttributeException(string attributeValue, string entityName)
    : TemplateException($"Invalid attribute type {attributeValue} in entity {entityName}");