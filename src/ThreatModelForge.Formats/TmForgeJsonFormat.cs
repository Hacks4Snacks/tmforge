namespace ThreatModelForge.Formats
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
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

        /// <summary>
        /// The stable identity for the single implicit page of a flat (non-<c>diagrams</c>) document,
        /// so separate reads of single-page models share a surface id and the merge folds one side's
        /// additions onto the other's page instead of spawning a second page.
        /// </summary>
        private static readonly Guid DefaultSurfaceGuid = new Guid("7e3f1d52-0000-4000-8000-000000000001");

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
            DiagramEditor editor = new DiagramEditor(model);

            if (document.Diagrams != null && document.Diagrams.Count > 0)
            {
                int index = 1;
                foreach (TmForgeJsonDiagram page in document.Diagrams)
                {
                    string header = string.IsNullOrEmpty(page.Name)
                        ? "Diagram " + index.ToString(CultureInfo.InvariantCulture)
                        : page.Name;
                    DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = ResolveSurfaceGuid(page.Id), Header = header };
                    model.DrawingSurfaceList.Add(surface);
                    PopulateSurface(editor, surface, page.Elements, page.Flows);
                    index++;
                }
            }
            else
            {
                DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = DefaultSurfaceGuid, Header = "Diagram 1" };
                model.DrawingSurfaceList.Add(surface);
                PopulateSurface(editor, surface, document.Elements, document.Flows);
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

            List<TmForgeJsonDiagram> diagrams = new List<TmForgeJsonDiagram>();
            int index = 1;
            foreach (DrawingSurfaceModel surface in model.DrawingSurfaceList)
            {
                (TmForgeJsonElement[] pageElements, TmForgeJsonFlow[] pageFlows) = ExtractSurface(surface);
                string name = string.IsNullOrEmpty(surface.Header)
                    ? "Diagram " + index.ToString(CultureInfo.InvariantCulture)
                    : surface.Header!;
                diagrams.Add(new TmForgeJsonDiagram
                {
                    Id = surface.Guid.ToString(),
                    Name = name,
                    Elements = pageElements,
                    Flows = pageFlows,
                });
                index++;
            }

            // The top-level elements/flows mirror the first page so single-page readers keep working;
            // the diagrams array is emitted only when the model has more than one page.
            TmForgeJsonModel document = new TmForgeJsonModel
            {
                Schema = SchemaToken,
                Version = "0.1",
                Elements = diagrams.Count > 0 ? diagrams[0].Elements : Array.Empty<TmForgeJsonElement>(),
                Flows = diagrams.Count > 0 ? diagrams[0].Flows : Array.Empty<TmForgeJsonFlow>(),
                Diagrams = diagrams.Count > 1 ? diagrams.ToArray() : null,
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

        private static void PopulateSurface(
            DiagramEditor editor,
            DrawingSurfaceModel surface,
            IReadOnlyList<TmForgeJsonElement>? elements,
            IReadOnlyList<TmForgeJsonFlow>? flows)
        {
            Dictionary<string, Guid> idMap = new Dictionary<string, Guid>(StringComparer.Ordinal);

            foreach (TmForgeJsonElement element in elements ?? Array.Empty<TmForgeJsonElement>())
            {
                StencilKind kind = MapKind(element.Kind);
                Guid guid = editor.AddElement(surface, kind, element.X, element.Y);
                guid = PreserveId(surface.Borders, guid, element.Id);
                editor.SetElementName(surface, guid, element.Name ?? string.Empty);
                if (kind == StencilKind.TrustBoundary)
                {
                    editor.ResizeElement(surface, guid, element.X, element.Y, element.Width ?? 260, element.Height ?? 180);
                }

                ApplyProperties(surface.Borders[guid] as Entity, element.Properties);

                if (!string.IsNullOrEmpty(element.Id))
                {
                    idMap[element.Id] = guid;
                }
            }

            foreach (TmForgeJsonFlow flow in flows ?? Array.Empty<TmForgeJsonFlow>())
            {
                if (idMap.TryGetValue(flow.Source, out Guid source) && idMap.TryGetValue(flow.Target, out Guid target))
                {
                    Guid connector = editor.AddConnector(surface, source, target);
                    connector = PreserveId(surface.Lines, connector, flow.Id);
                    editor.SetElementName(surface, connector, flow.Name ?? string.Empty);
                    ApplyProperties(surface.Lines[connector] as Entity, flow.Properties);
                }
            }
        }

        /// <summary>
        /// Resolves a diagram's surface identity from the id carried by the source document when it
        /// is a GUID (so pages align across files for the three-way merge), or a fresh id otherwise.
        /// </summary>
        /// <param name="id">The diagram id from the source document, if any.</param>
        /// <returns>The surface <see cref="Guid"/> to use.</returns>
        private static Guid ResolveSurfaceGuid(string? id)
        {
            return Guid.TryParse(id, out Guid parsed) ? parsed : Guid.NewGuid();
        }

        /// <summary>
        /// Re-keys a freshly created element to the identifier carried by the source document when
        /// that identifier is a GUID, so element identity survives the tmforge-json round-trip (which
        /// the structural diff and three-way merge match on). Non-GUID ids keep the generated one.
        /// </summary>
        /// <param name="elements">The surface dictionary (borders or lines) the element lives in.</param>
        /// <param name="current">The generated identifier the element was added under.</param>
        /// <param name="id">The identifier from the source document, if any.</param>
        /// <returns>The identifier the element is keyed under after this call.</returns>
        private static Guid PreserveId(IDictionary<Guid, object> elements, Guid current, string? id)
        {
            if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out Guid parsed) || parsed == current || elements.ContainsKey(parsed))
            {
                return current;
            }

            if (elements.TryGetValue(current, out object? value) && value is Entity entity)
            {
                elements.Remove(current);
                entity.Guid = parsed;
                elements[parsed] = entity;
                return parsed;
            }

            return current;
        }

        private static (TmForgeJsonElement[] Elements, TmForgeJsonFlow[] Flows) ExtractSurface(DrawingSurfaceModel surface)
        {
            List<TmForgeJsonElement> elements = new List<TmForgeJsonElement>();
            List<TmForgeJsonFlow> flows = new List<TmForgeJsonFlow>();

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

            return (elements.ToArray(), flows.ToArray());
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
