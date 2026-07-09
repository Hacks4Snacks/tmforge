namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The result of an imperative authoring operation on the <see cref="AuthoringService"/> facade:
    /// the edited model and resolved identifiers on success, or a blocking error. Non-blocking
    /// warnings (for example, forced property overrides) are reported regardless.
    /// </summary>
    public sealed class AuthoringResultDto
    {
        /// <summary>Gets a value indicating whether the operation succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>Gets the blocking error message when <see cref="Success"/> is <see langword="false"/>.</summary>
        public string? Error { get; init; }

        /// <summary>Gets the non-blocking warnings (for example, forced property overrides).</summary>
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        /// <summary>Gets the edited model on success; otherwise <see langword="null"/>.</summary>
        public TmForgeModelDto? Model { get; init; }

        /// <summary>Gets the identifier of the affected element or flow (for add, connect, set, and rename).</summary>
        public string? Id { get; init; }

        /// <summary>Gets the resolved source element identifier (for connect).</summary>
        public string? Source { get; init; }

        /// <summary>Gets the resolved target element identifier (for connect).</summary>
        public string? Target { get; init; }

        /// <summary>Gets the identifiers removed (the element and its flows, for remove).</summary>
        public IReadOnlyList<string>? Removed { get; init; }
    }
}
