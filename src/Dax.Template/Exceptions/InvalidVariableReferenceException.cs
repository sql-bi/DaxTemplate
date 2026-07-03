namespace Dax.Template.Exceptions;

public class InvalidVariableReferenceException(string variableName, string daxExpression)
    : TemplateException($"Invalid variable reference {variableName} in DAX expression: {daxExpression}");