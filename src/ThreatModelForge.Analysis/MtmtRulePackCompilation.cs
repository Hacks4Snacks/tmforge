namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;

    /// <summary>The deterministic rule pack and translation summary produced from one MTMT template.</summary>
    public sealed class MtmtRulePackCompilation
    {
        /// <summary>Initializes a new instance of the <see cref="MtmtRulePackCompilation"/> class.</summary>
        /// <param name="content">The compiled version 2 rule-pack JSON.</param>
        /// <param name="packId">The effective pack identifier.</param>
        /// <param name="packName">The pack display name.</param>
        /// <param name="sourceThreatCount">The number of source threat types.</param>
        /// <param name="emittedRuleCount">The number of emitted rules.</param>
        /// <param name="categoryDistribution">Emitted rule counts keyed by source category id.</param>
        /// <param name="diagnostics">Translation diagnostics.</param>
        /// <param name="warningCount">The total number of warnings, including omitted diagnostics.</param>
        /// <param name="errorCount">The total number of skipped threats, including omitted diagnostics.</param>
        internal MtmtRulePackCompilation(
            byte[] content,
            string packId,
            string packName,
            int sourceThreatCount,
            int emittedRuleCount,
            IReadOnlyDictionary<string, int> categoryDistribution,
            IReadOnlyList<MtmtRulePackDiagnostic> diagnostics,
            int warningCount,
            int errorCount)
        {
            this.Content = content ?? Array.Empty<byte>();
            this.PackId = packId;
            this.PackName = packName;
            this.SourceThreatCount = sourceThreatCount;
            this.EmittedRuleCount = emittedRuleCount;
            this.CategoryDistribution = categoryDistribution;
            this.Diagnostics = diagnostics;
            this.WarningCount = warningCount;
            this.ErrorCount = errorCount;
        }

        /// <summary>Gets the compiled version 2 rule-pack JSON.</summary>
        public byte[] Content { get; }

        /// <summary>Gets the effective pack identifier.</summary>
        public string PackId { get; }

        /// <summary>Gets the pack display name.</summary>
        public string PackName { get; }

        /// <summary>Gets the number of source threat types.</summary>
        public int SourceThreatCount { get; }

        /// <summary>Gets the number of emitted rules.</summary>
        public int EmittedRuleCount { get; }

        /// <summary>Gets the number of skipped source threat types.</summary>
        public int SkippedThreatCount => this.SourceThreatCount - this.EmittedRuleCount;

        /// <summary>Gets emitted rule counts keyed by source category id.</summary>
        public IReadOnlyDictionary<string, int> CategoryDistribution { get; }

        /// <summary>Gets translation diagnostics.</summary>
        public IReadOnlyList<MtmtRulePackDiagnostic> Diagnostics { get; }

        /// <summary>Gets the number of non-fatal diagnostics.</summary>
        public int WarningCount { get; }

        /// <summary>Gets the number of source threats that could not be represented.</summary>
        public int ErrorCount { get; }
    }
}
