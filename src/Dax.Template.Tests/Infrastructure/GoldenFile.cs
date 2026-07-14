namespace Dax.Template.Tests.Infrastructure
{
    using Microsoft.AnalysisServices.Tabular;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using Xunit;

    /// <summary>
    /// Golden-file (snapshot) helpers for offline regression tests. Serializes a tabular database to BIM,
    /// strips non-deterministic content, and compares against a committed snapshot under _data/Golden.
    /// Set the environment variable <c>UPDATE_GOLDEN=1</c> to (re)write snapshots instead of asserting.
    /// </summary>
    public static class GoldenFile
    {
        private static readonly Regex LineageTagRegex =
            new("\"lineageTag\": \"[0-9a-fA-F-]{36}\"", RegexOptions.Compiled);

        /// <summary>
        /// Serializes the database to BIM JSON and normalizes volatile content (lineage tag GUIDs) so the
        /// result is stable across runs. Structural content (names, expressions, properties) is preserved.
        /// </summary>
        public static string SerializeNormalized(Database database)
        {
            var bim = JsonSerializer.SerializeDatabase(database);
            return Normalize(bim);
        }

        public static string Normalize(string bim)
        {
            // lineage tags are freshly-generated GUIDs (Guid.NewGuid) on every apply; blank them out.
            bim = LineageTagRegex.Replace(bim, "\"lineageTag\": \"\"");
            // normalize line endings so snapshots are not sensitive to git autocrlf.
            return bim.Replace("\r\n", "\n");
        }

        /// <summary>
        /// Asserts <paramref name="actual"/> equals the committed snapshot at
        /// <c>_data/Golden/{name}.{extension}</c> (extension defaults to <c>bim</c> for backward compatibility
        /// with the existing BIM snapshot callers). When the snapshot is missing or UPDATE_GOLDEN=1, the
        /// snapshot is written and the assertion is skipped.
        /// </summary>
        public static void AssertMatchesSnapshot(string actual, string name, string extension = "bim", [CallerFilePath] string callerFilePath = "")
        {
            actual = actual.Replace("\r\n", "\n");
            var goldenPath = GetSnapshotPath(name, extension, callerFilePath);
            var update = Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1";

            if (update || !File.Exists(goldenPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
                File.WriteAllText(goldenPath, actual);
                return;
            }

            var expected = File.ReadAllText(goldenPath).Replace("\r\n", "\n");
            Assert.Equal(expected, actual);
        }

        private static string GetSnapshotPath(string name, string extension, string callerFilePath)
        {
            // callerFilePath points at the calling test's source file, which lives somewhere under the test
            // project. Walk up to the directory that holds the .csproj so snapshots are committed next to the
            // source and located independently of the build output directory or the caller's subfolder depth.
            // Under a ContinuousIntegrationBuild (e.g. the Azure Pipelines build step, which applies
            // /p:ContinuousIntegrationBuild=true to every project), Roslyn's deterministic source-path mapping
            // rewrites the [CallerFilePath] literal to a placeholder like "/_/src/...", which never exists on
            // disk. AppContext.BaseDirectory is the test assembly's real runtime output directory and is
            // unaffected by that compile-time remapping, so fall back to it when the caller path doesn't resolve.
            var dir = FindProjectDirectory(Path.GetDirectoryName(callerFilePath))
                ?? FindProjectDirectory(AppContext.BaseDirectory);
            if (dir == null)
            {
                throw new InvalidOperationException(
                    $"Could not locate the test project directory (.csproj) from caller path '{callerFilePath}' or base directory '{AppContext.BaseDirectory}'.");
            }
            return Path.Combine(dir, "_data", "Golden", name + "." + extension);
        }

        private static string? FindProjectDirectory(string? startDirectory)
        {
            var dir = startDirectory;
            while (dir != null && Directory.Exists(dir) && Directory.GetFiles(dir, "*.csproj").Length == 0)
            {
                dir = Path.GetDirectoryName(dir);
            }
            return dir != null && Directory.Exists(dir) && Directory.GetFiles(dir, "*.csproj").Length > 0 ? dir : null;
        }
    }
}