namespace ThreatModelForge.Analysis
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Provides the authoritative JSON Schema for versioned rule-pack documents.
    /// </summary>
    public static class RulePackSchema
    {
        /// <summary>Gets the Draft 2020-12 schema for <c>tmforge-rules</c> version 2.</summary>
        public static string VersionTwo { get; } = LoadVersionTwo();

        private static string LoadVersionTwo()
        {
            Assembly assembly = typeof(RulePackSchema).Assembly;
            string? resource = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("tmforge-rules-v2.schema.json", StringComparison.OrdinalIgnoreCase));
            if (resource is null)
            {
                throw new InvalidOperationException("The version 2 rule-pack schema resource is missing.");
            }

            using Stream stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"The rule-pack schema resource '{resource}' could not be opened.");
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
