namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Shared helpers for the <c>tmforge mcp</c> tool classes: property-assignment marshaling and the
    /// grounding text an agent needs to author declarative manifests.
    /// </summary>
    internal static class McpToolSupport
    {
        /// <summary>
        /// A human-readable description of the declarative manifest shape accepted by the
        /// <c>apply</c> tool, returned by the <c>manifest_schema</c> tool as agent grounding.
        /// </summary>
        public const string ManifestSchemaText =
            "A declarative threat-model manifest is a JSON object with these fields:\n" +
            "{\n" +
            "  \"name\": \"<model title>\",\n" +
            "  \"boundaries\": [ { \"alias\": \"<stable id>\", \"name\": \"<label>\" } ],\n" +
            "  \"elements\": [ {\n" +
            "    \"alias\": \"<stable id>\",\n" +
            "    \"kind\": \"process | store | external\",   // omit when 'stencil' is set\n" +
            "    \"name\": \"<label>\",\n" +
            "    \"stencil\": \"<stencil id from the stencils tool>\",  // optional\n" +
            "    \"boundary\": \"<boundary alias>\",           // optional\n" +
            "    \"props\": { \"<Key>\": \"<Value>\" }            // optional, validated against property_schema\n" +
            "  } ],\n" +
            "  \"flows\": [ {\n" +
            "    \"from\": \"<element alias or unique name>\",\n" +
            "    \"to\": \"<element alias or unique name>\",\n" +
            "    \"name\": \"<label>\",\n" +
            "    \"props\": { \"Protocol\": \"HTTPS\", \"Port\": \"443\" }   // optional\n" +
            "  } ]\n" +
            "}\n" +
            "Elements and flow endpoints are referenced by alias (or unique name), so the manifest needs no GUIDs " +
            "and round-trips with the export_manifest tool. Property values are validated against the property_schema " +
            "tool's catalog; pass force=true to store unknown names or values.";

        private const int MaxPages = 128;
        private const int MaxElements = 10000;
        private const int MaxFlows = 20000;
        private const int MaxThreats = 20000;
        private const int MaxProperties = 100000;
        private const int MaxListItems = 100000;
        private const int MaxGeneratedItems = 100000;
        private const int MaxStringLength = 65536;
        private const long MaxTextCharacters = 16L * 1024 * 1024;
        private const int MaxResponseCharacters = 32 * 1024 * 1024;

        /// <summary>
        /// Marshals a property map (a JSON object of key/value strings) into the <c>KEY=VALUE</c>
        /// assignment list the authoring requests consume.
        /// </summary>
        /// <param name="properties">The property map, or <see langword="null"/>.</param>
        /// <returns>The assignments, or an empty list.</returns>
        public static IReadOnlyList<string> ToAssignments(IReadOnlyDictionary<string, string>? properties)
        {
            if (properties == null || properties.Count == 0)
            {
                return Array.Empty<string>();
            }

            List<string> assignments = new List<string>(properties.Count);
            foreach (KeyValuePair<string, string> pair in properties)
            {
                assignments.Add(pair.Key + "=" + pair.Value);
            }

            return assignments;
        }

        /// <summary>Validates a model against the MCP request complexity budget.</summary>
        /// <param name="model">The model to validate, or <see langword="null"/>.</param>
        public static void ValidateModel(TmForgeModelDto? model)
        {
            ValidateModels(model);
        }

        /// <summary>Validates several models against one shared MCP operation budget.</summary>
        /// <param name="models">The models participating in one operation.</param>
        public static void ValidateModels(params TmForgeModelDto?[] models)
        {
            Budget budget = new Budget();
            foreach (TmForgeModelDto? model in models)
            {
                AddModel(budget, model);
            }
        }

        /// <summary>Validates generated findings before returning them to an MCP caller.</summary>
        /// <param name="findings">The generated findings.</param>
        /// <returns>The unchanged findings.</returns>
        public static IReadOnlyList<FindingDto> ValidateResponse(IReadOnlyList<FindingDto> findings)
        {
            if (findings.Count > MaxGeneratedItems)
            {
                throw new InvalidDataException($"MCP response exceeds the limit of {MaxGeneratedItems} findings.");
            }

            Budget budget = new Budget();
            foreach (FindingDto finding in findings)
            {
                budget.AddText(finding.Id);
                budget.AddText(finding.Severity);
                budget.AddText(finding.RuleId);
                budget.AddText(finding.Message);
                budget.AddTexts(finding.ElementIds);
            }

            return findings;
        }

        /// <summary>Validates generated threats before returning them to an MCP caller.</summary>
        /// <param name="threats">The generated threats.</param>
        /// <returns>The unchanged threats.</returns>
        public static IReadOnlyList<ThreatDto> ValidateResponse(IReadOnlyList<ThreatDto> threats)
        {
            if (threats.Count > MaxGeneratedItems)
            {
                throw new InvalidDataException($"MCP response exceeds the limit of {MaxGeneratedItems} threats.");
            }

            Budget budget = new Budget();
            foreach (ThreatDto threat in threats)
            {
                budget.AddText(threat.Id);
                budget.AddText(threat.RuleId);
                budget.AddText(threat.Category);
                budget.AddText(threat.Title);
                budget.AddText(threat.Mitigation);
                budget.AddText(threat.Severity);
                budget.AddText(threat.Priority);
                budget.AddTexts(threat.References);
                budget.AddTexts(threat.ElementIds);
                budget.AddText(threat.Interaction);
                budget.AddText(threat.State);
                budget.AddText(threat.Justification);
                budget.AddText(threat.Description);
            }

            return threats;
        }

        /// <summary>Validates generated text before returning it to an MCP caller.</summary>
        /// <param name="text">The response text.</param>
        /// <returns>The unchanged text.</returns>
        public static string ValidateResponse(string text)
        {
            if (text.Length > MaxResponseCharacters)
            {
                throw new InvalidDataException($"MCP response exceeds the limit of {MaxResponseCharacters} characters.");
            }

            return text;
        }

        /// <summary>Validates a merge result before returning it to an MCP caller.</summary>
        /// <param name="result">The merge result.</param>
        /// <returns>The unchanged merge result.</returns>
        public static MergeResultDto ValidateResult(MergeResultDto result)
        {
            ValidateModel(result.Merged);
            int conflicts = result.Conflicts?.Count ?? 0;
            if (conflicts > MaxGeneratedItems)
            {
                throw new InvalidDataException($"MCP response exceeds the limit of {MaxGeneratedItems} merge conflicts.");
            }

            return result;
        }

        /// <summary>Validates an authoring manifest against the MCP request complexity budget.</summary>
        /// <param name="manifest">The manifest to validate.</param>
        public static void ValidateManifest(Manifest manifest)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            Budget budget = new Budget();
            budget.AddText(manifest.Name);
            if (manifest.Boundaries != null)
            {
                budget.AddElements(manifest.Boundaries.Count);
                foreach (ManifestBoundary boundary in manifest.Boundaries)
                {
                    budget.AddText(boundary.Alias);
                    budget.AddText(boundary.Name);
                }
            }

            if (manifest.Elements != null)
            {
                budget.AddElements(manifest.Elements.Count);
                foreach (ManifestElement element in manifest.Elements)
                {
                    budget.AddText(element.Alias);
                    budget.AddText(element.Kind);
                    budget.AddText(element.Name);
                    budget.AddText(element.Stencil);
                    budget.AddText(element.Boundary);
                    budget.AddProperties(element.Props);
                }
            }

            if (manifest.Flows != null)
            {
                budget.AddFlows(manifest.Flows.Count);
                foreach (ManifestFlow flow in manifest.Flows)
                {
                    budget.AddText(flow.From);
                    budget.AddText(flow.To);
                    budget.AddText(flow.Name);
                    budget.AddProperties(flow.Props);
                }
            }
        }

        /// <summary>Validates direct string and property arguments before an MCP tool processes them.</summary>
        /// <param name="values">The string arguments to validate.</param>
        /// <param name="properties">An optional property map to validate.</param>
        public static void ValidateArguments(
            IEnumerable<string?> values,
            IReadOnlyDictionary<string, string>? properties = null)
        {
            Budget budget = new Budget();
            foreach (string? value in values)
            {
                budget.AddText(value);
            }

            budget.AddProperties(properties);
        }

        /// <summary>Validates the model in an authoring result and returns the same result.</summary>
        /// <param name="result">The result to validate.</param>
        /// <returns>The unchanged result.</returns>
        public static AuthoringResultDto ValidateResult(AuthoringResultDto result)
        {
            ValidateModel(result.Model);
            return result;
        }

        /// <summary>Validates the model in an apply result and returns the same result.</summary>
        /// <param name="result">The result to validate.</param>
        /// <returns>The unchanged result.</returns>
        public static ApplyResultDto ValidateResult(ApplyResultDto result)
        {
            ValidateModel(result.Model);
            return result;
        }

        private static void AddModel(Budget budget, TmForgeModelDto? model)
        {
            if (model == null)
            {
                return;
            }

            budget.AddText(model.Schema);
            budget.AddText(model.Version);
            AddElements(budget, model.Elements);
            AddFlows(budget, model.Flows);
            if (model.Diagrams != null && model.Diagrams.Count > 0)
            {
                budget.AddPages(model.Diagrams.Count);
                foreach (TmForgeDiagramDto diagram in model.Diagrams)
                {
                    budget.AddText(diagram.Id);
                    budget.AddText(diagram.Name);
                    AddElements(budget, diagram.Elements);
                    AddFlows(budget, diagram.Flows);
                }
            }

            if (model.Analysis != null)
            {
                budget.AddTexts(model.Analysis.DisabledPacks);
                budget.AddTexts(model.Analysis.DisabledRuleIds);
            }

            IReadOnlyList<ThreatStateDto>? threats = model.Threats;
            if (threats != null)
            {
                budget.AddThreats(threats.Count);
                foreach (ThreatStateDto threat in threats)
                {
                    budget.AddText(threat.Id);
                    budget.AddText(threat.State);
                    budget.AddText(threat.Justification);
                    budget.AddText(threat.Category);
                    budget.AddText(threat.Title);
                    budget.AddText(threat.Description);
                    budget.AddText(threat.Mitigation);
                    budget.AddText(threat.Priority);
                    budget.AddTexts(threat.ElementIds);
                }
            }
        }

        private static void AddElements(Budget budget, IReadOnlyList<TmForgeElementDto>? elements)
        {
            if (elements == null)
            {
                return;
            }

            budget.AddElements(elements.Count);
            foreach (TmForgeElementDto element in elements)
            {
                budget.AddText(element.Id);
                budget.AddText(element.Kind);
                budget.AddText(element.Name);
                budget.AddProperties(element.Properties);
            }
        }

        private static void AddFlows(Budget budget, IReadOnlyList<TmForgeFlowDto>? flows)
        {
            if (flows == null)
            {
                return;
            }

            budget.AddFlows(flows.Count);
            foreach (TmForgeFlowDto flow in flows)
            {
                budget.AddText(flow.Id);
                budget.AddText(flow.Source);
                budget.AddText(flow.Target);
                budget.AddText(flow.Name);
                budget.AddProperties(flow.Properties);
            }
        }

        private sealed class Budget
        {
            private int elements;
            private int flows;
            private int listItems;
            private int pages;
            private int properties;
            private long textCharacters;
            private int threats;

            public void AddElements(int count)
            {
                this.elements = CheckedTotal(this.elements, count, MaxElements, "elements");
            }

            public void AddFlows(int count)
            {
                this.flows = CheckedTotal(this.flows, count, MaxFlows, "flows");
            }

            public void AddPages(int count)
            {
                this.pages = CheckedTotal(this.pages, count, MaxPages, "pages");
            }

            public void AddThreats(int count)
            {
                this.threats = CheckedTotal(this.threats, count, MaxThreats, "threats");
            }

            public void AddProperties(IReadOnlyDictionary<string, string>? values)
            {
                if (values == null)
                {
                    return;
                }

                this.properties = CheckedTotal(this.properties, values.Count, MaxProperties, "properties");
                foreach (KeyValuePair<string, string> value in values)
                {
                    this.AddText(value.Key);
                    this.AddText(value.Value);
                }
            }

            public void AddTexts(IEnumerable<string>? values)
            {
                if (values == null)
                {
                    return;
                }

                foreach (string value in values)
                {
                    this.listItems = CheckedTotal(this.listItems, 1, MaxListItems, "nested list items");
                    this.AddText(value);
                }
            }

            public void AddText(string? value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return;
                }

                if (value!.Length > MaxStringLength)
                {
                    throw new InvalidDataException($"MCP model string exceeds the limit of {MaxStringLength} characters.");
                }

                this.textCharacters += value.Length;
                if (this.textCharacters > MaxTextCharacters)
                {
                    throw new InvalidDataException($"MCP model text exceeds the limit of {MaxTextCharacters} characters.");
                }
            }

            private static int CheckedTotal(int current, int added, int limit, string name)
            {
                if (added < 0 || added > limit - current)
                {
                    throw new InvalidDataException($"MCP model exceeds the limit of {limit} {name}.");
                }

                return current + added;
            }
        }
    }
}
