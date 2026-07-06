namespace ThreatModelForge.Editing
{
    using System.Collections.Generic;

    /// <summary>
    /// Typed definition of a single element custom property (for example, a data store's
    /// <c>Encrypted</c> setting). The schema lets authoring surfaces render the right control
    /// (dropdown, checkbox, or free text) and emit canonical values, so the analysis rules that
    /// inspect these properties match reliably instead of guessing at free-form text.
    /// </summary>
    public sealed class PropertyDescriptor
    {
        /// <summary>Gets the DFD primitive this property applies to (<c>process</c>, <c>datastore</c>, <c>external</c>, or <c>flow</c>).</summary>
        public string AppliesTo { get; init; } = string.Empty;

        /// <summary>Gets the property name, i.e. the custom-attribute key (for example, <c>AuthenticationScheme</c>).</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the value kind that drives the editor control: <c>enum</c>, <c>bool</c>, or <c>string</c>.</summary>
        public string Kind { get; init; } = string.Empty;

        /// <summary>Gets the allowed values for an <c>enum</c> or <c>bool</c> property; empty for a free-form <c>string</c>.</summary>
        public IReadOnlyList<string> Values { get; init; } = new List<string>();

        /// <summary>Gets the default value applied when the property is first added to an element.</summary>
        public string Default { get; init; } = string.Empty;
    }
}
