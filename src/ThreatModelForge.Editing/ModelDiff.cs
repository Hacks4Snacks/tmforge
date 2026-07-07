namespace ThreatModelForge.Editing
{
    using System;
    using System.Collections.Generic;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Computes a structural difference between two threat models, matched by the stable
    /// <see cref="Entity.Guid"/> that every element carries. Because elements are matched by identity
    /// rather than by serialized text, a model that was only re-laid-out or re-serialized produces no
    /// differences, and a renamed or re-typed element is reported as a single modification rather than
    /// a delete plus an add. Geometry (position and size) is intentionally ignored so the diff stays
    /// focused on semantically meaningful content: an element's name, kind, custom properties, and —
    /// for a flow — its endpoints.
    /// </summary>
    public static class ModelDiff
    {
        private const string NameKey = "name";
        private const string KindKey = "kind";
        private const string SourceKey = "source";
        private const string TargetKey = "target";

        /// <summary>
        /// Compares two models and returns the elements added, removed, and modified between them.
        /// </summary>
        /// <param name="baseModel">The base (left-hand) model.</param>
        /// <param name="revisedModel">The revised (right-hand) model.</param>
        /// <returns>The structural difference between the two models.</returns>
        public static ModelDifference Compare(ThreatModel baseModel, ThreatModel revisedModel)
        {
            if (baseModel == null)
            {
                throw new ArgumentNullException(nameof(baseModel));
            }

            if (revisedModel == null)
            {
                throw new ArgumentNullException(nameof(revisedModel));
            }

            Dictionary<Guid, ElementSnapshot> before = Snapshot(baseModel);
            Dictionary<Guid, ElementSnapshot> after = Snapshot(revisedModel);

            List<ElementChange> added = new List<ElementChange>();
            List<ElementChange> removed = new List<ElementChange>();
            List<ElementChange> modified = new List<ElementChange>();

            foreach (KeyValuePair<Guid, ElementSnapshot> entry in after)
            {
                if (!before.ContainsKey(entry.Key))
                {
                    added.Add(entry.Value.ToChange(ChangeKind.Added, Array.Empty<PropertyChange>()));
                }
            }

            foreach (KeyValuePair<Guid, ElementSnapshot> entry in before)
            {
                if (!after.TryGetValue(entry.Key, out ElementSnapshot? revised))
                {
                    removed.Add(entry.Value.ToChange(ChangeKind.Removed, Array.Empty<PropertyChange>()));
                    continue;
                }

                List<PropertyChange> changes = DiffAttributes(entry.Value, revised);
                if (changes.Count > 0)
                {
                    modified.Add(revised.ToChange(ChangeKind.Modified, changes));
                }
            }

            added.Sort(CompareChanges);
            removed.Sort(CompareChanges);
            modified.Sort(CompareChanges);

            return new ModelDifference { Added = added, Removed = removed, Modified = modified };
        }

        private static Dictionary<Guid, ElementSnapshot> Snapshot(ThreatModel model)
        {
            Dictionary<Guid, ElementSnapshot> map = new Dictionary<Guid, ElementSnapshot>();
            foreach (DrawingSurfaceModel surface in model.DrawingSurfaceList)
            {
                string diagramName = string.IsNullOrEmpty(surface.Header) ? "Diagram" : surface.Header!;
                Collect(surface.Borders, surface, diagramName, map);
                Collect(surface.Lines, surface, diagramName, map);
            }

            return map;
        }

        private static void Collect(IDictionary<Guid, object> elements, DrawingSurfaceModel surface, string diagramName, IDictionary<Guid, ElementSnapshot> map)
        {
            foreach (KeyValuePair<Guid, object> pair in elements)
            {
                if (!(pair.Value is Entity entity))
                {
                    continue;
                }

                Guid id = entity.Guid != Guid.Empty ? entity.Guid : pair.Key;
                string kind = Classify(entity);
                Dictionary<string, string> attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [NameKey] = DiagramElementHelper.GetName(entity),
                    [KindKey] = kind,
                };

                if (entity is LineElement line)
                {
                    attributes[SourceKey] = line.SourceGuid.ToString();
                    attributes[TargetKey] = line.TargetGuid.ToString();
                }

                foreach (KeyValuePair<string, string> property in DiagramElementHelper.GetCustomProperties(entity))
                {
                    attributes[property.Key] = property.Value;
                }

                map[id] = new ElementSnapshot
                {
                    Id = id,
                    Kind = kind,
                    Name = attributes[NameKey],
                    DiagramName = diagramName,
                    DiagramId = surface.Guid,
                    Attributes = attributes,
                };
            }
        }

        private static string Classify(Entity entity)
        {
            string? typeId = string.IsNullOrEmpty(entity.TypeId) ? entity.GenericTypeId : entity.TypeId;
            switch (typeId)
            {
                case "GE.P":
                    return "process";
                case "GE.EI":
                    return "external";
                case "GE.DS":
                    return "store";
                case "GE.TB.B":
                    return "boundary";
                case "GE.DF":
                    return "flow";
                default:
                    break;
            }

            if (entity is Connector)
            {
                return "flow";
            }

            if (entity is BorderBoundary || entity is LineBoundary)
            {
                return "boundary";
            }

            return typeId ?? entity.GetType().Name;
        }

        private static List<PropertyChange> DiffAttributes(ElementSnapshot before, ElementSnapshot after)
        {
            HashSet<string> keys = new HashSet<string>(before.Attributes.Keys, StringComparer.Ordinal);
            keys.UnionWith(after.Attributes.Keys);

            List<PropertyChange> changes = new List<PropertyChange>();
            foreach (string key in keys)
            {
                before.Attributes.TryGetValue(key, out string? from);
                after.Attributes.TryGetValue(key, out string? to);
                if (!string.Equals(from, to, StringComparison.Ordinal))
                {
                    changes.Add(new PropertyChange { Key = key, From = from, To = to });
                }
            }

            changes.Sort(ComparePropertyChanges);
            return changes;
        }

        private static int ComparePropertyChanges(PropertyChange left, PropertyChange right)
        {
            int byRank = KeyRank(left.Key).CompareTo(KeyRank(right.Key));
            return byRank != 0 ? byRank : string.CompareOrdinal(left.Key, right.Key);
        }

        private static int KeyRank(string key)
        {
            switch (key)
            {
                case NameKey:
                    return 0;
                case KindKey:
                    return 1;
                case SourceKey:
                    return 2;
                case TargetKey:
                    return 3;
                default:
                    return 4;
            }
        }

        private static int CompareChanges(ElementChange left, ElementChange right)
        {
            int byDiagram = string.CompareOrdinal(left.DiagramName, right.DiagramName);
            if (byDiagram != 0)
            {
                return byDiagram;
            }

            int byName = string.CompareOrdinal(left.Name, right.Name);
            if (byName != 0)
            {
                return byName;
            }

            return string.CompareOrdinal(left.Id.ToString(), right.Id.ToString());
        }

        private sealed class ElementSnapshot
        {
            public Guid Id { get; init; }

            public string Kind { get; init; } = string.Empty;

            public string Name { get; init; } = string.Empty;

            public string DiagramName { get; init; } = string.Empty;

            public Guid DiagramId { get; init; }

            public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();

            public ElementChange ToChange(ChangeKind kind, IReadOnlyList<PropertyChange> propertyChanges)
            {
                return new ElementChange
                {
                    Id = this.Id,
                    Kind = kind,
                    ElementKind = this.Kind,
                    Name = this.Name,
                    DiagramName = this.DiagramName,
                    DiagramId = this.DiagramId,
                    PropertyChanges = propertyChanges,
                };
            }
        }
    }
}
