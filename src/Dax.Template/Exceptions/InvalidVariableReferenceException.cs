namespace Dax.Template.Exceptions;

public class InvalidVariableReferenceException(string variableName, string daxExpressionmessage)
    : TemplateException($"Invalid variable reference {variableName} in DAX expression: {daxExpressionmessage}");