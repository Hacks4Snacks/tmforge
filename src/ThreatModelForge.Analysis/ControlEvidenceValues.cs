namespace ThreatModelForge.Analysis
{
    using System;

    /// <summary>
    /// Reads a control-like property value as <see cref="ControlEvidence"/>. Every rule that decides
    /// whether a control is in place should classify through here rather than testing the raw string,
    /// because the tempting shorthand — "any value other than <c>No</c> means encrypted" — silently
    /// turns an unevidenced control into a satisfied one, which suppresses a real finding.
    /// </summary>
    public static class ControlEvidenceValues
    {
        /// <summary>
        /// The canonical value an author uses to say "this control has not been evidenced". It is
        /// deliberately a real, schema-valid value so an author can record honest uncertainty without
        /// claiming the control is absent and without needing <c>--force</c>.
        /// </summary>
        public const string Unknown = "Unknown";

        /// <summary>
        /// Returns whether the model records nothing about a control: the property is missing, blank,
        /// or explicitly <see cref="Unknown"/>.
        /// </summary>
        /// <param name="value">The raw property value, or <see langword="null"/> when absent.</param>
        /// <returns><see langword="true"/> when there is no evidence either way.</returns>
        public static bool IsUnevidenced(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ||
                string.Equals(value!.Trim(), Unknown, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Classifies a control whose absence is spelled by known negative values (for example
        /// <c>No</c> or <c>None</c>); any other evidenced value counts as the control being present.
        /// Use this for open vocabularies where new positive values may be added over time.
        /// </summary>
        /// <param name="value">The raw property value, or <see langword="null"/> when absent.</param>
        /// <param name="absentValues">The values that explicitly state the control is not in place.</param>
        /// <returns>The evidence the model provides.</returns>
        public static ControlEvidence ClassifyByAbsentValues(string? value, params string[] absentValues)
        {
            if (IsUnevidenced(value))
            {
                return ControlEvidence.Unevidenced;
            }

            string trimmed = value!.Trim();
            foreach (string absent in absentValues ?? Array.Empty<string>())
            {
                if (string.Equals(trimmed, absent, StringComparison.OrdinalIgnoreCase))
                {
                    return ControlEvidence.Absent;
                }
            }

            return ControlEvidence.Present;
        }

        /// <summary>
        /// Classifies a control whose presence is spelled by an explicit allow list (for example
        /// <c>RBAC</c> or <c>ACL</c>); any other evidenced value counts as the control being absent.
        /// Use this for closed vocabularies where only named values actually protect anything.
        /// </summary>
        /// <param name="value">The raw property value, or <see langword="null"/> when absent.</param>
        /// <param name="presentValues">The values that state the control is in place.</param>
        /// <returns>The evidence the model provides.</returns>
        public static ControlEvidence ClassifyByPresentValues(string? value, params string[] presentValues)
        {
            if (IsUnevidenced(value))
            {
                return ControlEvidence.Unevidenced;
            }

            string trimmed = value!.Trim();
            foreach (string present in presentValues ?? Array.Empty<string>())
            {
                if (string.Equals(trimmed, present, StringComparison.OrdinalIgnoreCase))
                {
                    return ControlEvidence.Present;
                }
            }

            return ControlEvidence.Absent;
        }
    }
}
