namespace Dax.Template.Exceptions;

public class CircularDependencyException(string? variableName, string? daxExpressionmessage)
    : TemplateException($"Circulare dependency in variable definition {variableName ?? "[undefined]"} with DAX expression: {daxExpressionmessage ?? "[undefined]"}");