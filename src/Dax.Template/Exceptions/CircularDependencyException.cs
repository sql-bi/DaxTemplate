namespace Dax.Template.Exceptions
{
    public class CircularDependencyException : TemplateException
    {
        public CircularDependencyException(string? variableName, string? daxExpressionmessage)
            : base($"Circulare dependency in variable definition {variableName??"[undefined]"} with DAX expression: {daxExpressionmessage??"[undefined]"}") { }
    }
}
