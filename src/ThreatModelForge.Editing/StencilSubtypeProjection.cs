namespace ThreatModelForge.Editing
{
    using System;
    using System.Collections.Generic;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Retypes each drawn element that was placed from a Threat Model Forge stencil to the matching
    /// standard element type in the exported knowledge base, so the Microsoft Threat Modeling Tool
    /// recognizes it as that stencil (for example, "Azure SQL Database") rather than a bare generic
    /// type. An element's stencil is read from its <c>StencilType</c> custom property; it is retyped
    /// only when the knowledge base actually declares a standard element with that identifier, so a
    /// retyped element always resolves in the tool. The element's generic type is left unchanged, so
    /// analysis continues to classify it by its primitive.
    /// </summary>
    public static class StencilSubtypeProjection
    {
        private const string StencilTypePropertyName = "StencilType";

        /// <summary>
        /// Retypes stencil-placed elements to their standard element subtype declared in
        /// <paramref name="knowledgeBase"/>.
        /// </summary>
        /// <param name="model">The model whose elements are retyped; it is mutated in place.</param>
        /// <param name="knowledgeBase">The knowledge base whose standard elements are the valid targets.</param>
        public static void Apply(ThreatModel model, KnowledgeBaseData knowledgeBase)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (knowledgeBase == null)
            {
                throw new ArgumentNullException(nameof(knowledgeBase));
            }

            Dictionary<string, string> subtypeIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ElementType standard in knowledgeBase.StandardElements)
            {
                if (!string.IsNullOrEmpty(standard.Id))
                {
                    subtypeIds[standard.Id!] = standard.Id!;
                }
            }

            if (subtypeIds.Count == 0)
            {
                return;
            }

            foreach (DrawingSurfaceModel surface in model.DrawingSurfaceList)
            {
                foreach (object node in surface.Borders.Values)
                {
                    Retype(node as Entity, subtypeIds);
                }

                foreach (object line in surface.Lines.Values)
                {
                    Retype(line as Entity, subtypeIds);
                }
            }
        }

        private static void Retype(Entity? element, IReadOnlyDictionary<string, string> subtypeIds)
        {
            if (element == null)
            {
                return;
            }

            IReadOnlyDictionary<string, string> properties = DiagramElementHelper.GetCustomProperties(element);
            if (properties.TryGetValue(StencilTypePropertyName, out string? stencilType) &&
                stencilType != null &&
                subtypeIds.TryGetValue(stencilType, out string? canonical))
            {
                element.TypeId = canonical;
            }
        }
    }
}
