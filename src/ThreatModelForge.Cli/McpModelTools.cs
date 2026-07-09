namespace ThreatModelForge.Cli
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Text;
    using ModelContextProtocol.Server;
    using ThreatModelForge.Engine;

    /// <summary>
    /// MCP tools for reading, saving, analyzing, and reporting on threat models. Analysis tools are
    /// stateless: they take a canonical tmforge-json model in and return findings, threats, or a
    /// report out.
    /// </summary>
    [McpServerToolType]
    public static class McpModelTools
    {
        /// <summary>
        /// Reads a threat model file from a local path and returns the canonical tmforge-json model.
        /// </summary>
        /// <param name="path">The local file path to read.</param>
        /// <param name="format">An optional explicit format id; omit to auto-detect by content.</param>
        /// <returns>The canonical model.</returns>
        [McpServerTool(Name = "read")]
        [Description("Reads a threat model file (.tm7, .tmforge.json, .drawio, .vsdx) from a local path and returns the canonical tmforge-json model.")]
        public static TmForgeModelDto Read(
            [Description("The local file path to read.")] string path,
            [Description("Optional explicit format id (tm7, tmforge-json, drawio, vsdx); omit to auto-detect.")] string? format = null)
        {
            byte[] content = File.ReadAllBytes(path);
            return EngineService.ReadModel(content, format);
        }

        /// <summary>
        /// Detects the file format of a local threat model document by content sniffing.
        /// </summary>
        /// <param name="path">The local file path to inspect.</param>
        /// <returns>The detected format, or <see langword="null"/> when none matches.</returns>
        [McpServerTool(Name = "detect")]
        [Description("Detects the file format of a local threat model document by content sniffing.")]
        public static FormatDto? Detect([Description("The local file path to inspect.")] string path)
        {
            byte[] content = File.ReadAllBytes(path);
            return EngineService.Detect(content);
        }

        /// <summary>
        /// Writes a tmforge-json model to a local file, materializing it as <c>.tm7</c> or another format.
        /// </summary>
        /// <param name="model">The tmforge-json model to write.</param>
        /// <param name="path">The local file path to write.</param>
        /// <param name="format">An optional format id; omit to infer from the path extension.</param>
        /// <returns>Where the model was written, in what format, and how many bytes.</returns>
        [McpServerTool(Name = "save")]
        [Description("Writes a tmforge-json model to a local file (default format inferred from the extension). Use this to materialize .tm7/.vsdx/.drawio.")]
        public static McpSaveResult Save(
            [Description("The tmforge-json model to write.")] TmForgeModelDto model,
            [Description("The local file path to write.")] string path,
            [Description("Optional format id (tm7, tmforge-json, drawio, vsdx); omit to infer from the path extension.")] string? format = null)
        {
            string formatId = string.IsNullOrEmpty(format) ? InferFormat(path) : format!;
            byte[] content = EngineService.Convert(model, formatId);
            File.WriteAllBytes(path, content);
            return new McpSaveResult { Path = path, Format = formatId, Bytes = content.Length };
        }

        /// <summary>
        /// Runs the analysis rule set over the model and returns the findings.
        /// </summary>
        /// <param name="model">The tmforge-json model to analyze.</param>
        /// <returns>The findings.</returns>
        [McpServerTool(Name = "analyze")]
        [Description("Runs the analysis rule set over the model and returns the findings (rule id, severity, message, affected element ids).")]
        public static IReadOnlyList<FindingDto> Analyze([Description("The tmforge-json model to analyze.")] TmForgeModelDto model)
            => EngineService.Analyze(model);

        /// <summary>
        /// Projects the model's findings into STRIDE threats (the persistable, triaged view).
        /// </summary>
        /// <param name="model">The tmforge-json model.</param>
        /// <returns>The generated threats.</returns>
        [McpServerTool(Name = "threats")]
        [Description("Projects the model's findings into STRIDE threats (the persistable, triaged view with title, mitigation, and references).")]
        public static IReadOnlyList<ThreatDto> Threats([Description("The tmforge-json model.")] TmForgeModelDto model)
            => EngineService.GenerateThreats(model);

        /// <summary>
        /// Renders a self-contained report for the model as text (HTML or SVG).
        /// </summary>
        /// <param name="model">The tmforge-json model.</param>
        /// <param name="format">The report format: <c>html</c> (default) or <c>svg</c>.</param>
        /// <returns>The report content.</returns>
        [McpServerTool(Name = "report")]
        [Description("Renders a self-contained report for the model as text. Format 'html' (default) or 'svg'.")]
        public static string Report(
            [Description("The tmforge-json model.")] TmForgeModelDto model,
            [Description("The report format: html or svg.")] string format = "html")
            => Encoding.UTF8.GetString(EngineService.Report(model, format));

        /// <summary>
        /// Merges two edited models by element identity, reporting conflicts (resolved to <c>ours</c>).
        /// </summary>
        /// <param name="ours">The local model.</param>
        /// <param name="theirs">The incoming model.</param>
        /// <param name="baseModel">An optional common ancestor for a three-way merge; omit for two-way.</param>
        /// <returns>The merged model and any conflicts.</returns>
        [McpServerTool(Name = "merge")]
        [Description("Three-way (or two-way when base is omitted) identity-keyed merge of two edited models; conflicts are resolved to 'ours' and reported.")]
        public static MergeResultDto Merge(
            [Description("The local model.")] TmForgeModelDto ours,
            [Description("The incoming model.")] TmForgeModelDto theirs,
            [Description("Optional common ancestor for a three-way merge; omit for two-way.")] TmForgeModelDto? baseModel = null)
            => EngineService.Merge(baseModel, ours, theirs);

        private static string InferFormat(string path)
        {
            string extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            switch (extension)
            {
                case "vsdx":
                    return "vsdx";
                case "drawio":
                    return "drawio";
                case "json":
                    return "tmforge-json";
                default:
                    return "tm7";
            }
        }
    }
}
