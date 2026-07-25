namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using ModelContextProtocol.Server;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Engine;

    /// <summary>
    /// MCP tools that expose the engine's grounding catalogs — the formats, stencils, typed property
    /// schema, rules, and manifest shape an agent needs to author and analyze models with confidence.
    /// </summary>
    [McpServerToolType]
    public static class McpGroundingTools
    {
        /// <summary>Lists the file formats tmforge can read and write.</summary>
        /// <returns>The available formats.</returns>
        [McpServerTool(Name = "formats")]
        [Description("Lists the file formats tmforge can read and write, with their capabilities.")]
        public static IReadOnlyList<FormatDto> Formats() => EngineService.GetFormats();

        /// <summary>Lists the built-in authoring stencils.</summary>
        /// <returns>The available stencils.</returns>
        [McpServerTool(Name = "stencils")]
        [Description("Lists the built-in authoring stencils; use a stencil id with the 'add' tool or a manifest element.")]
        public static IReadOnlyList<StencilDto> Stencils() => EngineService.GetStencils();

        /// <summary>Lists the typed element/flow property schema the analysis rules read.</summary>
        /// <returns>The property descriptors across all DFD primitives.</returns>
        [McpServerTool(Name = "property_schema")]
        [Description("Lists the typed element/flow property schema (names, kinds, allowed values, defaults) the analysis rules read.")]
        public static IReadOnlyList<ThreatModelForge.Editing.PropertyDescriptor> PropertySchema() => EngineService.GetPropertySchema();

        /// <summary>Lists the analysis rules the engine evaluates.</summary>
        /// <param name="services">The MCP request services.</param>
        /// <param name="rulesPath">An optional custom rule pack inside the workspace root.</param>
        /// <returns>The available rules.</returns>
        [McpServerTool(Name = "rules")]
        [Description("Lists the analysis rules the engine evaluates, with pack, severity, and help link.")]
        public static IReadOnlyList<RuleDto> Rules(
            IServiceProvider services,
            [Description("Optional path to a custom .tmrules.json pack inside the configured MCP workspace root; omit to list the built-in rules only.")] string? rulesPath = null)
            => EngineService.GetRules(McpToolSupport.LoadRules(services, rulesPath));

        /// <summary>Lists the analysis rule packs available for per-model toggles.</summary>
        /// <param name="services">The MCP request services.</param>
        /// <param name="rulesPath">An optional custom rule pack inside the workspace root.</param>
        /// <returns>The available rule packs.</returns>
        [McpServerTool(Name = "rule_packs")]
        [Description("Lists the analysis rule packs (groupings of rules) available for per-model toggles.")]
        public static IReadOnlyList<RulePackDto> RulePacks(
            IServiceProvider services,
            [Description("Optional path to a custom .tmrules.json pack inside the configured MCP workspace root; omit to list the built-in packs only.")] string? rulesPath = null)
            => EngineService.GetRulePacks(McpToolSupport.LoadRules(services, rulesPath));

        /// <summary>Describes the declarative manifest shape accepted by the <c>apply</c> tool.</summary>
        /// <returns>The manifest grounding text.</returns>
        [McpServerTool(Name = "manifest_schema")]
        [Description("Describes the declarative manifest shape accepted by the 'apply' tool.")]
        public static string ManifestSchema() => McpToolSupport.ManifestSchemaText;
    }
}
