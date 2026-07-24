namespace ThreatModelForge.Api
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Configuration;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Resolves the custom analysis rule packs this API host serves. Packs are trusted deployment
    /// configuration, not request input: they are named by the operator, read once at startup, and then
    /// applied to every rule-reading endpoint, so a request can never inject rules and every caller of
    /// this host analyzes against exactly the same bundle.
    /// </summary>
    internal static class ApiRuleSources
    {
        /// <summary>The configuration key naming the rule packs (files or directories) to load.</summary>
        public const string ConfigurationKey = "TmForge:Rules";

        /// <summary>
        /// Reads the configured rule packs into engine rule options. Read failures and validation
        /// warnings are collected rather than thrown, and are reported through <c>/v1/rule-bundle</c> so
        /// a misconfigured deployment is visible instead of silently analyzing with built-in rules only.
        /// </summary>
        /// <param name="configuration">The host configuration.</param>
        /// <returns>The engine rule options and the diagnostics raised while reading them.</returns>
        public static (EngineRuleOptions Options, IReadOnlyList<string> Diagnostics) Load(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            List<string> diagnostics = new List<string>();
            IReadOnlyList<string> paths = ResolvePaths(configuration);
            if (paths.Count == 0)
            {
                return (new EngineRuleOptions(), diagnostics);
            }

            List<RuleSourceDto> sources = new List<RuleSourceDto>();
            foreach (RuleContent content in DeclarativeRuleProvider.ReadContents(paths, diagnostics.Add))
            {
                sources.Add(new RuleSourceDto { Name = content.Name, Json = content.ToJson() });
            }

            return (new EngineRuleOptions { Sources = sources }, diagnostics);
        }

        private static IReadOnlyList<string> ResolvePaths(IConfiguration configuration)
        {
            List<string> paths = new List<string>();
            foreach (IConfigurationSection child in configuration.GetSection(ConfigurationKey).GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(child.Value))
                {
                    paths.Add(child.Value!);
                }
            }

            if (paths.Count > 0)
            {
                return paths;
            }

            string? scalar = configuration[ConfigurationKey];
            if (string.IsNullOrWhiteSpace(scalar))
            {
                return paths;
            }

            foreach (string part in scalar!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    paths.Add(part.Trim());
                }
            }

            return paths;
        }
    }
}
