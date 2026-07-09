namespace ThreatModelForge.Editing
{
    using System;
    using System.Collections.Generic;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Performs a three-way, identity-keyed merge of two edited threat models against their common
    /// ancestor. Elements are matched by their stable <see cref="Entity.Guid"/>, so non-overlapping
    /// edits from both sides combine automatically: an element one side added, deleted, renamed, or
    /// re-propertied is carried into the result unless the other side changed the same thing
    /// incompatibly. Where both sides changed the same attribute differently (or one deleted what the
    /// other modified), the <c>ours</c> value is kept and a <see cref="MergeConflict"/> is recorded,
    /// so the result is always a valid model — never one carrying textual conflict markers. Geometry
    /// is not merged. The <c>ours</c> model is mutated in place and returned as the result.
    /// </summary>
    public static class ModelMerge
    {
        /// <summary>
        /// Merges <paramref name="theirs"/> into <paramref name="ours"/> relative to their common
        /// ancestor <paramref name="baseModel"/>.
        /// </summary>
        /// <param name="baseModel">The common ancestor model.</param>
        /// <param name="ours">The local model; mutated into the merged result and returned.</param>
        /// <param name="theirs">The incoming model whose changes are replayed onto <paramref name="ours"/>.</param>
        /// <returns>The merged model and any conflicts (all resolved in favor of <c>ours</c>).</returns>
        public static MergeResult Merge(ThreatModel baseModel, ThreatModel ours, ThreatModel theirs)
        {
            if (baseModel == null)
            {
                throw new ArgumentNullException(nameof(baseModel));
            }

            if (ours == null)
            {
                throw new ArgumentNullException(nameof(ours));
            }

            if (theirs == null)
            {
                throw new ArgumentNullException(nameof(theirs));
            }

            Dictionary<Guid, ElementDescriptor> baseById = ById(ModelSnapshot.Capture(baseModel));
            Dictionary<Guid, ElementDescriptor> oursById = ById(ModelSnapshot.Capture(ours));
            Dictionary<Guid, ElementDescriptor> theirsById = ById(ModelSnapshot.Capture(theirs));
            Dictionary<Guid, ElementRef> oursRefs = IndexElements(ours);
            Dictionary<Guid, ElementRef> theirsRefs = IndexElements(theirs);
            Dictionary<Guid, DrawingSurfaceModel> oursSurfaces = SurfacesById(ours);

            List<MergeConflict> conflicts = new List<MergeConflict>();

            // 1. Deletions made by theirs (present in the ancestor, absent from theirs).
            foreach (KeyValuePair<Guid, ElementDescriptor> entry in baseById)
            {
                if (theirsById.ContainsKey(entry.Key))
                {
                    continue;
                }

                if (!oursById.TryGetValue(entry.Key, out ElementDescriptor? oursOnly))
                {
                    continue; // Deleted on both sides.
                }

                if (AttributesEqual(oursOnly, entry.Value))
                {
                    if (oursRefs.TryGetValue(entry.Key, out ElementRef? reference))
                    {
                        Remove(reference);
                    }
                }
                else
                {
                    conflicts.Add(DeleteModifyConflict(oursOnly, ours: "modified", theirs: "deleted"));
                }
            }

            // 2. Additions and modifications made by theirs.
            foreach (KeyValuePair<Guid, ElementDescriptor> entry in theirsById)
            {
                Guid id = entry.Key;
                ElementDescriptor theirDescriptor = entry.Value;

                if (!baseById.TryGetValue(id, out ElementDescriptor? baseDescriptor))
                {
                    if (!oursById.TryGetValue(id, out ElementDescriptor? oursAdded))
                    {
                        if (theirsRefs.TryGetValue(id, out ElementRef? incoming))
                        {
                            Add(incoming, ours, oursSurfaces);
                        }
                    }
                    else
                    {
                        AddAddConflicts(oursAdded, theirDescriptor, conflicts);
                    }

                    continue;
                }

                if (!oursById.TryGetValue(id, out ElementDescriptor? oursExisting))
                {
                    if (!AttributesEqual(theirDescriptor, baseDescriptor))
                    {
                        conflicts.Add(DeleteModifyConflict(theirDescriptor, ours: "deleted", theirs: "modified"));
                    }

                    continue;
                }

                if (oursRefs.TryGetValue(id, out ElementRef? oursReference))
                {
                    MergeAttributes(oursReference.Entity, baseDescriptor, oursExisting, theirDescriptor, conflicts);
                }
            }

            // 3. Flows whose endpoints the merge removed.
            DetectDangling(ours, conflicts);

            return new MergeResult { Merged = ours, Conflicts = conflicts };
        }

        /// <summary>
        /// Performs a two-way, identity-keyed merge of two edited models when their common ancestor
        /// is unavailable. Elements are still matched by <see cref="Entity.Guid"/>: one present on
        /// only one side is carried into the result, and where both sides carry the same element with
        /// a differing attribute the <c>ours</c> value is kept and a <see cref="MergeConflict"/> is
        /// recorded — without an ancestor the merge cannot tell which side changed it, so every
        /// overlapping difference is surfaced for the caller to resolve. Nothing is deleted (a missing
        /// element is indistinguishable from one the other side never had). The <paramref name="ours"/>
        /// model is mutated in place and returned.
        /// </summary>
        /// <param name="ours">The local model; mutated into the merged result and returned.</param>
        /// <param name="theirs">The other model whose additions are folded into <paramref name="ours"/>.</param>
        /// <returns>The merged model and any conflicts (all resolved in favor of <c>ours</c>).</returns>
        public static MergeResult Merge(ThreatModel ours, ThreatModel theirs)
        {
            if (ours == null)
            {
                throw new ArgumentNullException(nameof(ours));
            }

            if (theirs == null)
            {
                throw new ArgumentNullException(nameof(theirs));
            }

            Dictionary<Guid, ElementDescriptor> oursById = ById(ModelSnapshot.Capture(ours));
            Dictionary<Guid, ElementDescriptor> theirsById = ById(ModelSnapshot.Capture(theirs));
            Dictionary<Guid, ElementRef> theirsRefs = IndexElements(theirs);
            Dictionary<Guid, ElementRef> oursRefs = IndexElements(ours);
            Dictionary<Guid, DrawingSurfaceModel> oursSurfaces = SurfacesById(ours);

            List<MergeConflict> conflicts = new List<MergeConflict>();
            ElementDescriptor noAncestor = new ElementDescriptor();

            foreach (KeyValuePair<Guid, ElementDescriptor> entry in theirsById)
            {
                Guid id = entry.Key;
                if (!oursById.TryGetValue(id, out ElementDescriptor? oursExisting))
                {
                    // Present only in theirs — with no ancestor this reads as an addition; fold it in.
                    if (theirsRefs.TryGetValue(id, out ElementRef? incoming))
                    {
                        Add(incoming, ours, oursSurfaces);
                    }
                }
                else if (oursRefs.TryGetValue(id, out ElementRef? oursReference))
                {
                    // Present on both sides — compare against an empty ancestor so any divergence
                    // becomes an ours/theirs conflict instead of silently taking a side.
                    MergeAttributes(oursReference.Entity, noAncestor, oursExisting, entry.Value, conflicts);
                }
            }

            DetectDangling(ours, conflicts);

            return new MergeResult { Merged = ours, Conflicts = conflicts };
        }

        private static void MergeAttributes(Entity entity, ElementDescriptor baseDescriptor, ElementDescriptor oursDescriptor, ElementDescriptor theirDescriptor, List<MergeConflict> conflicts)
        {
            HashSet<string> keys = new HashSet<string>(baseDescriptor.Attributes.Keys, StringComparer.Ordinal);
            keys.UnionWith(oursDescriptor.Attributes.Keys);
            keys.UnionWith(theirDescriptor.Attributes.Keys);

            foreach (string key in keys)
            {
                baseDescriptor.Attributes.TryGetValue(key, out string? ancestor);
                oursDescriptor.Attributes.TryGetValue(key, out string? ours);
                theirDescriptor.Attributes.TryGetValue(key, out string? theirs);

                if (string.Equals(theirs, ancestor, StringComparison.Ordinal))
                {
                    continue; // Theirs did not change this attribute.
                }

                if (string.Equals(ours, ancestor, StringComparison.Ordinal))
                {
                    if (!TryApply(entity, key, theirs))
                    {
                        conflicts.Add(PropertyConflict(oursDescriptor, key, ancestor, ours, theirs));
                    }
                }
                else if (!string.Equals(ours, theirs, StringComparison.Ordinal))
                {
                    conflicts.Add(PropertyConflict(oursDescriptor, key, ancestor, ours, theirs));
                }
            }
        }

        private static bool TryApply(Entity entity, string key, string? value)
        {
            if (value == null)
            {
                return false; // Attribute removal is not represented; surface it as a conflict.
            }

            switch (key)
            {
                case ModelSnapshot.NameKey:
                    DiagramElementHelper.SetName(entity, value);
                    return true;
                case ModelSnapshot.KindKey:
                    return false; // A kind change cannot be applied to an existing element safely.
                case ModelSnapshot.SourceKey:
                    if (entity is LineElement source && Guid.TryParse(value, out Guid sourceGuid))
                    {
                        source.SourceGuid = sourceGuid;
                        return true;
                    }

                    return false;
                case ModelSnapshot.TargetKey:
                    if (entity is LineElement target && Guid.TryParse(value, out Guid targetGuid))
                    {
                        target.TargetGuid = targetGuid;
                        return true;
                    }

                    return false;
                default:
                    DiagramElementHelper.SetCustomProperty(entity, key, value);
                    return true;
            }
        }

        private static void AddAddConflicts(ElementDescriptor ours, ElementDescriptor theirs, List<MergeConflict> conflicts)
        {
            HashSet<string> keys = new HashSet<string>(ours.Attributes.Keys, StringComparer.Ordinal);
            keys.UnionWith(theirs.Attributes.Keys);

            foreach (string key in keys)
            {
                ours.Attributes.TryGetValue(key, out string? oursValue);
                theirs.Attributes.TryGetValue(key, out string? theirsValue);
                if (!string.Equals(oursValue, theirsValue, StringComparison.Ordinal))
                {
                    conflicts.Add(new MergeConflict
                    {
                        ElementId = ours.Id,
                        ElementKind = ours.Kind,
                        Name = ours.Name,
                        DiagramName = ours.DiagramName,
                        Kind = MergeConflictKind.AddAdd,
                        Property = key,
                        Ours = oursValue,
                        Theirs = theirsValue,
                    });
                }
            }
        }

        private static void DetectDangling(ThreatModel model, List<MergeConflict> conflicts)
        {
            HashSet<Guid> present = new HashSet<Guid>();
            foreach (DrawingSurfaceModel surface in model.DrawingSurfaceList)
            {
                CollectIds(surface.Borders, present);
                CollectIds(surface.Lines, present);
            }

            foreach (DrawingSurfaceModel surface in model.DrawingSurfaceList)
            {
                string diagramName = string.IsNullOrEmpty(surface.Header) ? "Diagram" : surface.Header!;
                foreach (Connector connector in surface.Lines.Values.OfType<Connector>())
                {
                    CheckEndpoint(connector, connector.SourceGuid, ModelSnapshot.SourceKey, present, diagramName, conflicts);
                    CheckEndpoint(connector, connector.TargetGuid, ModelSnapshot.TargetKey, present, diagramName, conflicts);
                }
            }
        }

        private static void CheckEndpoint(Connector connector, Guid endpoint, string property, HashSet<Guid> present, string diagramName, List<MergeConflict> conflicts)
        {
            if (endpoint != Guid.Empty && !present.Contains(endpoint))
            {
                conflicts.Add(new MergeConflict
                {
                    ElementId = connector.Guid,
                    ElementKind = "flow",
                    Name = DiagramElementHelper.GetName(connector),
                    DiagramName = diagramName,
                    Kind = MergeConflictKind.DanglingReference,
                    Property = property,
                    Ours = endpoint.ToString(),
                });
            }
        }

        private static void CollectIds(IDictionary<Guid, object> elements, HashSet<Guid> present)
        {
            foreach (KeyValuePair<Guid, object> pair in elements.Where(pair => pair.Value is Entity))
            {
                Entity entity = (Entity)pair.Value;
                present.Add(entity.Guid != Guid.Empty ? entity.Guid : pair.Key);
            }
        }

        private static void Remove(ElementRef reference)
        {
            if (reference.IsLine)
            {
                reference.Surface.Lines.Remove(reference.Key);
            }
            else
            {
                reference.Surface.Borders.Remove(reference.Key);
            }
        }

        private static void Add(ElementRef incoming, ThreatModel ours, Dictionary<Guid, DrawingSurfaceModel> oursSurfaces)
        {
            Guid surfaceId = incoming.Surface.Guid;
            if (!oursSurfaces.TryGetValue(surfaceId, out DrawingSurfaceModel? surface))
            {
                surface = new DrawingSurfaceModel { Guid = surfaceId, Header = incoming.Surface.Header, Zoom = incoming.Surface.Zoom };
                ours.DrawingSurfaceList.Add(surface);
                oursSurfaces[surfaceId] = surface;
            }

            Guid key = incoming.Entity.Guid != Guid.Empty ? incoming.Entity.Guid : incoming.Key;
            if (incoming.IsLine)
            {
                surface.Lines[key] = incoming.Entity;
            }
            else
            {
                surface.Borders[key] = incoming.Entity;
            }
        }

        private static MergeConflict PropertyConflict(ElementDescriptor descriptor, string key, string? ancestor, string? ours, string? theirs)
        {
            return new MergeConflict
            {
                ElementId = descriptor.Id,
                ElementKind = descriptor.Kind,
                Name = descriptor.Name,
                DiagramName = descriptor.DiagramName,
                Kind = MergeConflictKind.Property,
                Property = key,
                Base = ancestor,
                Ours = ours,
                Theirs = theirs,
            };
        }

        private static MergeConflict DeleteModifyConflict(ElementDescriptor descriptor, string ours, string theirs)
        {
            return new MergeConflict
            {
                ElementId = descriptor.Id,
                ElementKind = descriptor.Kind,
                Name = descriptor.Name,
                DiagramName = descriptor.DiagramName,
                Kind = MergeConflictKind.DeleteModify,
                Property = string.Empty,
                Ours = ours,
                Theirs = theirs,
            };
        }

        private static bool AttributesEqual(ElementDescriptor left, ElementDescriptor right)
        {
            if (left.Attributes.Count != right.Attributes.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, string> pair in left.Attributes.Where(pair => !right.Attributes.TryGetValue(pair.Key, out string? other) || !string.Equals(pair.Value, other, StringComparison.Ordinal)))
            {
                return false;
            }

            return true;
        }

        private static Dictionary<Guid, ElementDescriptor> ById(IReadOnlyList<ElementDescriptor> descriptors)
        {
            Dictionary<Guid, ElementDescriptor> map = new Dictionary<Guid, ElementDescriptor>();
            foreach (ElementDescriptor descriptor in descriptors)
            {
                map[descriptor.Id] = descriptor;
            }

            return map;
        }

        private static Dictionary<Guid, DrawingSurfaceModel> SurfacesById(ThreatModel model)
        {
            Dictionary<Guid, DrawingSurfaceModel> map = new Dictionary<Guid, DrawingSurfaceModel>();
            foreach (DrawingSurfaceModel surface in model.DrawingSurfaceList)
            {
                map[surface.Guid] = surface;
            }

            return map;
        }

        private static Dictionary<Guid, ElementRef> IndexElements(ThreatModel model)
        {
            Dictionary<Guid, ElementRef> map = new Dictionary<Guid, ElementRef>();
            foreach (DrawingSurfaceModel surface in model.DrawingSurfaceList)
            {
                IndexInto(surface.Borders, surface, isLine: false, map);
                IndexInto(surface.Lines, surface, isLine: true, map);
            }

            return map;
        }

        private static void IndexInto(IDictionary<Guid, object> elements, DrawingSurfaceModel surface, bool isLine, Dictionary<Guid, ElementRef> map)
        {
            foreach (KeyValuePair<Guid, object> pair in elements.Where(pair => pair.Value is Entity))
            {
                Entity entity = (Entity)pair.Value;
                Guid id = entity.Guid != Guid.Empty ? entity.Guid : pair.Key;
                map[id] = new ElementRef { Key = pair.Key, Entity = entity, Surface = surface, IsLine = isLine };
            }
        }

        private sealed class ElementRef
        {
            public Guid Key { get; init; }

            public Entity Entity { get; init; } = null!;

            public DrawingSurfaceModel Surface { get; init; } = null!;

            public bool IsLine { get; init; }
        }
    }
}
