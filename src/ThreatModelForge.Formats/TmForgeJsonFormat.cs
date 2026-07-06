namespace ThreatModelForge.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// The <c>tmforge-json</c> format provider: the canvas's canonical wire model. It reads and
    /// writes the structural diagram (elements, flows, trust boundaries, names, and geometry) the
    /// React front end edits, reconstructing a <see cref="ThreatModel"/> via the UI-agnostic
    /// <see cref="DiagramEditor"/>.
    /// </summary>
    public sealed class TmForgeJsonFormat : IThreatModelFormat
    {
        /// <summary>The stable format identifier.</summary>
        public const string FormatId = "tmforge-json";

        private const string SchemaToken = "tmforge-json";

        private static readonly IReadOnlyList<string> SupportedExtensions = new[] { ".tmforge.json" };

        private static readonly FormatCapabilities JsonCapabilities = new FormatCapabilities(
            canRead: true,
            canWrite: true,
            roundTrips: false,
            fidelityNote: "The canvas wire model: elements, flows, trust boundaries, names, and geometry. Knowledge-base attributes and generated threats are not represented.");

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <inheritdoc/>
        public string Id => FormatId;

        /// <inheritdoc/>
        public string DisplayName => "Threat Model Forge JSON (.tmforge.json)";

        /// <inheritdoc/>
        public IReadOnlyList<string> Extensions => SupportedExtensions;

        /// <inheritdoc/>
        public FormatCapabilities Capabilities => JsonCapabilities;

        /// <summary>
        /// Reads only the per-model validation selection from a <c>tmforge-json</c> stream, without
        /// building the full model. Used to honor a model's rule selection when linting.
        /// </summary>
        /// <param name="stream">The source stream positioned at the start of the document.</param>
        /// <param name="disabledPacks">On return, the rule pack ids to skip (empty when none).</param>
        /// <param name="disabledRuleIds">On return, the rule ids to skip (empty when none).</param>
        /// <returns><c>true</c> if the document declared any pack or rule to skip; otherwise <c>false</c>.</returns>
        public static bool TryReadValidation(
            Stream stream,
            out IReadOnlyList<string> disabledPacks,
            out IReadOnlyList<string> disabledRuleIds)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            disabledPacks = Array.Empty<string>();
            disabledRuleIds = Array.Empty<string>();

            string text;
            using (StreamReader reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true))
            {
                text = reader.ReadToEnd();
            }

            TmForgeJsonModel? document = JsonSerializer.Deserialize<TmForgeJsonModel>(text, SerializerOptions);
            if (document?.Validation == null)
            {
                return false;
            }

            disabledPacks = document.Validation.DisabledPacks ?? Array.Empty<string>();
            disabledRuleIds = document.Validation.DisabledRuleIds ?? Array.Empty<string>();
            return disabledPacks.Count > 0 || disabledRuleIds.Count > 0;
        }

        /// <inheritdoc/>
        public bool CanRead(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanSeek)
            {
                throw new NotSupportedException("Content sniffing requires a seekable stream.");
            }

            long originalPosition = stream.Position;
            try
            {
                using (StreamReader reader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: true))
                {
                    char[] buffer = new char[512];
                    int count = reader.Read(buffer, 0, buffer.Length);
                    string prefix = new string(buffer, 0, count);
                    return prefix.IndexOf(SchemaToken, StringComparison.Ordinal) >= 0;
                }
            }
            finally
            {
                stream.Position = originalPosition;
            }
        }

        /// <inheritdoc/>
        public ThreatModel Read(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            string text;
            using (StreamReader reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true))
            {
                text = reader.ReadToEnd();
            }

            TmForgeJsonModel document = JsonSerializer.Deserialize<TmForgeJsonModel>(text, SerializerOptions)
                ?? new TmForgeJsonModel();

            ThreatModel model = new ThreatModel { Version = "1.0" };
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Diagram 1" };
            model.DrawingSurfaceList.Add(diagram);

            DiagramEditor editor = new DiagramEditor(model);
            Dictionary<string, Guid> idMap = new Dictionary<string, Guid>(StringComparer.Ordinal);

            foreach (TmForgeJsonElement element in document.Elements ?? Array.Empty<TmForgeJsonElement>())
            {
                StencilKind kind = MapKind(element.Kind);
                Guid guid = editor.AddElement(diagram, kind, element.X, element.Y);
                editor.SetElementName(diagram, guid, element.Name ?? string.Empty);
                if (kind == StencilKind.TrustBoundary)
                {
                    editor.ResizeElement(diagram, guid, element.X, element.Y, element.Width ?? 260, element.Height ?? 180);
                }

                ApplyProperties(diagram.Borders[guid] as Entity, element.Properties);

                if (!string.IsNullOrEmpty(element.Id))
                {
                    idMap[element.Id] = guid;
                }
            }

            foreach (TmForgeJsonFlow flow in document.Flows ?? Array.Empty<TmForgeJsonFlow>())
            {
                if (idMap.TryGetValue(flow.Source, out Guid source) && idMap.TryGetValue(flow.Target, out Guid target))
                {
                    Guid connector = editor.AddConnector(diagram, source, target);
                    editor.SetElementName(diagram, connector, flow.Name ?? string.Empty);
                    ApplyProperties(diagram.Lines[connector] as Entity, flow.Properties);
                }
            }

            return model;
        }

        /// <inheritdoc/>
        public void Write(ThreatModel model, Stream stream)
        {
            this.Write(model, stream, null);
        }

        /// <summary>
        /// Writes the model to <c>tmforge-json</c>, embedding the given per-model validation selection.
        /// </summary>
        /// <param name="model">The model to write.</param>
        /// <param name="stream">The destination stream.</param>
        /// <param name="validation">The validation selection to embed, or <see langword="null"/> to omit it.</param>
        public void Write(ThreatModel model, Stream stream, TmForgeJsonValidation? validation)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            List<TmForgeJsonElement> elements = new List<TmForgeJsonElement>();
            List<TmForgeJsonFlow> flows = new List<TmForgeJsonFlow>();

            foreach (DrawingSurfaceModel surface in model.DrawingSurfaceList)
            {
                foreach (DrawingElement element in surface.Borders.Values.OfType<DrawingElement>())
                {
                    elements.Add(new TmForgeJsonElement
                    {
                        Id = element.Guid.ToString(),
                        Kind = KindOf(element),
                        Name = DiagramElementHelper.GetName(element),
                        X = element.Left,
                        Y = element.Top,
                        Width = element.Width,
                        Height = element.Height,
                        Properties = DiagramElementHelper.GetCustomProperties(element),
                    });
                }

                foreach (Connector connector in surface.Lines.Values.OfType<Connector>())
                {
                    flows.Add(new TmForgeJsonFlow
                    {
                        Id = connector.Guid.ToString(),
                        Source = connector.SourceGuid.ToString(),
                        Target = connector.TargetGuid.ToString(),
                        Name = DiagramElementHelper.GetName(connector),
                        Properties = DiagramElementHelper.GetCustomProperties(connector),
                    });
                }
            }

            TmForgeJsonModel document = new TmForgeJsonModel
            {
                Schema = SchemaToken,
                Version = "0.1",
                Elements = elements.ToArray(),
                Flows = flows.ToArray(),
                Validation = validation,
            };

            string json = JsonSerializer.Serialize(document, SerializerOptions);
            using (StreamWriter writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024,
                leaveOpen: true))
            {
                writer.Write(json);
            }
        }

        private static void ApplyProperties(Entity? entity, IReadOnlyDictionary<string, string>? properties)
        {
            if (entity == null || properties == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> pair in properties)
            {
                DiagramElementHelper.SetCustomProperty(entity, pair.Key, pair.Value);
            }
        }

        private static StencilKind MapKind(string? kind)
        {
            switch (kind)
            {
                case "datastore":
                    return StencilKind.DataStore;
                case "external":
                    return StencilKind.ExternalEntity;
                case "boundary":
                    return StencilKind.TrustBoundary;
                default:
                    return StencilKind.Process;
            }
        }

        private static string KindOf(DrawingElement element)
        {
            return element switch
            {
                BorderBoundary => "boundary",
                StencilEllipse => "process",
                StencilParallelLines => "datastore",
                _ => "external",
            };
        }
    }
}
