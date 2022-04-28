namespace Dax.Template.Exceptions
{
    public class InvalidVariableReferenceException : TemplateException
    {
        public InvalidVariableReferenceException(string variableName, string daxExpressionmessage) 
            : base( $"Invalid variable reference {variableName} in DAX expression: {daxExpressionmessage}") { }
    }
}
