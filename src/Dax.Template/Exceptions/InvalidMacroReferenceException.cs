namespace Dax.Template.Exceptions;

public class InvalidMacroReferenceException : TemplateException
{
    public InvalidMacroReferenceException(string macro, string daxExpression)
        : base($"Invalid macro reference {macro} in DAX expression: {daxExpression}") { }

    public InvalidMacroReferenceException(string macro, string daxExpression, string additionalMessage)
        : base($"{additionalMessage} Invalid macro reference {macro} in DAX expression: {daxExpression}") { }

    public InvalidMacroReferenceException(string macro, string[] multipleMatches, string daxExpression)
        : base($"Multiple results ({string.Join(", ", multipleMatches)}) for macro reference {macro} in DAX expression: {daxExpression}") { }
}