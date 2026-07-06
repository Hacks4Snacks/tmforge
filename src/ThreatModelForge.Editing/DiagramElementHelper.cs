namespace ThreatModelForge.Editing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Helpers for reading and writing the well-known display properties of a diagram element.
    /// </summary>
    public static class DiagramElementHelper
    {
        private const string NamePropertyName = "Name";

        /// <summary>
        /// Gets the display name of an element from its <c>Name</c> property.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The name, or an empty string when the element has none.</returns>
        public static string GetName(Entity element)
        {
            if (element == null)
            {
                return string.Empty;
            }

            foreach (StringDisplayAttribute property in element.Properties.OfType<StringDisplayAttribute>())
            {
                if (IsNameProperty(property))
                {
                    return property.Value as string ?? string.Empty;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Sets the display name of an element, adding a <c>Name</c> property when one is absent.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="name">The new name.</param>
        public static void SetName(Entity element, string name)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            foreach (StringDisplayAttribute property in element.Properties.OfType<StringDisplayAttribute>())
            {
                if (IsNameProperty(property))
                {
                    property.Value = name;
                    return;
                }
            }

            element.Properties.Add(new StringDisplayAttribute { Name = NamePropertyName, DisplayName = NamePropertyName, Value = name });
        }

        /// <summary>
        /// Sets a custom property (for example, <c>Protocol</c> or <c>DataType</c>) that analysis rules
        /// read, replacing any existing value for the same key. Stored as a
        /// <see cref="CustomStringDisplayAttribute"/> with value <c>key:value</c>.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        public static void SetCustomProperty(Entity element, string key, string value)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(key));
            }

            string encoded = key + ":" + value;
            string prefix = key + ":";
            foreach (CustomStringDisplayAttribute property in element.Properties.OfType<CustomStringDisplayAttribute>())
            {
                string current = property.Value as string ?? string.Empty;
                if (current.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    property.Value = encoded;
                    return;
                }
            }

            element.Properties.Add(new CustomStringDisplayAttribute { Value = encoded });
        }

        /// <summary>
        /// Reads the custom properties (see <see cref="SetCustomProperty"/>) declared on an element.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>A map of property key to value.</returns>
        public static IReadOnlyDictionary<string, string> GetCustomProperties(Entity element)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (element == null)
            {
                return result;
            }

            foreach (CustomStringDisplayAttribute property in element.Properties.OfType<CustomStringDisplayAttribute>())
            {
                string current = property.Value as string ?? string.Empty;
                int separator = current.IndexOf(':');
                if (separator > 0)
                {
                    result[current.Substring(0, separator)] = current.Substring(separator + 1);
                }
            }

            return result;
        }

        private static bool IsNameProperty(StringDisplayAttribute property)
        {
            return string.Equals(property.Name, NamePropertyName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(property.DisplayName, NamePropertyName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
