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

            // Act & Assert: VisitDependencies tracks the current DFS recursion path (Extensions/TSort.cs);
            // re-visiting `node` while it is still on that path is detected the moment the self-reference
            // is walked, so the exception is thrown immediately (no deep recursion needed).
            Assert.Throws<CircularDependencyException>(() => new[] { node }.TSort(x => x.Dependencies?.Cast<DaxElement>()).ToList());
        }

        [Fact]
        public void TSort_TwoNodeMutualCycle_ThrowsCircularDependencyExceptionPromptly()
        {
            // Arrange: A -> B -> A. Neither node's *direct* Dependencies array contains itself, but the
            // DFS recursion-path tracking in VisitDependencies (Extensions/TSort.cs) detects the cycle as
            // soon as A is revisited while still on the current path (A -> B -> A), without needing any
            // nested-call backstop.
            var nodeA = new DaxElement { Expression = "A" };
            var nodeB = new DaxElement { Expression = "B" };
            nodeA.Dependencies = new IDependencies<DaxBase>[] { nodeB };
            nodeB.Dependencies = new IDependencies<DaxBase>[] { nodeA };

            // Act & Assert: detection is prompt and reports the actual cycle node/expression, not a
            // generic stack-overflow-style message.
            var ex = Assert.Throws<CircularDependencyException>(() => new[] { nodeA }.TSort(x => x.Dependencies?.Cast<DaxElement>()).ToList());
            // The message must report an actual cycle node's Expression (A or B, whichever the DFS
            // revisits first) rather than a generic stack-overflow message. A bare Contains("A") would be
            // tautological since the fixed template text already contains "DAX".
            Assert.Matches("expression: [AB]$", ex.Message);
            Assert.DoesNotContain("STACK OVERFLOW", ex.Message);
        }
    }
}