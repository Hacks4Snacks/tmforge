namespace ThreatModelForge.Editing
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The kind of problem found when validating a custom-property assignment against the typed
    /// property schema.
    /// </summary>
    public enum PropertySchemaIssueKind
    {
        /// <summary>The property name is not defined for the target DFD primitive.</summary>
        UnknownProperty,

        /// <summary>The value is not one of the property's allowed enum/bool values.</summary>
        InvalidValue,
    }

    /// <summary>
    /// Describes a single custom-property assignment that does not match the typed schema, so a
    /// write-path caller can reject a typo (or a value that no longer exists) before it silently
    /// makes a rule fail to match.
    /// </summary>
    public sealed class PropertySchemaIssue
    {
        /// <summary>Gets the DFD primitive the assignment targets.</summary>
        public string AppliesTo { get; init; } = string.Empty;

        /// <summary>Gets the problem kind.</summary>
        public PropertySchemaIssueKind Kind { get; init; }

        /// <summary>Gets the property name from the assignment.</summary>
        public string Property { get; init; } = string.Empty;

        /// <summary>Gets the value from the assignment.</summary>
        public string Value { get; init; } = string.Empty;

        /// <summary>
        /// Gets the allowed values for an <see cref="PropertySchemaIssueKind.InvalidValue"/>, or the
        /// known property names for an <see cref="PropertySchemaIssueKind.UnknownProperty"/>.
        /// </summary>
        public IReadOnlyList<string> Allowed { get; init; } = Array.Empty<string>();
    }
}
