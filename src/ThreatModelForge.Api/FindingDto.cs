namespace ThreatModelForge.Api
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A single analysis finding returned to the client.
    /// </summary>
    public sealed class FindingDto
    {
        /// <summary>Gets a stable identifier for this finding within the response.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the severity (<c>info</c>, <c>warning</c>, or <c>error</c>).</summary>
        public string Severity { get; init; } = "info";

        /// <summary>Gets the originating rule identifier, if any.</summary>
        public string? RuleId { get; init; }

        /// <summary>Gets the human-readable finding text.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Gets the client element ids this finding refers to, for highlighting.</summary>
        public IReadOnlyList<string> ElementIds { get; init; } = Array.Empty<string>();
    }
}
