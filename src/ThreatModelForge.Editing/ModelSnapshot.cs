namespace ThreatModelForge.Editing
{
    using System;
    using System.Collections.Generic;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Captures a threat model as a flat list of <see cref="ElementDescriptor"/> values — a
    /// normalized view that ignores geometry and serialization order and keys every element by its
    /// stable <see cref="Entity.Guid"/>. This is the shared basis for the structural diff
    /// (<see cref="ModelDiff"/>) and for canonical text rendering (the git textconv), so element
    /// classification and attribute extraction live in exactly one place.
    /// </summary>
    public static class ModelSnapshot
    {
        /// <summary>The synthesized attribute key holding an element's display name.</summary>
        public const string NameKey = "name";

        /// <summary>The synthesized attribute key holding an element's kind.</summary>
        public const string KindKey = "kind";

        /// <summary>The synthesized attribute key holding a flow's source element id.</summary>
        public const string SourceKey = "source";

        /// <summary>The synthesized attribute key holding a flow's target element id.</summary>
        public const string TargetKey = "target";

        /// <summary>
        /// Captures the elements of every diagram in <paramref name="model"/>, in document order.
        /// </summary>
        /// <param name="model">The model to capture.</param>
        /// <returns>The element descriptors, in the order the diagrams and elements are stored.</returns>
        public static IReadOnlyList<ElementDescriptor> Capture(ThreatModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            List<ElementDescriptor> descriptors = new List<ElementDescriptor>();
            foreach (DrawingSurfaceModel surface in model.DrawingSurfaceList)
            {
                string diagramName = string.IsNullOrEmpty(surface.Header) ? "Diagram" : surface.Header!;
                Collect(surface.Borders, surface, diagramName, descriptors);
                Collect(surface.Lines, surface, diagramName, descriptors);
            }

            return descriptors;
        }

        private static void Collect(IDictionary<Guid, object> elements, DrawingSurfaceModel surface, string diagramName, List<ElementDescriptor> descriptors)
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

                descriptors.Add(new ElementDescriptor
                {
                    Id = id,
                    Kind = kind,
                    Name = attributes[NameKey],
                    DiagramName = diagramName,
                    DiagramId = surface.Guid,
                    Attributes = attributes,
                });
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
    }
}
