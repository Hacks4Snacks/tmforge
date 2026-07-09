namespace ThreatModelForge.Editing
{
    using System;
    using System.Collections.Generic;
    using ThreatModelForge.Model;

    /// <summary>
    /// Computes a structural difference between two threat models, matched by the stable
    /// <see cref="ThreatModelForge.Model.Abstracts.Entity.Guid"/> that every element carries. Because elements are
    /// matched by identity rather than by serialized text, a model that was only re-laid-out or
    /// re-serialized produces no differences, and a renamed or re-typed element is reported as a
    /// single modification rather than a delete plus an add. Geometry (position and size) is
    /// intentionally ignored so the diff stays focused on semantically meaningful content: an
    /// element's name, kind, custom properties, and — for a flow — its endpoints.
    /// </summary>
    public static class ModelDiff
    {
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

            Dictionary<Guid, ElementDescriptor> before = Index(ModelSnapshot.Capture(baseModel));
            Dictionary<Guid, ElementDescriptor> after = Index(ModelSnapshot.Capture(revisedModel));

            List<ElementChange> added = new List<ElementChange>();
            List<ElementChange> removed = new List<ElementChange>();
            List<ElementChange> modified = new List<ElementChange>();

            foreach (KeyValuePair<Guid, ElementDescriptor> entry in after.Where(entry => !before.ContainsKey(entry.Key)))
            {
                added.Add(ToChange(entry.Value, ChangeKind.Added, Array.Empty<PropertyChange>()));
            }

            foreach (KeyValuePair<Guid, ElementDescriptor> entry in before)
            {
                if (!after.TryGetValue(entry.Key, out ElementDescriptor? revised))
                {
                    removed.Add(ToChange(entry.Value, ChangeKind.Removed, Array.Empty<PropertyChange>()));
                    continue;
                }

                List<PropertyChange> changes = DiffAttributes(entry.Value, revised);
                if (changes.Count > 0)
                {
                    modified.Add(ToChange(revised, ChangeKind.Modified, changes));
                }
            }

            added.Sort(CompareChanges);
            removed.Sort(CompareChanges);
            modified.Sort(CompareChanges);

            return new ModelDifference { Added = added, Removed = removed, Modified = modified };
        }

        private static Dictionary<Guid, ElementDescriptor> Index(IReadOnlyList<ElementDescriptor> descriptors)
        {
            Dictionary<Guid, ElementDescriptor> map = new Dictionary<Guid, ElementDescriptor>();
            foreach (ElementDescriptor descriptor in descriptors)
            {
                map[descriptor.Id] = descriptor;
            }

            return map;
        }

        private static ElementChange ToChange(ElementDescriptor descriptor, ChangeKind kind, IReadOnlyList<PropertyChange> propertyChanges)
        {
            return new ElementChange
            {
                Id = descriptor.Id,
                Kind = kind,
                ElementKind = descriptor.Kind,
                Name = descriptor.Name,
                DiagramName = descriptor.DiagramName,
                DiagramId = descriptor.DiagramId,
                PropertyChanges = propertyChanges,
            };
        }

        private static List<PropertyChange> DiffAttributes(ElementDescriptor before, ElementDescriptor after)
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
                case ModelSnapshot.NameKey:
                    return 0;
                case ModelSnapshot.KindKey:
                    return 1;
                case ModelSnapshot.SourceKey:
                    return 2;
                case ModelSnapshot.TargetKey:
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
    }
}
