namespace Dax.Template.Tests
{
    using Dax.Template.Exceptions;
    using Dax.Template.Extensions;
    using Dax.Template.Syntax;
    using System.Linq;
    using Xunit;

    /// <summary>
    /// Characterization tests pinning the CURRENT behavior of <see cref="Extensions.TSort{T}"/>
    /// (Extensions/TSort.cs), the dependency-ordering topological sort used to sequence DAX code
    /// generation steps (see Syntax/IDependencies.cs, Syntax/DaxElement.cs, Syntax/Var.cs). These exist
    /// as a safety net before any refactor of the sort/cycle-detection logic.
    /// </summary>
    public class DependencySortCharacterizationTests
    {
        [Fact]
        public void TSort_LinearChain_OrdersDependenciesBeforeDependents()
        {
            // Arrange: A depends on B, B depends on C (C is the leaf, no dependencies).
            var nodeC = new DaxElement { Expression = "C" };
            var nodeB = new DaxElement { Expression = "B", Dependencies = new IDependencies<DaxBase>[] { nodeC } };
            var nodeA = new DaxElement { Expression = "A", Dependencies = new IDependencies<DaxBase>[] { nodeB } };

            // Act
            var sorted = new[] { nodeA, nodeB, nodeC }.TSort(x => x.Dependencies?.Cast<DaxElement>()).ToList();

            // Assert: dependencies are ordered before their dependents (topological order): C, B, A.
            var order = sorted.Select(s => s.item).ToArray();
            Assert.Equal(new DaxElement[] { nodeC, nodeB, nodeA }, order);

            // and levels strictly increase from leaf to root.
            Assert.True(sorted[0].level < sorted[1].level);
            Assert.True(sorted[1].level < sorted[2].level);
        }

        [Fact]
        public void TSort_EmptySource_ReturnsEmpty()
        {
            // Arrange
            var source = Enumerable.Empty<DaxElement>();

            // Act
            var sorted = source.TSort(x => x.Dependencies?.Cast<DaxElement>()).ToList();

            // Assert
            Assert.Empty(sorted);
        }

        [Fact]
        public void TSort_SelfReferencingNode_ThrowsCircularDependencyExceptionImmediately()
        {
            // Arrange: a node whose Dependencies array directly contains itself.
            var node = new DaxElement { Expression = "SelfRef" };
            node.Dependencies = new IDependencies<DaxBase>[] { node };

            // Act & Assert: current behavior detects a direct self-reference via the
            // `allDependencies.Contains(item)` check in VisitDependencies and throws immediately
            // (no deep recursion needed).
            Assert.Throws<CircularDependencyException>(() => new[] { node }.TSort(x => x.Dependencies?.Cast<DaxElement>()).ToList());
        }

        [Fact]
        public void TSort_TwoNodeMutualCycle_ThrowsCircularDependencyExceptionViaNestedCallGuard()
        {
            // Arrange: A -> B -> A. Neither node's *direct* Dependencies array contains itself, so the
            // immediate self-reference check in VisitDependencies never trips; the mutual recursion
            // instead grows until the MAX_NESTED_CALLS (1000) guard fires. This is a real quirk of the
            // current implementation worth pinning: a 2+ node cycle is detected far less directly than
            // a single-node self-loop (see previous test).
            var nodeA = new DaxElement { Expression = "A" };
            var nodeB = new DaxElement { Expression = "B" };
            nodeA.Dependencies = new IDependencies<DaxBase>[] { nodeB };
            nodeB.Dependencies = new IDependencies<DaxBase>[] { nodeA };

            // Act & Assert
            Assert.Throws<CircularDependencyException>(() => new[] { nodeA }.TSort(x => x.Dependencies?.Cast<DaxElement>()).ToList());
        }
    }
}