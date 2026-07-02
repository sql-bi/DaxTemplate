using Dax.Template.Syntax;
using System.Collections.Generic;

namespace Dax.Template.Extensions
{
    public static partial class Extensions
    {
        public static IEnumerable<IDependencies<DaxBase>> GetDependencies(this IEnumerable<IDependencies<DaxBase>> listDependencies, bool includeSelf = true)
        {
            var result = new List<IDependencies<DaxBase>>();
            foreach (var dep in listDependencies)
            {
                if (includeSelf)
                {
                    result.Add(dep);
                }
                if (dep.Dependencies != null)
                {
                    result.AddRange(dep.Dependencies);
                }
            }
            return result;
        }
    }
}