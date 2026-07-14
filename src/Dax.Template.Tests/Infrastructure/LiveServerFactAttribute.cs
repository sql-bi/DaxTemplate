namespace Dax.Template.Tests.Infrastructure
{
    using System;
    using Xunit;

    /// <summary>
    /// A <see cref="FactAttribute"/> that is skipped unless live-server connection details are provided via
    /// environment variables. This keeps the live-server tests in the suite (discoverable, runnable on demand)
    /// while never gating CI, which runs offline.
    /// </summary>
    public sealed class LiveServerFactAttribute : FactAttribute
    {
        public const string ServerEnvVar = "DAXTEMPLATE_LIVE_SERVER";
        public const string DatabaseEnvVar = "DAXTEMPLATE_LIVE_DATABASE";

        public LiveServerFactAttribute()
        {
            var server = Environment.GetEnvironmentVariable(ServerEnvVar);
            var database = Environment.GetEnvironmentVariable(DatabaseEnvVar);
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
            {
                Skip = $"Set {ServerEnvVar} and {DatabaseEnvVar} to run live-server tests.";
            }
        }
    }
}