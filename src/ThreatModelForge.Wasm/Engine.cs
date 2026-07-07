namespace ThreatModelForge.Wasm
{
    using System;
    using System.Runtime.InteropServices.JavaScript;
    using System.Text.Json;
    using ThreatModelForge.Engine;

    /// <summary>
    /// The in-browser engine interop surface. These <c>[JSExport]</c> methods are a thin marshaling
    /// shim over the shared <see cref="EngineService"/> — the SAME facade the <c>/v1</c> API calls — so
    /// the browser runs identical engine logic with no network. tmforge-json crosses the JS boundary as
    /// a string; binary documents (<c>.tm7</c>, <c>.drawio</c>, <c>.vsdx</c>, reports) as base64.
    /// </summary>
    public static partial class Engine
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>Sanity check that the module loaded and interop works.</summary>
        /// <returns>The engine runtime banner.</returns>
        [JSExport]
        public static string Ping()
            => $".NET {Environment.Version} WASM engine ready";

        /// <summary>Lists the registered file formats and their capabilities.</summary>
        /// <returns>The formats as a JSON array.</returns>
        [JSExport]
        public static string Formats()
            => Serialize(EngineService.GetFormats());

        /// <summary>Lists the authoring stencil catalog offered to the palette.</summary>
        /// <returns>The stencils as a JSON array.</returns>
        [JSExport]
        public static string Stencils()
            => Serialize(EngineService.GetStencils());

        /// <summary>Lists the stencil packs offered to the palette.</summary>
        /// <returns>The stencil packs as a JSON array.</returns>
        [JSExport]
        public static string StencilPacks()
            => Serialize(EngineService.GetStencilPacks());

        /// <summary>Lists the analysis rules offered by the engine.</summary>
        /// <returns>The rules as a JSON array.</returns>
        [JSExport]
        public static string Rules()
            => Serialize(EngineService.GetRules());

        /// <summary>Lists the rule packs offered by the engine.</summary>
        /// <returns>The rule packs as a JSON array.</returns>
        [JSExport]
        public static string RulePacks()
            => Serialize(EngineService.GetRulePacks());

        /// <summary>Lists the typed element-property schema.</summary>
        /// <returns>The property descriptors as a JSON array.</returns>
        [JSExport]
        public static string PropertySchema()
            => Serialize(EngineService.GetPropertySchema());

        /// <summary>Runs the shared engine's analysis rule set over a tmforge-json model.</summary>
        /// <param name="tmforgeJson">The canonical tmforge-json model.</param>
        /// <returns>The findings as a JSON array (the /v1 FindingDto shape).</returns>
        [JSExport]
        public static string Validate(string tmforgeJson)
            => Serialize(EngineService.Validate(Deserialize(tmforgeJson)));

        /// <summary>Merges two edited tmforge-json models, keyed by element identity.</summary>
        /// <param name="baseJson">The common ancestor model, or an empty string for a two-way merge.</param>
        /// <param name="oursJson">The local model.</param>
        /// <param name="theirsJson">The incoming model.</param>
        /// <returns>The merged model and conflicts as JSON (the /v1 MergeResultDto shape).</returns>
        [JSExport]
        public static string Merge(string baseJson, string oursJson, string theirsJson)
            => Serialize(EngineService.Merge(
                string.IsNullOrWhiteSpace(baseJson) ? null : Deserialize(baseJson),
                Deserialize(oursJson),
                Deserialize(theirsJson)));

        /// <summary>Detects the format of a document, or returns an empty string when none matches.</summary>
        /// <param name="contentBase64">The raw document bytes, base64-encoded.</param>
        /// <returns>The detected format as JSON, or an empty string when unrecognized.</returns>
        [JSExport]
        public static string Detect(string contentBase64)
        {
            FormatDto? format = EngineService.Detect(Convert.FromBase64String(contentBase64));
            return format is null ? string.Empty : Serialize(format);
        }

        /// <summary>Reads a document in any registered format into the canonical tmforge-json model.</summary>
        /// <param name="contentBase64">The raw document bytes, base64-encoded.</param>
        /// <param name="formatId">An explicit format id, or an empty string to content-sniff.</param>
        /// <returns>The canonical tmforge-json model.</returns>
        [JSExport]
        public static string ReadFile(string contentBase64, string formatId)
        {
            TmForgeModelDto model = EngineService.ReadModel(
                Convert.FromBase64String(contentBase64),
                string.IsNullOrEmpty(formatId) ? null : formatId);
            return Serialize(model);
        }

        /// <summary>Serializes a tmforge-json model to lossless <c>.tm7</c> bytes, returned as base64.</summary>
        /// <param name="tmforgeJson">The canonical tmforge-json model.</param>
        /// <returns>The <c>.tm7</c> document bytes, base64-encoded.</returns>
        [JSExport]
        public static string ExportTm7(string tmforgeJson)
            => Convert.ToBase64String(EngineService.ExportTm7(Deserialize(tmforgeJson)));

        /// <summary>Serializes a tmforge-json model to another registered format, returned as base64.</summary>
        /// <param name="tmforgeJson">The canonical tmforge-json model.</param>
        /// <param name="toFormatId">The target format id (for example <c>drawio</c>, <c>vsdx</c>, <c>tm7</c>).</param>
        /// <returns>The serialized document bytes, base64-encoded.</returns>
        [JSExport]
        public static string ConvertModel(string tmforgeJson, string toFormatId)
            => Convert.ToBase64String(EngineService.Convert(Deserialize(tmforgeJson), toFormatId));

        /// <summary>Renders an HTML or SVG report for a tmforge-json model, returned as base64.</summary>
        /// <param name="tmforgeJson">The canonical tmforge-json model.</param>
        /// <param name="format">The report format: <c>html</c> or <c>svg</c>.</param>
        /// <returns>The report bytes (UTF-8), base64-encoded.</returns>
        [JSExport]
        public static string Report(string tmforgeJson, string format)
            => Convert.ToBase64String(EngineService.Report(Deserialize(tmforgeJson), format));

        private static string Serialize<T>(T value)
            => JsonSerializer.Serialize(value, JsonOptions);

        private static TmForgeModelDto Deserialize(string tmforgeJson)
            => JsonSerializer.Deserialize<TmForgeModelDto>(tmforgeJson, JsonOptions) ?? new TmForgeModelDto();
    }
}
