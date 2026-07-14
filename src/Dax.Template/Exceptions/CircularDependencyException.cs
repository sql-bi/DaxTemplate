namespace Dax.Template.Exceptions;

public class CircularDependencyException(string? variableName, string? daxExpression)
    : TemplateException($"Circular dependency in variable definition {variableName ?? "[undefined]"} with DAX expression: {daxExpression ?? "[undefined]"}");