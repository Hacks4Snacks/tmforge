namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Builds a <see cref="ThreatModel"/> from a declarative <see cref="Manifest"/> (for
    /// <c>tmforge apply</c>) and extracts a manifest from a model (for <c>tmforge export</c>). The
    /// build is all-or-nothing: it reports the first blocking problem and produces no partial model,
    /// so the caller can write atomically. Element references (aliases, unique names) and boundary
    /// membership reuse the same resolution the imperative verbs use.
    /// </summary>
    internal static class ManifestSupport
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
        };

        /// <summary>Deserializes a manifest from JSON text.</summary>
        /// <param name="json">The manifest JSON.</param>
        /// <returns>The manifest, or <see langword="null"/> when the JSON is the literal <c>null</c>.</returns>
        public static Manifest? Deserialize(string json)
        {
            return JsonSerializer.Deserialize<Manifest>(json, SerializerOptions);
        }

        /// <summary>Serializes a manifest to indented, camelCase JSON.</summary>
        /// <param name="manifest">The manifest to serialize.</param>
        /// <returns>The JSON text.</returns>
        public static string Serialize(Manifest manifest)
        {
            return JsonSerializer.Serialize(manifest, SerializerOptions);
        }

        /// <summary>
        /// Materializes a manifest into a model. Boundaries are laid out top to bottom and each element
        /// is placed inside its declared boundary (so trust-boundary crossings are computed correctly),
        /// with a deterministic id when it declares an alias.
        /// </summary>
        /// <param name="manifest">The manifest to build.</param>
        /// <param name="force">Whether to store unknown/invalid property values instead of rejecting them.</param>
        /// <param name="model">On success, the built model.</param>
        /// <param name="summary">On success, the counts of what was built.</param>
        /// <param name="error">On failure, a message describing the first blocking problem.</param>
        /// <returns><see langword="true"/> when the whole manifest was applied.</returns>
        public static bool Build(Manifest manifest, bool force, out ThreatModel model, out ManifestSummary summary, out string? error)
        {
            error = null;
            summary = default;
            model = new ThreatModel { Version = "1.0" };
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Diagram 1" };
            model.DrawingSurfaceList.Add(diagram);
            if (!string.IsNullOrWhiteSpace(manifest.Name))
            {
                model.MetaInformation ??= new MetaInformation();
                model.MetaInformation.ThreatModelName = manifest.Name;
            }

            List<ManifestBoundary> boundaries = manifest.Boundaries ?? new List<ManifestBoundary>();
            List<ManifestElement> elements = manifest.Elements ?? new List<ManifestElement>();
            List<ManifestFlow> flows = manifest.Flows ?? new List<ManifestFlow>();

            DiagramEditor editor = new DiagramEditor(model);
            HashSet<Guid> aliasIds = new HashSet<Guid>();

            Dictionary<string, int> memberCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (ManifestElement element in elements)
            {
                if (!string.IsNullOrEmpty(element.Boundary))
                {
                    memberCounts.TryGetValue(element.Boundary!, out int count);
                    memberCounts[element.Boundary!] = count + 1;
                }
            }

            Dictionary<string, BoundaryBox> boxes = new Dictionary<string, BoundaryBox>(StringComparer.OrdinalIgnoreCase);
            int cursorY = 40;
            foreach (ManifestBoundary boundary in boundaries)
            {
                int members = !string.IsNullOrEmpty(boundary.Alias) && memberCounts.TryGetValue(boundary.Alias!, out int m) ? m : 0;
                int rows = Math.Max(1, (members + 2) / 3);
                int width = 24 + (3 * 120) + 24;
                int height = 48 + (rows * 84) + 24;
                int x = 40;
                int y = cursorY;

                Guid id = editor.AddElement(diagram, StencilKind.TrustBoundary, x, y);
                editor.ResizeElement(diagram, id, x, y, width, height);
                Entity? boundaryEntity = DiagramEditor.FindElement(diagram, id);
                if (boundaryEntity != null && !string.IsNullOrWhiteSpace(boundary.Name))
                {
                    DiagramElementHelper.SetName(boundaryEntity, boundary.Name!);
                }

                if (!string.IsNullOrEmpty(boundary.Alias))
                {
                    if (!TryAssignAlias(diagram, id, boundary.Alias!, aliasIds, out _, out error))
                    {
                        return false;
                    }

                    boxes[boundary.Alias!] = new BoundaryBox(x, y);
                }

                cursorY = y + height + 40;
            }

            int unassigned = 0;
            foreach (ManifestElement element in elements)
            {
                if (!TryResolveKind(element, out StencilKind kind, out StencilDto? stencil, out error))
                {
                    return false;
                }

                (int ex, int ey) = NextElementPosition(element.Boundary, boxes, ref unassigned, cursorY);
                Guid id = editor.AddElement(diagram, kind, ex, ey);
                Entity? added = DiagramEditor.FindElement(diagram, id);
                if (added == null)
                {
                    error = "Failed to create element.";
                    return false;
                }

                string? name = element.Name ?? stencil?.Label;
                if (!string.IsNullOrEmpty(name))
                {
                    DiagramElementHelper.SetName(added, name!);
                }

                if (stencil != null)
                {
                    DiagramElementHelper.SetCustomProperty(added, "StencilType", stencil.Id);
                    foreach (KeyValuePair<string, string> preset in stencil.Defaults)
                    {
                        DiagramElementHelper.SetCustomProperty(added, preset.Key, preset.Value);
                    }
                }

                if (!string.IsNullOrEmpty(element.Boundary))
                {
                    DiagramElementHelper.SetCustomProperty(added, AuthoringSupport.BoundaryPropertyName, element.Boundary!);
                }

                if (element.Props != null && element.Props.Count > 0 &&
                    !AuthoringSupport.TryApplyProperties(added, ToAssignments(element.Props), AuthoringSupport.SchemaBase(kind), force, out error, out _))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(element.Alias) && !TryAssignAlias(diagram, id, element.Alias!, aliasIds, out _, out error))
                {
                    return false;
                }
            }

            foreach (ManifestFlow flow in flows)
            {
                if (string.IsNullOrEmpty(flow.From) || string.IsNullOrEmpty(flow.To))
                {
                    error = "Each flow needs a 'from' and a 'to'.";
                    return false;
                }

                if (!AuthoringSupport.TryResolveElementId(model, diagram, flow.From!, out Guid source, out error) ||
                    !AuthoringSupport.TryResolveElementId(model, diagram, flow.To!, out Guid target, out error))
                {
                    return false;
                }

                if (!diagram.Borders.ContainsKey(source))
                {
                    error = "Flow source is not a component: " + flow.From;
                    return false;
                }

                if (!diagram.Borders.ContainsKey(target))
                {
                    error = "Flow target is not a component: " + flow.To;
                    return false;
                }

                Guid id = editor.AddConnector(diagram, source, target);
                Entity? connector = DiagramEditor.FindElement(diagram, id);
                if (connector != null && !string.IsNullOrEmpty(flow.Name))
                {
                    DiagramElementHelper.SetName(connector, flow.Name!);
                }

                if (connector != null && flow.Props != null && flow.Props.Count > 0 &&
                    !AuthoringSupport.TryApplyProperties(connector, ToAssignments(flow.Props), "flow", force, out error, out _))
                {
                    return false;
                }
            }

            summary = new ManifestSummary(boundaries.Count, elements.Count, flows.Count);
            return true;
        }

        /// <summary>
        /// Extracts a manifest from a model: boundaries, elements, and flows across every page, with
        /// each flow endpoint referenced by the element's alias (or name). Geometry is intentionally
        /// dropped so the manifest is a stable, review-friendly authoring source.
        /// </summary>
        /// <param name="model">The model to capture.</param>
        /// <returns>The extracted manifest.</returns>
        public static Manifest Extract(ThreatModel model)
        {
            List<ManifestBoundary> boundaries = new List<ManifestBoundary>();
            List<ManifestElement> elements = new List<ManifestElement>();
            List<ManifestFlow> flows = new List<ManifestFlow>();

            foreach (DrawingSurfaceModel surface in model.DrawingSurfaceList)
            {
                foreach (Entity entity in surface.Borders.Values.OfType<Entity>())
                {
                    IReadOnlyDictionary<string, string> props = DiagramElementHelper.GetCustomProperties(entity);
                    string? alias = props.TryGetValue(AuthoringSupport.AliasPropertyName, out string? a) ? a : null;
                    string name = DiagramElementHelper.GetName(entity);
                    if (entity is BorderBoundary)
                    {
                        boundaries.Add(new ManifestBoundary { Alias = alias, Name = NullIfEmpty(name) });
                        continue;
                    }

                    Dictionary<string, string> userProps = FilterProps(props);
                    elements.Add(new ManifestElement
                    {
                        Alias = alias,
                        Kind = KindNoun(entity),
                        Name = NullIfEmpty(name),
                        Stencil = props.TryGetValue("StencilType", out string? stencil) ? stencil : null,
                        Boundary = props.TryGetValue(AuthoringSupport.BoundaryPropertyName, out string? boundary) ? boundary : null,
                        Props = userProps.Count > 0 ? userProps : null,
                    });
                }

                foreach (Connector connector in surface.Lines.Values.OfType<Connector>())
                {
                    Dictionary<string, string> userProps = FilterProps(DiagramElementHelper.GetCustomProperties(connector));
                    flows.Add(new ManifestFlow
                    {
                        From = ReferenceFor(surface, connector.SourceGuid),
                        To = ReferenceFor(surface, connector.TargetGuid),
                        Name = NullIfEmpty(DiagramElementHelper.GetName(connector)),
                        Props = userProps.Count > 0 ? userProps : null,
                    });
                }
            }

            return new Manifest
            {
                Name = NullIfEmpty(model.MetaInformation?.ThreatModelName ?? string.Empty),
                Boundaries = boundaries.Count > 0 ? boundaries : null,
                Elements = elements.Count > 0 ? elements : null,
                Flows = flows.Count > 0 ? flows : null,
            };
        }

        private static bool TryResolveKind(ManifestElement element, out StencilKind kind, out StencilDto? stencil, out string? error)
        {
            error = null;
            stencil = null;
            if (!string.IsNullOrEmpty(element.Stencil))
            {
                stencil = StencilCatalog.Find(element.Stencil!);
                if (stencil == null)
                {
                    kind = StencilKind.Process;
                    error = "Unknown stencil: " + element.Stencil + " (run 'tmforge stencils').";
                    return false;
                }

                if (!AuthoringSupport.TryParseKind(stencil.Base, out kind))
                {
                    error = "Stencil '" + stencil.Id + "' has an unrecognized base primitive: " + stencil.Base + ".";
                    return false;
                }

                return true;
            }

            if (!string.IsNullOrEmpty(element.Kind))
            {
                if (!AuthoringSupport.TryParseKind(element.Kind!, out kind))
                {
                    error = "Unknown element kind: " + element.Kind + " (expected process, store, external, or boundary).";
                    return false;
                }

                return true;
            }

            kind = StencilKind.Process;
            error = "Each element needs a 'kind' or a 'stencil'.";
            return false;
        }

        private static bool TryAssignAlias(DrawingSurfaceModel diagram, Guid current, string alias, HashSet<Guid> used, out Guid assigned, out string? error)
        {
            error = null;
            Guid desired = AuthoringSupport.DeterministicId(alias);
            if (!used.Add(desired))
            {
                assigned = current;
                error = "Duplicate alias '" + alias + "'; aliases must be unique within a model.";
                return false;
            }

            Entity? entity = DiagramEditor.FindElement(diagram, current);
            if (entity != null)
            {
                DiagramElementHelper.SetCustomProperty(entity, AuthoringSupport.AliasPropertyName, alias);
            }

            AuthoringSupport.RekeyComponent(diagram, current, desired);
            assigned = desired;
            return true;
        }

        private static (int X, int Y) NextElementPosition(string? boundaryAlias, Dictionary<string, BoundaryBox> boxes, ref int unassigned, int unassignedBaseY)
        {
            if (!string.IsNullOrEmpty(boundaryAlias) && boxes.TryGetValue(boundaryAlias!, out BoundaryBox? box))
            {
                int index = box.Cursor;
                box.Cursor = index + 1;
                return (box.X + 24 + ((index % 3) * 120), box.Y + 48 + ((index / 3) * 84));
            }

            int k = unassigned;
            unassigned = k + 1;
            return (40 + ((k % 5) * 150), unassignedBaseY + ((k / 5) * 90));
        }

        private static IReadOnlyList<string> ToAssignments(Dictionary<string, string> props)
        {
            List<string> assignments = new List<string>();
            foreach (KeyValuePair<string, string> pair in props)
            {
                assignments.Add(pair.Key + "=" + pair.Value);
            }

            return assignments;
        }

        private static Dictionary<string, string> FilterProps(IReadOnlyDictionary<string, string> props)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in props)
            {
                if (string.Equals(pair.Key, AuthoringSupport.AliasPropertyName, StringComparison.Ordinal) ||
                    string.Equals(pair.Key, AuthoringSupport.BoundaryPropertyName, StringComparison.Ordinal) ||
                    string.Equals(pair.Key, "StencilType", StringComparison.Ordinal))
                {
                    continue;
                }

                result[pair.Key] = pair.Value;
            }

            return result;
        }

        private static string ReferenceFor(DrawingSurfaceModel surface, Guid guid)
        {
            Entity? entity = DiagramEditor.FindElement(surface, guid);
            if (entity == null)
            {
                return guid.ToString();
            }

            IReadOnlyDictionary<string, string> props = DiagramElementHelper.GetCustomProperties(entity);
            if (props.TryGetValue(AuthoringSupport.AliasPropertyName, out string? alias) && !string.IsNullOrEmpty(alias))
            {
                return alias!;
            }

            string name = DiagramElementHelper.GetName(entity);
            return string.IsNullOrWhiteSpace(name) ? guid.ToString() : name;
        }

        private static string KindNoun(Entity entity)
        {
            return AuthoringSupport.SchemaBase(entity) switch
            {
                "datastore" => "store",
                "external" => "external",
                _ => "process",
            };
        }

        private static string? NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private sealed class BoundaryBox
        {
            public BoundaryBox(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }

            public int X { get; }

            public int Y { get; }

            public int Cursor { get; set; }
        }
    }
}
