namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Allocates the stable identity of an analysis finding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A finding id is <c>{ruleId}:{diagram}:{target}:{occurrence}</c>. Every segment is derived from
    /// something that actually determines the finding, so the id survives the things that must not
    /// change it: evaluating the rules in a different order, enabling an unrelated rule, and rewording
    /// a message. The scheme this replaces numbered findings by their position in the message list, so
    /// one added rule renumbered every finding after it and nothing could be reconciled across runs.
    /// </para>
    /// <para>
    /// Callers supply the element keys, because the stable key differs by host. A caller working from
    /// canonical model JSON supplies the author's own element ids, which is what survives a round trip
    /// through that format; a caller working from a <c>.tm7</c> supplies the persisted guids. Passing
    /// a key that the model regenerates on load would produce ids that change on every run.
    /// </para>
    /// <para>
    /// The occurrence counter disambiguates a rule that legitimately fires more than once against the
    /// same target on the same diagram. It is the only positional segment, so clearing some of those
    /// findings can renumber the survivors; reconcile on <see cref="Scope"/> when triage has to cross
    /// that kind of edit.
    /// </para>
    /// </remarks>
    public sealed class FindingIdentity
    {
        /// <summary>
        /// The segment used when a finding has no diagram or no target, because it is about the model
        /// as a whole rather than about one element.
        /// </summary>
        public const string ModelScope = "model";

        private const char Separator = ':';

        private readonly Dictionary<string, int> occurrences =
            new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// Builds the scope of a finding: its identity without the occurrence counter. Two findings
        /// share a scope when the same rule fired against the same target on the same diagram.
        /// </summary>
        /// <param name="ruleId">The reporting rule's id.</param>
        /// <param name="diagramKey">The diagram's stable key, or <see langword="null"/> for a model-wide finding.</param>
        /// <param name="targetKey">The target element's stable key, or <see langword="null"/> when the finding has no element.</param>
        /// <returns>The scope segment triple.</returns>
        public static string Scope(string? ruleId, string? diagramKey, string? targetKey)
        {
            StringBuilder builder = new StringBuilder();
            AppendSegment(builder, ruleId);
            builder.Append(Separator);
            AppendSegment(builder, diagramKey);
            builder.Append(Separator);
            AppendSegment(builder, targetKey);
            return builder.ToString();
        }

        /// <summary>
        /// Formats a complete finding identity whose occurrence is already known, for example when
        /// reading one back from a persisted analysis artifact.
        /// </summary>
        /// <param name="ruleId">The reporting rule's id.</param>
        /// <param name="diagramKey">The diagram's stable key, or <see langword="null"/> for a model-wide finding.</param>
        /// <param name="targetKey">The target element's stable key, or <see langword="null"/> when the finding has no element.</param>
        /// <param name="occurrence">The zero-based occurrence within the scope.</param>
        /// <returns>The finding identity.</returns>
        public static string Format(string? ruleId, string? diagramKey, string? targetKey, int occurrence)
        {
            if (occurrence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(occurrence));
            }

            return Scope(ruleId, diagramKey, targetKey) +
                Separator +
                occurrence.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Allocates the next identity in a scope. Call this once per finding, in the order the
        /// findings were collected from a single evaluation.
        /// </summary>
        /// <param name="ruleId">The reporting rule's id.</param>
        /// <param name="diagramKey">The diagram's stable key, or <see langword="null"/> for a model-wide finding.</param>
        /// <param name="targetKey">The target element's stable key, or <see langword="null"/> when the finding has no element.</param>
        /// <returns>The finding identity.</returns>
        public string Next(string? ruleId, string? diagramKey, string? targetKey)
        {
            string scope = Scope(ruleId, diagramKey, targetKey);
            this.occurrences.TryGetValue(scope, out int occurrence);
            this.occurrences[scope] = occurrence + 1;
            return scope + Separator + occurrence.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Appends one segment, escaping the separator so an element id that contains a colon cannot
        /// impersonate a different finding. Ordinary guid and alias keys pass through unchanged.
        /// </summary>
        /// <param name="builder">The identity under construction.</param>
        /// <param name="value">The raw segment value.</param>
        private static void AppendSegment(StringBuilder builder, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                builder.Append(ModelScope);
                return;
            }

            foreach (char character in value!.Trim())
            {
                switch (character)
                {
                    case '%':
                        builder.Append("%25");
                        break;
                    case Separator:
                        builder.Append("%3A");
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }
        }
    }
}
