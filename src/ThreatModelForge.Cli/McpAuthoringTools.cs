namespace ThreatModelForge.Cli
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using ModelContextProtocol.Server;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Engine;

    /// <summary>
    /// MCP authoring tools over the stateless <see cref="AuthoringService"/> facade: declarative
    /// manifests (<c>apply</c>/<c>export_manifest</c>) and imperative edits (<c>add</c>/<c>connect</c>/
    /// <c>set</c>/<c>rename</c>/<c>remove</c>). Each edit takes a tmforge-json model in and returns the
    /// edited model out, so an agent can iterate: apply, analyze, edit, repeat.
    /// </summary>
    [McpServerToolType]
    public static class McpAuthoringTools
    {
        /// <summary>
        /// Materializes a declarative manifest into a model (all-or-nothing).
        /// </summary>
        /// <param name="manifest">The declarative manifest (see the manifest_schema tool).</param>
        /// <param name="force">Whether to store unknown/invalid property values instead of rejecting them.</param>
        /// <returns>The built model and the counts of what was created, or a blocking error.</returns>
        [McpServerTool(Name = "apply")]
        [Description("Materializes a declarative manifest into a model (all-or-nothing). Call the manifest_schema tool for the shape.")]
        public static ApplyResultDto Apply(
            [Description("The declarative manifest.")] Manifest manifest,
            [Description("Store unknown/invalid property values instead of rejecting them.")] bool force = false)
            => AuthoringService.Apply(manifest, force);

        /// <summary>
        /// Extracts a declarative manifest from a model.
        /// </summary>
        /// <param name="model">The tmforge-json model to capture.</param>
        /// <returns>The extracted manifest.</returns>
        [McpServerTool(Name = "export_manifest")]
        [Description("Extracts a declarative manifest from a model, so a model authored any way becomes a review-friendly source that round-trips with apply.")]
        public static Manifest ExportManifest([Description("The tmforge-json model to capture.")] TmForgeModelDto model)
            => AuthoringService.ExportManifest(model);

        /// <summary>
        /// Adds a process, data store, external interactor, or trust boundary to the model.
        /// </summary>
        /// <param name="model">The model to edit; omit or pass an empty model to start fresh.</param>
        /// <param name="kind">The element kind (process, store, external, boundary); optional when a stencil is set.</param>
        /// <param name="name">The display name.</param>
        /// <param name="stencilId">A concrete stencil id whose base primitive determines the kind.</param>
        /// <param name="page">The target page (1-based index or page name).</param>
        /// <param name="alias">A stable alias giving the element a deterministic id resolvable by later tools.</param>
        /// <param name="boundary">A trust-boundary reference (alias/name/id) to place the element inside.</param>
        /// <param name="properties">Typed properties to stamp (validated against property_schema).</param>
        /// <param name="force">Whether to store unknown/invalid property values instead of rejecting them.</param>
        /// <returns>The edited model and the new element id, or a blocking error.</returns>
        [McpServerTool(Name = "add")]
        [Description("Adds a process, data store, external interactor, or trust boundary. Give an alias for a stable id you can reference later.")]
        public static AuthoringResultDto Add(
            [Description("The tmforge-json model to edit; omit or pass an empty model to start fresh.")] TmForgeModelDto? model = null,
            [Description("The element kind: process, store, external, or boundary. Optional when stencilId is set.")] string? kind = null,
            [Description("The display name.")] string? name = null,
            [Description("A concrete stencil id (from the stencils tool) whose base determines the kind.")] string? stencilId = null,
            [Description("The target page (1-based index or page name).")] string? page = null,
            [Description("A stable alias giving the element a deterministic id, resolvable by later tools.")] string? alias = null,
            [Description("A trust-boundary reference (alias/name/id) to place the element inside.")] string? boundary = null,
            [Description("Typed properties to stamp (validated against property_schema).")] IReadOnlyDictionary<string, string>? properties = null,
            [Description("Store unknown/invalid property values instead of rejecting them.")] bool force = false)
        {
            if (!AuthoringSupport.TryResolveKind(kind, stencilId, out StencilKind resolvedKind, out StencilDto? stencil, out string? error))
            {
                return new AuthoringResultDto { Success = false, Error = error };
            }

            AddRequest request = new AddRequest
            {
                Kind = resolvedKind,
                Stencil = stencil,
                Name = name,
                Page = page,
                Alias = alias,
                Boundary = boundary,
                Properties = McpToolSupport.ToAssignments(properties),
                Force = force,
            };
            return AuthoringService.Add(model, request);
        }

        /// <summary>
        /// Adds a data-flow connector between two existing elements.
        /// </summary>
        /// <param name="model">The tmforge-json model to edit.</param>
        /// <param name="source">The source element reference (id, alias, or unique name).</param>
        /// <param name="target">The target element reference (id, alias, or unique name).</param>
        /// <param name="name">The flow name.</param>
        /// <param name="page">The page (1-based index or page name).</param>
        /// <param name="properties">Typed flow properties (validated against property_schema).</param>
        /// <param name="force">Whether to store unknown/invalid property values instead of rejecting them.</param>
        /// <returns>The edited model, the connector id, and the resolved endpoints, or a blocking error.</returns>
        [McpServerTool(Name = "connect")]
        [Description("Adds a data-flow connector between two existing elements (resolved by id, alias, or unique name).")]
        public static AuthoringResultDto Connect(
            [Description("The tmforge-json model to edit.")] TmForgeModelDto model,
            [Description("The source element reference.")] string source,
            [Description("The target element reference.")] string target,
            [Description("The flow name.")] string? name = null,
            [Description("The page (1-based index or page name).")] string? page = null,
            [Description("Typed flow properties, e.g. Protocol, Port, DataType.")] IReadOnlyDictionary<string, string>? properties = null,
            [Description("Store unknown/invalid property values instead of rejecting them.")] bool force = false)
        {
            ConnectRequest request = new ConnectRequest
            {
                Source = source,
                Target = target,
                Name = name,
                Page = page,
                Properties = McpToolSupport.ToAssignments(properties),
                Force = force,
            };
            return AuthoringService.Connect(model, request);
        }

        /// <summary>
        /// Sets the name and/or typed properties of an existing element or flow (for example, to resolve a finding).
        /// </summary>
        /// <param name="model">The tmforge-json model to edit.</param>
        /// <param name="id">The element or flow reference (id, alias, or unique name).</param>
        /// <param name="name">A new name, or omit to leave it unchanged.</param>
        /// <param name="page">The page (1-based index or page name).</param>
        /// <param name="properties">Typed properties to set (validated against property_schema).</param>
        /// <param name="force">Whether to store unknown/invalid property values instead of rejecting them.</param>
        /// <returns>The edited model and the resolved id, or a blocking error.</returns>
        [McpServerTool(Name = "set")]
        [Description("Sets the name and/or typed properties of an existing element or flow, e.g. Protocol=HTTPS to resolve a finding.")]
        public static AuthoringResultDto Set(
            [Description("The tmforge-json model to edit.")] TmForgeModelDto model,
            [Description("The element or flow reference (id, alias, or unique name).")] string id,
            [Description("A new name, or omit to leave unchanged.")] string? name = null,
            [Description("The page (1-based index or page name).")] string? page = null,
            [Description("Typed properties to set (validated against property_schema).")] IReadOnlyDictionary<string, string>? properties = null,
            [Description("Store unknown/invalid property values instead of rejecting them.")] bool force = false)
        {
            SetRequest request = new SetRequest
            {
                Id = id,
                Name = name,
                Page = page,
                Properties = McpToolSupport.ToAssignments(properties),
                Force = force,
            };
            return AuthoringService.Set(model, request);
        }

        /// <summary>
        /// Renames an existing element.
        /// </summary>
        /// <param name="model">The tmforge-json model to edit.</param>
        /// <param name="id">The element reference (id, alias, or unique name).</param>
        /// <param name="name">The new display name.</param>
        /// <param name="page">The page (1-based index or page name).</param>
        /// <returns>The edited model and the resolved id, or a blocking error.</returns>
        [McpServerTool(Name = "rename")]
        [Description("Renames an existing element.")]
        public static AuthoringResultDto Rename(
            [Description("The tmforge-json model to edit.")] TmForgeModelDto model,
            [Description("The element reference (id, alias, or unique name).")] string id,
            [Description("The new display name.")] string name,
            [Description("The page (1-based index or page name).")] string? page = null)
            => AuthoringService.Rename(model, new RenameRequest { Id = id, Name = name, Page = page });

        /// <summary>
        /// Removes an element and any data flows attached to it.
        /// </summary>
        /// <param name="model">The tmforge-json model to edit.</param>
        /// <param name="id">The element reference (id, alias, or unique name).</param>
        /// <param name="page">The page (1-based index or page name).</param>
        /// <returns>The edited model and the removed identifiers, or a blocking error.</returns>
        [McpServerTool(Name = "remove")]
        [Description("Removes an element and any data flows attached to it.")]
        public static AuthoringResultDto Remove(
            [Description("The tmforge-json model to edit.")] TmForgeModelDto model,
            [Description("The element reference (id, alias, or unique name).")] string id,
            [Description("The page (1-based index or page name).")] string? page = null)
            => AuthoringService.Remove(model, new RemoveRequest { Id = id, Page = page });
    }
}
