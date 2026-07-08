namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using ThreatModelForge.Editing;

    /// <summary>
    /// Loads <see cref="DeclarativeRule"/> instances from declarative spec files (<c>*.tmrules.json</c>).
    /// Loading is resilient: a spec file that cannot be read or parsed, or an individual rule that fails
    /// validation, is reported through the diagnostics sink and skipped rather than aborting the load, so
    /// one malformed rule never takes down the whole set. Property names are validated against the
    /// typed property schema and produce a warning (not a failure) when unknown, because custom
    /// properties are open-ended.
    /// </summary>
    public static class DeclarativeRuleProvider
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private static readonly HashSet<string> Kinds = new HashSet<string>(
            new[] { "process", "datastore", "external", "flow" },
            StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Loads all declarative rules from the given spec paths. A path may be a single
        /// <c>*.tmrules.json</c> file or a directory that is searched recursively for them.
        /// </summary>
        /// <param name="paths">The spec files or directories to load.</param>
        /// <param name="diagnostics">An optional sink for non-fatal load and validation warnings.</param>
        /// <returns>The loaded rules, in file then declaration order.</returns>
        public static IReadOnlyList<Rule> Load(IEnumerable<string> paths, Action<string>? diagnostics = null)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            List<Rule> rules = new List<Rule>();
            foreach (string path in paths)
            {
                foreach (string file in ExpandFiles(path, diagnostics))
                {
                    LoadFile(file, rules, diagnostics);
                }
            }

            return rules;
        }

        private static IEnumerable<string> ExpandFiles(string path, Action<string>? diagnostics)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Array.Empty<string>();
            }

            if (Directory.Exists(path))
            {
                return Directory.EnumerateFiles(path, "*.tmrules.json", SearchOption.AllDirectories)
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .ToList();
            }

            if (File.Exists(path))
            {
                return new[] { path };
            }

            diagnostics?.Invoke($"Rule source not found: '{path}'.");
            return Array.Empty<string>();
        }

        private static void LoadFile(string file, List<Rule> rules, Action<string>? diagnostics)
        {
            DeclarativeRuleFile? parsed;
            try
            {
                string json = File.ReadAllText(file);
                parsed = JsonSerializer.Deserialize<DeclarativeRuleFile>(json, Options);
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is UnauthorizedAccessException)
            {
                diagnostics?.Invoke($"Skipped rule file '{file}': {ex.Message}");
                return;
            }

            if (parsed?.Rules == null)
            {
                return;
            }

            foreach (DeclarativeRuleSpec spec in parsed.Rules)
            {
                Rule? rule = Compile(spec, file, diagnostics);
                if (rule != null)
                {
                    rules.Add(rule);
                }
            }
        }

        private static Rule? Compile(DeclarativeRuleSpec spec, string file, Action<string>? diagnostics)
        {
            string origin = $"rule '{spec.Id ?? "(no id)"}' in '{file}'";

            if (string.IsNullOrWhiteSpace(spec.Id))
            {
                diagnostics?.Invoke($"Skipped a rule in '{file}': missing 'id'.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(spec.AppliesTo) || !Kinds.Contains(spec.AppliesTo!))
            {
                diagnostics?.Invoke($"Skipped {origin}: 'appliesTo' must be one of process, datastore, external, flow.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(spec.Message))
            {
                diagnostics?.Invoke($"Skipped {origin}: missing 'message'.");
                return null;
            }

            if (spec.When == null && spec.Assert == null)
            {
                diagnostics?.Invoke($"Skipped {origin}: at least one of 'when' or 'assert' is required.");
                return null;
            }

            bool isFlow = string.Equals(spec.AppliesTo, "flow", StringComparison.OrdinalIgnoreCase);
            if (!isFlow && (UsesRelational(spec.When) || UsesRelational(spec.Assert)))
            {
                diagnostics?.Invoke($"Skipped {origin}: 'crossesTrustBoundary', 'source', and 'target' require appliesTo 'flow'.");
                return null;
            }

            if (!TryParseSeverity(spec.Severity, out MessageSeverity severity))
            {
                diagnostics?.Invoke($"Skipped {origin}: unknown severity '{spec.Severity}'.");
                return null;
            }

            if (!TryParseStride(spec.Stride, out StrideCategory? stride))
            {
                diagnostics?.Invoke($"Skipped {origin}: unknown stride '{spec.Stride}'.");
                return null;
            }

            Uri? helpUri = null;
            if (!string.IsNullOrWhiteSpace(spec.HelpUri) && !Uri.TryCreate(spec.HelpUri, UriKind.Absolute, out helpUri))
            {
                diagnostics?.Invoke($"Ignored invalid 'helpUri' on {origin}: '{spec.HelpUri}'.");
                helpUri = null;
            }

            List<ThreatReference> references = ParseReferences(spec.ThreatReferences, origin, diagnostics);
            WarnUnknownProperties(spec, origin, diagnostics);

            string message = spec.Message!;
            return new DeclarativeRule(
                spec.Id!,
                string.IsNullOrWhiteSpace(spec.Pack) ? "custom" : spec.Pack!,
                severity,
                spec.AppliesTo!,
                message,
                string.IsNullOrWhiteSpace(spec.FullDescription) ? message : spec.FullDescription!,
                spec.HelpText ?? string.Empty,
                helpUri,
                stride,
                references,
                spec.When,
                spec.Assert);
        }

        private static bool UsesRelational(DeclarativeCondition? condition)
        {
            return condition != null &&
                (condition.CrossesTrustBoundary.HasValue || condition.Source != null || condition.Target != null);
        }

        private static bool TryParseSeverity(string? value, out MessageSeverity severity)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                severity = MessageSeverity.Warning;
                return true;
            }

            return Enum.TryParse(value, ignoreCase: true, out severity);
        }

        private static bool TryParseStride(string? value, out StrideCategory? stride)
        {
            stride = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (Enum.TryParse(value, ignoreCase: true, out StrideCategory parsed))
            {
                stride = parsed;
                return true;
            }

            return false;
        }

        private static List<ThreatReference> ParseReferences(List<string>? references, string origin, Action<string>? diagnostics)
        {
            List<ThreatReference> result = new List<ThreatReference>();
            if (references == null)
            {
                return result;
            }

            foreach (string reference in references)
            {
                ThreatReference? parsed = ParseReference(reference);
                if (parsed == null)
                {
                    diagnostics?.Invoke($"Ignored unrecognized threat reference '{reference}' on {origin}.");
                    continue;
                }

                result.Add(parsed);
            }

            return result;
        }

        private static ThreatReference? ParseReference(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            int separator = reference.IndexOf(':');
            if (separator <= 0)
            {
                return null;
            }

            string catalog = reference.Substring(0, separator).Trim();
            string id = reference.Substring(separator + 1).Trim();
            if (id.Length == 0)
            {
                return null;
            }

            if (string.Equals(catalog, "CWE", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int cwe))
            {
                return ThreatReference.Cwe(cwe);
            }

            if (string.Equals(catalog, "CAPEC", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int capec))
            {
                return ThreatReference.Capec(capec);
            }

            if (string.Equals(catalog, "ATTACK", StringComparison.OrdinalIgnoreCase))
            {
                return ThreatReference.Attack(id);
            }

            return null;
        }

        private static void WarnUnknownProperties(DeclarativeRuleSpec spec, string origin, Action<string>? diagnostics)
        {
            if (diagnostics == null)
            {
                return;
            }

            WarnProperty(spec.AppliesTo!, spec.When?.Property, origin, diagnostics);
            WarnProperty(spec.AppliesTo!, spec.Assert?.Property, origin, diagnostics);
            WarnEndpointProperties(spec.When, origin, diagnostics);
            WarnEndpointProperties(spec.Assert, origin, diagnostics);
        }

        private static void WarnEndpointProperties(DeclarativeCondition? condition, string origin, Action<string> diagnostics)
        {
            if (condition?.Source?.Kind != null)
            {
                WarnProperty(condition.Source.Kind!, condition.Source.Property, origin, diagnostics);
            }

            if (condition?.Target?.Kind != null)
            {
                WarnProperty(condition.Target.Kind!, condition.Target.Property, origin, diagnostics);
            }
        }

        private static void WarnProperty(string appliesTo, string? property, string origin, Action<string> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(property))
            {
                return;
            }

            bool known = PropertySchemaCatalog.All.Any(descriptor =>
                string.Equals(descriptor.AppliesTo, appliesTo, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(descriptor.Name, property, StringComparison.OrdinalIgnoreCase));

            if (!known)
            {
                diagnostics($"{origin} reads property '{property}' on '{appliesTo}', which is not in the property schema.");
            }
        }
    }
}
