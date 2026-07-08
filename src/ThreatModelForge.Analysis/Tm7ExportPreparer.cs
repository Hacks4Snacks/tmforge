namespace ThreatModelForge.Analysis
{
    using System;
    using ThreatModelForge.Editing;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

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

            if (model.KnowledgeBase != null && !KnowledgeBaseCatalog.IsDefault(model.KnowledgeBase))
            {
                return;
            }

            KnowledgeBaseData knowledgeBase = KnowledgeBaseCatalog.CreateDefault();
            SchemaBackedProperties.Apply(model, knowledgeBase);
            StencilSubtypeProjection.Apply(model, knowledgeBase);
            model.KnowledgeBase = knowledgeBase;
        }
    }
}
