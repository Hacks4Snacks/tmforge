namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Describes the set of configured data classification tags.
    /// </summary>
    public class DataClassificationTagSet
    {
        /// <summary>
        /// Variable name when the set is supplied in the <see cref="RuleEvaluationContext"/>.
        /// </summary>
        public const string VariableName = "DATACLASSTAGS";

        /// <summary>
        /// Identifier to look for in text or custom attributes.
        /// </summary>
        public const string TypePropertyName = "DataType";

        /// <summary>
        /// Name of the property or custom attribute text that declares
        /// a default classification for a diagram.
        /// </summary>
        private const string DefaultTypePropertyName = "DefaultDataType";

        /// <summary>
        /// The default set of data-classification tags recognized by the rule.
        /// </summary>
        private static readonly IReadOnlyList<string> DefaultTags = new string[]
        {
            "Access Control Data",
            "Customer Content",
            "EUII",
            "Support Data",
            "Feedback",
            "Account Data",
            "Public Personal Data",
            "EUPI",
            "Managed Service Data",
            "OII",
            "System Metadata",
            "Public Non-Personal Data",
        };

        private DataClassificationTagSet(IReadOnlyList<string> tags)
        {
            this.Tags = tags;
        }

        /// <summary>
        /// Gets the default set of tags.
        /// </summary>
        public static DataClassificationTagSet Default { get; } = new DataClassificationTagSet(DefaultTags);

        /// <summary>
        /// Gets the set of configured tags.
        /// </summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// Creates a new instance from the variable in the context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>
        /// A new instance of the <see cref="GeneralPurposeComponentSet"/> class.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
        public static DataClassificationTagSet FromContext(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));
            if (!context.Variables.TryGetValue(
                VariableName,
                out string? generalPurposeComponentTypes))
            {
                return Default;
            }

            var types = generalPurposeComponentTypes
                .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();
            return new DataClassificationTagSet(types);
        }

        /// <summary>
        /// Tries to get the default classification on a diagram.
        /// </summary>
        /// <param name="diagram">The diagram to check.</param>
        /// <param name="tag">On success; receives the tag found.</param>
        /// <returns>
        /// <see langword="true"/> if successful; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// This looks for a custom attribute on the diagram <c>DefaultDataType:</c>
        /// and then looks at each text annotation for text matching the set of known types.
        /// </remarks>
        public bool TryGetDefaultTag(DrawingSurfaceModel diagram, out string? tag)
        {
            _ = diagram ?? throw new ArgumentNullException(nameof(diagram));
            if (diagram.TryGetCustomPropertyValue(DefaultTypePropertyName, out tag))
            {
                return true;
            }

            tag = diagram.Borders.Values.OfType<Entity>()
                .Where(e => e.IsTextAnnotation())
                .Select(note => this.TryGetTagFromText(note.Name() ?? string.Empty, DefaultTypePropertyName, out string? t) ? t : null)
                .FirstOrDefault(t => t != null);
            return tag != null;
        }

        /// <summary>
        /// Tries to get a classification tag for the given entity.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <param name="tag">On success; receives the tag found.</param>
        /// <returns>
        /// <see langword="true"/> if successful; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryGetTag(Entity entity, out string? tag)
        {
            if (entity.TryGetCustomPropertyValue(TypePropertyName, out tag))
            {
                return true;
            }

            string name = entity.Name() ?? string.Empty;
            return this.TryGetTagFromText(name, TypePropertyName, out tag);
        }

        private static bool TryGetExplicitTagFromText(
            IReadOnlyList<string> tokens,
            string propName,
            out string? tag)
        {
            for (int i = 0; i < tokens.Count - 2; i++)
            {
                if (!string.Equals(tokens[i], propName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string sep = tokens[i + 1];
                string value = tokens[i + 2];
                if (sep.Length == 1 && (sep[0] == ':' || sep[0] == '='))
                {
                    if (!string.Equals(value, "(", StringComparison.InvariantCultureIgnoreCase))
                    {
                        tag = value;
                        return true;
                    }

                    i++;
                    List<string> result = new ();
                    while (i < tokens.Count && !string.Equals(tokens[i], ")", StringComparison.InvariantCultureIgnoreCase))
                    {
                        result.Add(tokens[i++]);
                    }

                    tag = string.Join(" ", result);
                    return true;
                }
            }

            tag = null;
            return false;
        }

        /// <summary>
        /// Tries to parse the text looking for a data classification tag.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <param name="propName">The name of the property when searching for explicit assignment.</param>
        /// <param name="tag">On success, receives the parsed classification tag.</param>
        /// <returns>
        /// <see langword="true"/> if successful; otherwise, <see langword="false"/>.
        /// </returns>
        private bool TryGetTagFromText(string text, string propName, out string? tag)
        {
            IReadOnlyList<string> tokens = text.TokenizeText().ToList().AsReadOnly();
            if (TryGetExplicitTagFromText(tokens, propName, out tag))
            {
                return true;
            }

            tag = this.Tags.FirstOrDefault(tagToMatch => (text ?? string.Empty).Contains(tagToMatch));
            return tag != null;
        }
    }
}
