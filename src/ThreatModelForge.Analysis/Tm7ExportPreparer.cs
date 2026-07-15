namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
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
            using RuleSet ruleSet = AnalysisRuleSources.Create();
            Prepare(model, ruleSet);
        }

        /// <summary>
        /// Ensures the model carries a knowledge base built from the supplied effective rule set and
        /// has its schema-backed properties typed, unless it already carries a foreign knowledge base.
        /// </summary>
        /// <param name="model">The model to prepare; it is mutated in place.</param>
        /// <param name="ruleSet">The effective rules whose categories and threat types are embedded.</param>
        public static void Prepare(ThreatModel model, RuleSet ruleSet)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (ruleSet == null)
            {
                throw new ArgumentNullException(nameof(ruleSet));
            }

            // Shift each surface so no element sits below the tool's minimum drawing coordinate. This is
            // independent of the knowledge base, so it runs before the foreign-knowledge-base short
            // circuit below.
            NormalizeCoordinates(model);

            if (model.KnowledgeBase != null && !KnowledgeBaseCatalog.IsDefault(model.KnowledgeBase))
            {
                MergeThreatCatalog(model.KnowledgeBase, KnowledgeBaseCatalog.CreateDefault(ruleSet), true);
                return;
            }

            KnowledgeBaseData? existingKnowledgeBase = model.KnowledgeBase;
            KnowledgeBaseData knowledgeBase = KnowledgeBaseCatalog.CreateDefault(ruleSet);
            if (existingKnowledgeBase != null)
            {
                MergeThreatCatalog(knowledgeBase, existingKnowledgeBase, false);
            }

            SchemaBackedProperties.Apply(model, knowledgeBase);
            StencilSubtypeProjection.Apply(model, knowledgeBase);
            model.KnowledgeBase = knowledgeBase;
        }

        private static void MergeThreatCatalog(KnowledgeBaseData target, KnowledgeBaseData source, bool rejectConflicts)
        {
            ThreatMetaDatum? nativePriority = target.ThreatMetaData?.PropertiesMetaData.FirstOrDefault(IsPriorityMetadata);
            Dictionary<string, string> categoryIds = target.ThreatCategories
                .Where(category => !string.IsNullOrEmpty(category.Id))
                .ToDictionary(category => category.Id!, category => category.Id!, StringComparer.OrdinalIgnoreCase);
            foreach (ThreatCategory category in source.ThreatCategories)
            {
                ThreatCategory? existing = target.ThreatCategories.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, category.Id, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    target.ThreatCategories.Add(category);
                    if (!string.IsNullOrEmpty(category.Id))
                    {
                        categoryIds[category.Id!] = category.Id!;
                    }

                    continue;
                }

                if (rejectConflicts && !string.Equals(existing.Name, category.Name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Foreign knowledge base category '{category.Id}' conflicts with the effective rule set.");
                }

                if (!string.IsNullOrEmpty(category.Id) && !string.IsNullOrEmpty(existing.Id))
                {
                    categoryIds[category.Id!] = existing.Id!;
                }
            }

            foreach (ThreatType threatType in source.ThreatTypes)
            {
                string? categoryId = threatType.Category != null && categoryIds.TryGetValue(threatType.Category, out string? retainedId)
                    ? retainedId
                    : threatType.Category;
                ThreatType? existing = target.ThreatTypes.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, threatType.Id, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    threatType.Category = categoryId;
                    NormalizePriorityMetadata(threatType.PropertiesMetaData, nativePriority);
                    target.ThreatTypes.Add(threatType);
                    continue;
                }

                if (rejectConflicts &&
                    (!string.Equals(existing.Category, categoryId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(existing.ShortTitle, threatType.ShortTitle, StringComparison.Ordinal) ||
                    !string.Equals(existing.Description, threatType.Description, StringComparison.Ordinal) ||
                    !string.Equals(existing.RelatedCategory, threatType.RelatedCategory, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(existing.GenerationFilters.Include, threatType.GenerationFilters.Include, StringComparison.Ordinal) ||
                    !string.Equals(existing.GenerationFilters.Exclude, threatType.GenerationFilters.Exclude, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        $"Foreign knowledge base threat type '{threatType.Id}' conflicts with the effective rule set.");
                }

                NormalizePriorityMetadata(existing.PropertiesMetaData, nativePriority);
                MergeThreatMetadata(
                    existing.PropertiesMetaData,
                    threatType.PropertiesMetaData,
                    rejectConflicts,
                    $"threat type '{threatType.Id}'",
                    nativePriority);
            }

            if (source.ThreatMetaData == null)
            {
                return;
            }

            if (target.ThreatMetaData == null)
            {
                target.ThreatMetaData = source.ThreatMetaData;
                return;
            }

            target.ThreatMetaData.IsPriorityUsed |= source.ThreatMetaData.IsPriorityUsed;
            MergeThreatMetadata(
                target.ThreatMetaData.PropertiesMetaData,
                source.ThreatMetaData.PropertiesMetaData,
                rejectConflicts,
                "global threat metadata",
                nativePriority);
        }

        private static void MergeThreatMetadata(
            List<ThreatMetaDatum> target,
            IEnumerable<ThreatMetaDatum> source,
            bool rejectConflicts,
            string owner,
            ThreatMetaDatum? nativePriority)
        {
            foreach (ThreatMetaDatum sourceDatum in source)
            {
                ThreatMetaDatum datum = NormalizePriorityMetadata(sourceDatum, nativePriority);
                List<ThreatMetaDatum> matches = string.IsNullOrEmpty(datum.Name)
                    ? new List<ThreatMetaDatum>()
                    : target.Where(existing =>
                        string.Equals(existing.Name, datum.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 0 && !string.IsNullOrEmpty(datum.Id))
                {
                    matches = target.Where(existing =>
                        string.Equals(existing.Id, datum.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (matches.Count == 0)
                {
                    target.Add(datum);
                    continue;
                }

                if (rejectConflicts && (matches.Count != 1 || !ThreatMetadataMatches(matches[0], datum)))
                {
                    throw new InvalidOperationException(
                        $"Foreign knowledge base {owner} conflicts with metadata '{datum.Id ?? datum.Name}'.");
                }
            }
        }

        private static void NormalizePriorityMetadata(
            List<ThreatMetaDatum> metadata,
            ThreatMetaDatum? nativePriority)
        {
            if (nativePriority == null)
            {
                return;
            }

            for (int index = 0; index < metadata.Count; index++)
            {
                metadata[index] = NormalizePriorityMetadata(metadata[index], nativePriority);
            }
        }

        private static ThreatMetaDatum NormalizePriorityMetadata(
            ThreatMetaDatum datum,
            ThreatMetaDatum? nativePriority)
        {
            if (nativePriority == null || !IsPriorityMetadata(datum))
            {
                return datum;
            }

            ThreatMetaDatum normalized = new ThreatMetaDatum
            {
                Name = nativePriority.Name,
                Label = nativePriority.Label,
                HideFromUI = nativePriority.HideFromUI,
                Description = nativePriority.Description,
                Id = nativePriority.Id,
                AttributeType = nativePriority.AttributeType,
            };
            normalized.Values.AddRange(datum.Values);
            return normalized;
        }

        private static bool IsPriorityMetadata(ThreatMetaDatum datum)
            => string.Equals(datum.Name, "Priority", StringComparison.OrdinalIgnoreCase);

        private static bool ThreatMetadataMatches(ThreatMetaDatum left, ThreatMetaDatum right)
        {
            return string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(left.Label, right.Label, StringComparison.Ordinal) &&
                string.Equals(left.Description, right.Description, StringComparison.Ordinal) &&
                left.HideFromUI == right.HideFromUI &&
                left.AttributeType == right.AttributeType &&
                left.Values.SequenceEqual(right.Values, StringComparer.Ordinal);
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
