namespace ThreatModelForge.Analysis
{
    using System;
    using System.Linq;
    using ThreatModelForge.Editing;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Prepares an in-memory model for export to the Microsoft Threat Modeling Tool's <c>.tm7</c>
    /// format so any write path (the CLI authoring verbs, <c>convert</c>, and the engine's Studio/API
    /// export) produces a file that opens in the tool. It embeds Threat Model Forge's own knowledge
    /// base and projects the element property schema onto the model so known properties render as
    /// first-class, typed tool properties rather than free-form custom attributes.
    /// </summary>
    /// <remarks>
    /// A model that already carries a foreign knowledge base (for example, one loaded from a file
    /// authored in the tool, or supplied through <c>convert --knowledge-base</c>) is left untouched,
    /// because its attributes follow a different naming scheme. Only an absent or Threat Model
    /// Forge-authored knowledge base is rebuilt, which keeps the operation idempotent under iterative
    /// authoring: re-preparing an already-prepared model rebuilds the same default knowledge base and
    /// leaves the already-typed properties in place while typing any newly added ones.
    /// </remarks>
    public static class Tm7ExportPreparer
    {
        /// <summary>
        /// The smallest drawing coordinate the Microsoft Threat Modeling Tool accepts. The tool treats
        /// any element positioned below this as corrupted and silently "corrects" it on open, so the
        /// export normalizes coordinates to sit at or beyond it.
        /// </summary>
        private const int MinimumCoordinate = 10;

        /// <summary>
        /// Ensures the model carries the default knowledge base and has its schema-backed properties
        /// typed, unless it already carries a foreign knowledge base.
        /// </summary>
        /// <param name="model">The model to prepare; it is mutated in place.</param>
        public static void Prepare(ThreatModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            // Shift each surface so no element sits below the tool's minimum drawing coordinate. This is
            // independent of the knowledge base, so it runs before the foreign-knowledge-base short
            // circuit below.
            NormalizeCoordinates(model);

            if (model.KnowledgeBase != null && !KnowledgeBaseCatalog.IsDefault(model.KnowledgeBase))
            {
                return;
            }

            KnowledgeBaseData knowledgeBase = KnowledgeBaseCatalog.CreateDefault();
            SchemaBackedProperties.Apply(model, knowledgeBase);
            StencilSubtypeProjection.Apply(model, knowledgeBase);
            model.KnowledgeBase = knowledgeBase;
        }

        /// <summary>
        /// Translates each drawing surface as a whole so its lowest element and connector coordinates
        /// sit at or beyond <see cref="MinimumCoordinate"/>. Shifting the surface rather than clamping
        /// each element individually preserves the relative layout and keeps connectors attached to
        /// their endpoints, which a per-element clamp (as the tool itself performs) would not.
        /// </summary>
        /// <param name="model">The model to normalize; it is mutated in place.</param>
        private static void NormalizeCoordinates(ThreatModel model)
        {
            foreach (DrawingSurfaceModel surface in model.DrawingSurfaceList)
            {
                int minX = int.MaxValue;
                int minY = int.MaxValue;

                foreach (DrawingElement element in surface.Borders.Values.OfType<DrawingElement>())
                {
                    minX = Math.Min(minX, element.Left);
                    minY = Math.Min(minY, element.Top);
                }

                foreach (LineElement line in surface.Lines.Values.OfType<LineElement>())
                {
                    minX = Math.Min(minX, Math.Min(line.SourceX, Math.Min(line.TargetX, line.HandleX)));
                    minY = Math.Min(minY, Math.Min(line.SourceY, Math.Min(line.TargetY, line.HandleY)));
                }

                if (minX == int.MaxValue)
                {
                    continue;
                }

                int deltaX = minX < MinimumCoordinate ? MinimumCoordinate - minX : 0;
                int deltaY = minY < MinimumCoordinate ? MinimumCoordinate - minY : 0;
                if (deltaX == 0 && deltaY == 0)
                {
                    continue;
                }

                foreach (DrawingElement element in surface.Borders.Values.OfType<DrawingElement>())
                {
                    element.Left += deltaX;
                    element.Top += deltaY;
                }

                foreach (LineElement line in surface.Lines.Values.OfType<LineElement>())
                {
                    // Shift the handle first: an unset handle reports the midpoint of its endpoints, so
                    // reading it before the endpoints move yields the original midpoint to offset.
                    line.HandleX += deltaX;
                    line.HandleY += deltaY;
                    line.SourceX += deltaX;
                    line.SourceY += deltaY;
                    line.TargetX += deltaX;
                    line.TargetY += deltaY;
                }
            }
        }
    }
}
