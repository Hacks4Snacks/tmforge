namespace ThreatModelForge.Editing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Projects Threat Model Forge's property schema onto an exported <c>.tm7</c> so the Microsoft
    /// Threat Modeling Tool renders known element properties as first-class, typed properties rather
    /// than free-form custom attributes. It declares each enumerated/boolean schema property as a
    /// list attribute on the matching generic element type in the knowledge base, and rewrites the
    /// corresponding <see cref="CustomStringDisplayAttribute"/> values on elements into typed
    /// <see cref="ListDisplayAttribute"/> selections. Free-text properties (which the tool's knowledge
    /// base cannot express as a list), and any value that is not one of a property's known options, are
    /// left as custom attributes so their values are preserved.
    /// </summary>
    public static class SchemaBackedProperties
    {
        private const string UnsetOption = "Select";

        private static readonly IReadOnlyDictionary<string, string> KindByGenericType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GE.P"] = "process",
            ["GE.DS"] = "datastore",
            ["GE.EI"] = "external",
            ["GE.DF"] = "flow",
        };

        /// <summary>
        /// Declares the schema's enumerated and boolean properties as list attributes on the knowledge
        /// base's generic element types, and rewrites those properties on the model's elements into
        /// typed list selections.
        /// </summary>
        /// <param name="model">The model whose element properties are typed.</param>
        /// <param name="knowledgeBase">The knowledge base whose element types gain the attributes.</param>
        public static void Apply(ThreatModel model, KnowledgeBaseData knowledgeBase)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (knowledgeBase == null)
            {
                throw new ArgumentNullException(nameof(knowledgeBase));
            }

            DeclareAttributes(knowledgeBase);
            TypeElementProperties(model);
        }

        private static void DeclareAttributes(KnowledgeBaseData knowledgeBase)
        {
            foreach (ElementType elementType in knowledgeBase.GenericElements)
            {
                if (elementType.Id == null || !KindByGenericType.TryGetValue(elementType.Id, out string? kind))
                {
                    continue;
                }

                foreach (PropertyDescriptor descriptor in PropertySchemaCatalog.For(kind))
                {
                    if (!IsListProperty(descriptor))
                    {
                        continue;
                    }

                    KnowledgeBaseAttribute attribute = new KnowledgeBaseAttribute
                    {
                        Name = descriptor.Name,
                        DisplayName = descriptor.Name,
                        Mode = AttributeMode.Dynamic,
                        Type = AttributeType.List,
                        Inheritance = AttributeInheritance.Virtual,
                    };

                    attribute.AttributeValues.Add(UnsetOption);
                    foreach (string value in descriptor.Values)
                    {
                        attribute.AttributeValues.Add(value);
                    }

                    elementType.Attributes.Add(attribute);
                }
            }
        }

        private static void TypeElementProperties(ThreatModel model)
        {
            foreach (DrawingSurfaceModel surface in model.DrawingSurfaceList)
            {
                foreach (object node in surface.Borders.Values)
                {
                    TypeElement(node as Entity);
                }

                foreach (object line in surface.Lines.Values)
                {
                    TypeElement(line as Entity);
                }
            }
        }

        private static void TypeElement(Entity? element)
        {
            if (element == null || element.GenericTypeId == null ||
                !KindByGenericType.TryGetValue(element.GenericTypeId, out string? kind))
            {
                return;
            }

            IReadOnlyList<PropertyDescriptor> schema = PropertySchemaCatalog.For(kind);
            foreach (CustomStringDisplayAttribute custom in element.Properties.OfType<CustomStringDisplayAttribute>().ToList())
            {
                string encoded = custom.Value as string ?? string.Empty;
                int separator = encoded.IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                string key = encoded.Substring(0, separator);
                string value = encoded.Substring(separator + 1);
                PropertyDescriptor? descriptor = schema.FirstOrDefault(
                    d => string.Equals(d.Name, key, StringComparison.OrdinalIgnoreCase));
                if (descriptor == null || !IsListProperty(descriptor))
                {
                    continue;
                }

                List<string> options = new List<string> { UnsetOption };
                options.AddRange(descriptor.Values);
                int index = options.FindIndex(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase));
                if (index <= 0)
                {
                    // The value is not one of the schema's options (or is the unset sentinel); leave it
                    // as a custom attribute so it is preserved rather than silently dropped.
                    continue;
                }

                element.Properties.Remove(custom);
                element.Properties.Add(new ListDisplayAttribute
                {
                    Name = descriptor.Name,
                    DisplayName = descriptor.Name,
                    Value = options.ToArray(),
                    SelectedIndex = index,
                });
            }
        }

        private static bool IsListProperty(PropertyDescriptor descriptor)
        {
            return string.Equals(descriptor.Kind, "enum", StringComparison.OrdinalIgnoreCase)
                || string.Equals(descriptor.Kind, "bool", StringComparison.OrdinalIgnoreCase);
        }
    }
}
