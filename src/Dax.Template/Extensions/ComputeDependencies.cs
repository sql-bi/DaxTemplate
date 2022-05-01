using System.Linq;
using System.Collections.Generic;
using Dax.Template.Syntax;
using System.Text.RegularExpressions;
using Dax.Template.Exceptions;

namespace Dax.Template.Extensions
{
    public static partial class Extensions
    {
        public static void AddDependenciesFromExpression(this IEnumerable<IDaxName> daxElements)
        {
            daxElements.AddDependenciesFromExpression(daxElements);
        }

        public static void AddDependenciesFromExpression(this IEnumerable<IDependencies<DaxBase>> items, IEnumerable<IDaxName> daxElements)
        {
            items
                .Where(item => !item.IgnoreAutoDependency && !string.IsNullOrEmpty(item.Expression))
                .ToList()
                .ForEach(item => item.InternalAdd(FindDaxReferences, daxElements));
        }

        //private readonly static Regex FindVariables = new(@"__(\w*)", RegexOptions.Compiled);
        //private readonly static Regex FindColumns = new(@"(?<=[^']|^)\[(.*?)\]", RegexOptions.Compiled);
        private readonly static Regex FindDaxReferences = new(@"__(\w*)|(?<=[^']|^)\[(.*?)\]", RegexOptions.Compiled);
        private static void InternalAdd(this IDependencies<DaxBase> item, Regex findTokenRegex, IEnumerable<IDaxName> daxElements)
        {
            if (item.Expression == null) return;

            var findTokens = findTokenRegex.Matches(item.Expression);

            var tokens =
                from token in findTokens
                select token.Value;

            var invalidReferences =
                from token in tokens
                where !daxElements.Any(v => v.DaxName == token)
                select token;

            if (invalidReferences.Any())
            {
                throw new InvalidVariableReferenceException(invalidReferences.First(), item.Expression);
            }

            var dependenciesToken =
                from var in daxElements
                where tokens.Contains(var.DaxName)
                    && !(item.Dependencies?.Contains(var) == true)
                select var;

            item.Dependencies = item.Dependencies?.Union(dependenciesToken).ToArray() ?? dependenciesToken.ToArray();
        }
    }
}
