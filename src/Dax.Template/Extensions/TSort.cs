using System;
using System.Linq;
using System.Collections.Generic;
using Dax.Template.Syntax;
using Dax.Template.Exceptions;

namespace Dax.Template.Extensions
{

    public static partial class Extensions
    {

        public static IEnumerable<(T item, int level)> TSort<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>?> dependencies, bool onlyAddLevel = true) where T : Syntax.IDependencies<Syntax.DaxBase>
        {
            var sorted = new List<(T item, int level)>();
            var visited = new HashSet<T>();

            if (!source.Any())
            {
                return sorted;
            }

            foreach (var item in source.Where(n => (!onlyAddLevel) || n.AddLevel))
            {
                Visit(item, visited, sorted, dependencies);
            }

            if (!sorted.Any())
            {
                return sorted;
            }

            // Add the dependencies required in each level (usually row variables)
            var result = new List<(T item, int level)>();
            int min = sorted.Min(element => element.level);
            int max = sorted.Max(element => element.level);
            for (int currentLoopLevel = min; currentLoopLevel <= max; currentLoopLevel++)
            {
                result.AddRange(sorted.Where(element => element.level == currentLoopLevel && element.item.AddLevel));
                var referencedVariables =
                    from element in sorted
                    where element.level == currentLoopLevel && element.item.AddLevel == false
                    select element;
                var referencedDependencies =
                    (from element in sorted
                     where element.level == currentLoopLevel
                     select element.item as Syntax.IDependencies<Syntax.DaxBase>
                    ).GetDependencies(includeSelf: false);
                var previousVariables = referencedDependencies.Where(element => element.AddLevel == false).TSort(v => v.Dependencies, onlyAddLevel: false);
                if (result.Any(t => t.level == currentLoopLevel))
                {
                    var pos = result.FirstOrDefault(t => t.level == currentLoopLevel);
                    result.InsertRange(
                        result.IndexOf(pos),
                        from element in sorted
                        where element.level < currentLoopLevel
                            && element.item.AddLevel == false
                            && previousVariables.Any(item => object.ReferenceEquals(item.item, element.item))
                            && !result.Any(existingItem => object.ReferenceEquals(existingItem.item, element.item) && existingItem.level == currentLoopLevel)
                            && element.item is not Syntax.IGlobalScope
                        select (element.item, currentLoopLevel)
                    );
                    result.InsertRange(
                        result.IndexOf(pos),
                        from element in referencedVariables
                        select (element.item, element.level + 1)
                    );
                }
                else
                {
                    result.AddRange(
                        from element in sorted
                        where element.level < currentLoopLevel
                            && element.item.AddLevel == false
                            && previousVariables.Any(item => object.ReferenceEquals(item.item, element.item))
                            && !result.Any(existingItem => object.ReferenceEquals(existingItem.item, element.item) && existingItem.level == currentLoopLevel)
                            && element.item is not Syntax.IGlobalScope
                        select (element.item, currentLoopLevel)
                    );
                    result.AddRange(
                        from element in referencedVariables
                        select (element.item, element.level + 1)
                    );

                }
            }
            return result;
        }

        /// <summary>
        /// Maximum number of nested calls in VisitDependencies
        /// </summary>
        private const int MAX_NESTED_CALLS = 1000;

        private static void Visit<T>(T item, HashSet<T> visited, List<(T, int level)> sorted, Func<T, IEnumerable<T>?> dependencies) where T : Syntax.IDependencies<Syntax.DaxBase>
        {
            if (!visited.Contains(item))
            {
                visited.Add(item);

                var allDependencies = dependencies(item);

                if (allDependencies != null)
                {
                    foreach (var dep in allDependencies)
                    {
                        Visit(dep, visited, sorted, dependencies);
                    }
                }

                int level = VisitDependencies(item, visited, sorted, dependencies);
                sorted.Add((item, level));
            }
        }

        private static int VisitDependencies<T>(T item, HashSet<T> visited, List<(T, int level)> sorted, Func<T, IEnumerable<T>?> dependencies, int level = 0, int nestedCalls = 0) where T : Syntax.IDependencies<Syntax.DaxBase>
        {
            var allDependencies = dependencies(item);
            // var dependenciesListAddLevel = allDependencies?.Where(d => d.AddLevel == true);

            if (nestedCalls > MAX_NESTED_CALLS)
            {
                string? varName = (item as IDaxName)?.DaxName.ToString();
                throw new CircularDependencyException(varName, "{STACK OVERFLOW: check complex dependencies}");
            }
            
            if (allDependencies?.Contains(item) == true)
            {
                throw new CircularDependencyException((item as IDaxName)?.DaxName.ToString(), item.Expression);
            }
            
            level += item.AddLevel ? 1 : 0;
            int maxLevel = level;
            if (allDependencies != null)
            {
                foreach (var dep in allDependencies)
                {
                    var nestedLevel = VisitDependencies(dep, visited, sorted, dependencies, level, ++nestedCalls);
                    if (nestedLevel > maxLevel)
                    {
                        maxLevel = nestedLevel;
                    }
                }
            }

            return maxLevel;
        }
    }
}
