namespace ThreatModelForge.Engine
{
    using System.IO;
    using System.Text.Json;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// Maps between the canonical tmforge-json DTO and the engine <see cref="ThreatModel"/> through the
    /// single <c>tmforge-json</c> format provider, so the DTO-to-model projection lives in exactly one
    /// place for every facade (validate, convert, authoring).
    /// </summary>
    internal static class ModelDtoMapper
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>Builds an engine model from the canonical tmforge-json DTO.</summary>
        /// <param name="dto">The canonical model.</param>
        /// <returns>The engine model.</returns>
        internal static ThreatModel ToModel(TmForgeModelDto dto)
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(dto, Options);
            using (MemoryStream stream = new MemoryStream(json))
            {
                return new TmForgeJsonFormat().Read(stream);
            }
        }

        /// <summary>Projects an engine model back onto the canonical tmforge-json DTO.</summary>
        /// <param name="model">The engine model.</param>
        /// <returns>The canonical model.</returns>
        internal static TmForgeModelDto ToDto(ThreatModel model)
        {
            using (MemoryStream output = new MemoryStream())
            {
                new TmForgeJsonFormat().Write(model, output);
                return JsonSerializer.Deserialize<TmForgeModelDto>(output.ToArray(), Options)
                    ?? new TmForgeModelDto();
            }
        }
    }
}
