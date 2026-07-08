namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The outcome of a threat-generation pass: every threat projected from the model's validation
    /// findings.
    /// </summary>
    public sealed class GenerationResult
    {
        /// <summary>Initializes a new instance of the <see cref="GenerationResult"/> class.</summary>
        /// <param name="threats">The generated threats.</param>
        public GenerationResult(IReadOnlyList<GeneratedThreat> threats)
        {
            this.Threats = threats ?? Array.Empty<GeneratedThreat>();
        }

        /// <summary>Gets every generated threat.</summary>
        public IReadOnlyList<GeneratedThreat> Threats { get; }

        /// <summary>Gets the number of generated threats.</summary>
        public int Count => this.Threats.Count;
    }
}
