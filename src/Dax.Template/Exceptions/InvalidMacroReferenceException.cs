namespace Dax.Template.Exceptions
{
    public class InvalidMacroReferenceException : TemplateException
    {
        public InvalidMacroReferenceException(string macro, string daxExpressionmessage)
            : base($"Invalid macro reference {macro} in DAX expression: {daxExpressionmessage}") { }

        public InvalidMacroReferenceException(string macro, string daxExpressionmessage, string additionalMessage)
            : base($"{additionalMessage} Invalid macro reference {macro} in DAX expression: {daxExpressionmessage}") { }

        public InvalidMacroReferenceException(string macro, string[] multipleMatches, string daxExpressionmessage)
            : base($"Multiple results ({string.Join(", ", multipleMatches)}) for macro reference {macro} in DAX expression: {daxExpressionmessage}") { }
    }
}
