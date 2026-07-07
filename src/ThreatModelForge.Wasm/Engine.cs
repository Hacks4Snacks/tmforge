namespace ThreatModelForge.Wasm
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices.JavaScript;
    using System.Text.Json;
    using ThreatModelForge.Engine;

    /// <summary>
    /// The in-browser engine interop surface. These <c>[JSExport]</c> methods are a thin
    /// marshaling shim over the shared <see cref="EngineService"/> — the SAME facade the <c>/v1</c> API
    /// calls — so the browser runs identical engine logic with no network. tmforge-json crosses the JS
    /// boundary as a string; <c>.tm7</c> bytes as base64.
    /// </summary>
    public static partial class Engine
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// Sanity check that the module loaded and interop works.
        /// </summary>
        /// <returns>The engine runtime banner.</returns>
        [JSExport]
        public static string Ping()
            => $".NET {Environment.Version} WASM engine ready";

        /// <summary>
        /// Runs the shared engine's analysis rule set over a tmforge-json model.
        /// </summary>
        /// <param name="tmforgeJson">The canonical tmforge-json model.</param>
        /// <returns>The findings as a JSON array (camelCase, the /v1 FindingDto shape).</returns>
        [JSExport]
        public static string Validate(string tmforgeJson)
        {
            IReadOnlyList<FindingDto> findings = EngineService.Validate(Deserialize(tmforgeJson));
            return JsonSerializer.Serialize(findings, JsonOptions);
        }

        /// <summary>
        /// Serializes a tmforge-json model to lossless <c>.tm7</c> bytes, returned as base64.
        /// </summary>
        /// <param name="tmforgeJson">The canonical tmforge-json model.</param>
        /// <returns>The <c>.tm7</c> document bytes, base64-encoded.</returns>
        [JSExport]
        public static string WriteTm7(string tmforgeJson)
            => Convert.ToBase64String(EngineService.ExportTm7(Deserialize(tmforgeJson)));

        /// <summary>
        /// Reads a <c>.tm7</c> document (base64) and projects it onto the canonical tmforge-json model.
        /// </summary>
        /// <param name="tm7Base64">The <c>.tm7</c> document bytes, base64-encoded.</param>
        /// <returns>The canonical tmforge-json model.</returns>
        [JSExport]
        public static string ReadTm7(string tm7Base64)
        {
            TmForgeModelDto model = EngineService.ReadModel(Convert.FromBase64String(tm7Base64), "tm7");
            return JsonSerializer.Serialize(model, JsonOptions);
        }

        private static TmForgeModelDto Deserialize(string tmforgeJson)
            => JsonSerializer.Deserialize<TmForgeModelDto>(tmforgeJson, JsonOptions) ?? new TmForgeModelDto();
    }
}
