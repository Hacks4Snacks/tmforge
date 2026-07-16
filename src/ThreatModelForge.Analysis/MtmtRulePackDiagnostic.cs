namespace ThreatModelForge.Analysis
{
    /// <summary>A diagnostic produced while translating one MTMT threat type.</summary>
    public sealed class MtmtRulePackDiagnostic
    {
        /// <summary>Gets the source threat identifier, when the diagnostic is threat-specific.</summary>
        public string? SourceThreatId { get; init; }

        /// <summary>Gets the diagnostic message.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Gets the original source expression associated with the diagnostic.</summary>
        public string? SourceExpression { get; init; }

        /// <summary>Gets a value indicating whether the source threat was skipped.</summary>
        public bool IsError { get; init; }
    }
}
