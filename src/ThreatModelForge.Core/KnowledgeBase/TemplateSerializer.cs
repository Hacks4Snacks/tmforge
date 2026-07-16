namespace ThreatModelForge.KnowledgeBase
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;
    using ThreatModelForge.Model;

    /// <summary>
    /// Reads and writes knowledge-base (<c>.tb7</c>) documents. Unlike <c>.tm7</c>, the <c>.tb7</c>
    /// XML omits the DataContract xmlns wrappers, so a hand-written LINQ-to-XML mapping is used rather
    /// than <see cref="System.Runtime.Serialization.DataContractSerializer"/>. The element and
    /// attribute names below follow the observable on-disk format.
    /// </summary>
    internal static class TemplateSerializer
    {
        /// <summary>
        /// Reads a knowledge base from the given stream. The stream is left open.
        /// </summary>
        /// <param name="stream">The source stream.</param>
        /// <returns>The parsed <see cref="KnowledgeBaseData"/>.</returns>
        public static KnowledgeBaseData ReadObject(Stream stream)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            };

            using TextReader textReader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);
            using XmlReader xmlReader = XmlReader.Create(textReader, settings);
            XDocument document = XDocument.Load(xmlReader);

            XElement root = document.Root ?? throw new InvalidOperationException("The knowledge base document is empty.");
            if (root.Name.Namespace != XNamespace.None ||
                !string.Equals(root.Name.LocalName, Names.KnowledgeBase, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Expected an unqualified <{Names.KnowledgeBase}> root element.");
            }

            KnowledgeBaseData result = new KnowledgeBaseData();

            XElement? manifest = root.Element(Names.Manifest);
            if (manifest is not null)
            {
                result.Manifest = ReadManifest(manifest);
            }

            XElement? threatMetaData = root.Element(Names.ThreatMetaData);
            if (threatMetaData is not null)
            {
                result.ThreatMetaData = ReadThreatMetaData(threatMetaData);
            }

            ReadElementTypes(result.GenericElements, root.Element(Names.GenericElements));
            ReadElementTypes(result.StandardElements, root.Element(Names.StandardElements));
            ReadThreatCategories(result.ThreatCategories, root.Element(Names.ThreatCategories));
            ReadThreatTypes(result.ThreatTypes, root.Element(Names.ThreatTypes));

            return result;
        }

        /// <summary>
        /// Writes a knowledge base to the given stream. The stream is left open.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="graph">The knowledge base to write.</param>
        public static void WriteObject(Stream stream, KnowledgeBaseData graph)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            XElement root = new XElement(
                Names.KnowledgeBase,
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
                WriteManifest(graph.Manifest ?? new Manifest()),
                WriteThreatMetaData(graph.ThreatMetaData ?? new ThreatMetaData()),
                WriteElementTypes(Names.GenericElements, graph.GenericElements),
                WriteElementTypes(Names.StandardElements, graph.StandardElements),
                WriteThreatCategories(graph.ThreatCategories),
                WriteThreatTypes(graph.ThreatTypes));

            XDocument document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                IndentChars = "  ",
                CloseOutput = false,
            };

            using XmlWriter xmlWriter = XmlWriter.Create(stream, settings);
            document.Save(xmlWriter);
        }

        private static Manifest ReadManifest(XElement source)
        {
            Manifest manifest = new Manifest
            {
                Author = source.Attribute(Names.AuthorAttribute)?.Value,
                Name = source.Attribute(Names.NameAttribute)?.Value,
                Version = source.Attribute(Names.VersionAttribute)?.Value,
            };

            string? id = source.Attribute(Names.IdAttribute)?.Value;
            if (!string.IsNullOrWhiteSpace(id) && Guid.TryParse(id, out Guid parsed))
            {
                manifest.Id = parsed;
            }

            return manifest;
        }

        private static ThreatMetaData ReadThreatMetaData(XElement source)
        {
            ThreatMetaData metaData = new ThreatMetaData
            {
                IsPriorityUsed = ReadBool(source, Names.IsPriorityUsed),
                IsStatusUsed = ReadBool(source, Names.IsStatusUsed),
            };

            ReadThreatMetaData(metaData.PropertiesMetaData, source.Element(Names.PropertiesMetaData));
            return metaData;
        }

        private static void ReadThreatMetaData(List<ThreatMetaDatum> target, XElement? source)
        {
            if (source is null)
            {
                return;
            }

            foreach (XElement datum in source.Elements(Names.ThreatMetaDatum))
            {
                target.Add(ReadThreatMetaDatum(datum));
            }
        }

        private static ThreatMetaDatum ReadThreatMetaDatum(XElement source)
        {
            ThreatMetaDatum datum = new ThreatMetaDatum
            {
                Id = Text(source, Names.Id),
                Label = Text(source, Names.Label),
                Description = Text(source, Names.Description),
                HideFromUI = ReadBool(source, Names.HideFromUI),
                AttributeType = ReadInt(source, Names.AttributeType),
                Name = Text(source, Names.Name),
            };

            XElement? values = source.Element(Names.Values);
            if (values is not null)
            {
                foreach (XElement value in values.Elements(Names.Value))
                {
                    datum.Values.Add(value.Value);
                }
            }

            return datum;
        }

        private static void ReadElementTypes(List<ElementType> target, XElement? source)
        {
            if (source is null)
            {
                return;
            }

            foreach (XElement element in source.Elements(Names.ElementType))
            {
                target.Add(ReadElementType(element));
            }
        }

        private static ElementType ReadElementType(XElement source)
        {
            ElementType element = new ElementType
            {
                Description = (Text(source, Names.Description) ?? string.Empty).Trim(),
                Name = Text(source, Names.Name),
                Hidden = ReadBool(source, Names.Hidden),
                Behavior = Text(source, Names.Behavior),
                Shape = Text(source, Names.Shape),
                Id = Text(source, Names.ElementId),
                ImageLocation = Text(source, Names.ImageLocation),
                ImageSource = Text(source, Names.Image),
                ParentId = Text(source, Names.ParentElement),
                Representation = ReadEnum(source, Names.Representation, ElementVisualRepresentation.None),
                StrokeDashArray = Text(source, Names.StrokeDashArray),
                StrokeThickness = ReadDouble(source, Names.StrokeThickness),
            };

            XElement? attributes = source.Element(Names.Attributes);
            if (attributes is not null)
            {
                foreach (XElement attribute in attributes.Elements(Names.Attribute))
                {
                    element.Attributes.Add(ReadKnowledgeBaseAttribute(attribute));
                }
            }

            return element;
        }

        private static KnowledgeBaseAttribute ReadKnowledgeBaseAttribute(XElement source)
        {
            KnowledgeBaseAttribute attribute = new KnowledgeBaseAttribute
            {
                Id = Text(source, Names.Id),
                DisplayName = Text(source, Names.DisplayName),
                Name = Text(source, Names.Name),
                Type = ReadEnum(source, Names.Type, AttributeType.List),
                Inheritance = ReadEnum(
                    source,
                    Names.Inheritance,
                    ReadEnum(source, Names.AttributeInheritance, AttributeInheritance.Virtual)),
                Mode = ReadEnum(
                    source,
                    Names.Mode,
                    ReadEnum(source, Names.AttributeMode, AttributeMode.Dynamic)),
                IsInherited = ReadBool(source, Names.IsInherited),
                IsOverrided = ReadBool(source, Names.IsOverrided),
            };

            XElement? values = source.Element(Names.AttributeValues);
            if (values is not null)
            {
                foreach (XElement value in values.Elements(Names.Value))
                {
                    attribute.AttributeValues.Add(value.Value);
                }
            }

            return attribute;
        }

        private static void ReadThreatCategories(List<ThreatCategory> target, XElement? source)
        {
            if (source is null)
            {
                return;
            }

            foreach (XElement element in source.Elements(Names.ThreatCategory))
            {
                target.Add(new ThreatCategory
                {
                    Id = Text(element, Names.Id),
                    LongDescription = Text(element, Names.LongDescription),
                    ShortDescription = Text(element, Names.ShortDescription),
                    Name = Text(element, Names.Name),
                });
            }
        }

        private static void ReadThreatTypes(List<ThreatType> target, XElement? source)
        {
            if (source is null)
            {
                return;
            }

            foreach (XElement element in source.Elements(Names.ThreatType))
            {
                target.Add(ReadThreatType(element));
            }
        }

        private static ThreatType ReadThreatType(XElement source)
        {
            ThreatType threatType = new ThreatType
            {
                Category = Text(source, Names.Category),
                Description = Text(source, Names.Description),
                Id = Text(source, Names.Id),
                RelatedCategory = Text(source, Names.RelatedCategory),
                ShortTitle = Text(source, Names.ShortTitle),
            };

            XElement? filters = source.Element(Names.GenerationFilters);
            if (filters is not null)
            {
                threatType.GenerationFilters.Include = Text(filters, Names.Include) ?? string.Empty;
                threatType.GenerationFilters.Exclude = Text(filters, Names.Exclude) ?? string.Empty;
            }

            ReadThreatMetaData(threatType.PropertiesMetaData, source.Element(Names.PropertiesMetaData));
            return threatType;
        }

        private static string? Text(XElement parent, string childName)
        {
            return parent.Element(childName)?.Value;
        }

        private static bool ReadBool(XElement parent, string childName)
        {
            return string.Equals(parent.Element(childName)?.Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static int ReadInt(XElement parent, string childName)
        {
            string? raw = parent.Element(childName)?.Value;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }

        private static double ReadDouble(XElement parent, string childName)
        {
            string? raw = parent.Element(childName)?.Value;
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0.0;
        }

        private static TEnum ReadEnum<TEnum>(XElement parent, string childName, TEnum fallback)
            where TEnum : struct
        {
            string? raw = parent.Element(childName)?.Value;
            return Enum.TryParse(raw, out TEnum value) ? value : fallback;
        }

        private static XElement WriteManifest(Manifest manifest)
        {
            return new XElement(
                Names.Manifest,
                new XAttribute(Names.NameAttribute, manifest.Name ?? string.Empty),
                new XAttribute(Names.IdAttribute, manifest.Id.ToString()),
                new XAttribute(Names.VersionAttribute, manifest.Version ?? string.Empty),
                new XAttribute(Names.AuthorAttribute, manifest.Author ?? string.Empty));
        }

        private static XElement WriteThreatMetaData(ThreatMetaData metaData)
        {
            XElement properties = new XElement(Names.PropertiesMetaData);
            foreach (ThreatMetaDatum datum in metaData.PropertiesMetaData)
            {
                properties.Add(WriteThreatMetaDatum(datum));
            }

            return new XElement(
                Names.ThreatMetaData,
                BoolElement(Names.IsPriorityUsed, metaData.IsPriorityUsed),
                BoolElement(Names.IsStatusUsed, metaData.IsStatusUsed),
                properties);
        }

        private static XElement WriteThreatMetaDatum(ThreatMetaDatum datum)
        {
            XElement values = new XElement(Names.Values);
            foreach (string value in datum.Values)
            {
                values.Add(new XElement(Names.Value, value));
            }

            XElement element = new XElement(
                Names.ThreatMetaDatum,
                new XElement(Names.Name, datum.Name ?? string.Empty),
                new XElement(Names.Label, datum.Label ?? string.Empty),
                BoolElement(Names.HideFromUI, datum.HideFromUI),
                values);

            if (datum.Description is not null)
            {
                element.Add(new XElement(Names.Description, datum.Description));
            }

            if (!string.IsNullOrEmpty(datum.Id))
            {
                element.Add(new XElement(Names.Id, datum.Id));
            }

            element.Add(new XElement(Names.AttributeType, datum.AttributeType.ToString(CultureInfo.InvariantCulture)));
            return element;
        }

        private static XElement WriteElementTypes(string containerName, IEnumerable<ElementType> elements)
        {
            XElement container = new XElement(containerName);
            foreach (ElementType element in elements)
            {
                container.Add(WriteElementType(element));
            }

            return container;
        }

        private static XElement WriteElementType(ElementType element)
        {
            XElement attributes = new XElement(Names.Attributes);
            foreach (KnowledgeBaseAttribute attribute in element.Attributes)
            {
                attributes.Add(WriteKnowledgeBaseAttribute(attribute));
            }

            XElement result = new XElement(
                Names.ElementType,
                new XElement(Names.Name, element.Name ?? string.Empty),
                new XElement(Names.ElementId, element.Id ?? string.Empty),
                new XElement(Names.Description, element.Description ?? string.Empty),
                new XElement(Names.ParentElement, element.ParentId ?? string.Empty));

            if (element.ImageSource is not null)
            {
                result.Add(new XElement(Names.Image, element.ImageSource));
            }

            result.Add(BoolElement(Names.Hidden, element.Hidden));

            if (element.Behavior is not null)
            {
                result.Add(new XElement(Names.Behavior, element.Behavior));
            }

            if (element.Shape is not null)
            {
                result.Add(new XElement(Names.Shape, element.Shape));
            }

            result.Add(
                new XElement(Names.Representation, element.Representation.ToString()),
                new XElement(Names.StrokeThickness, element.StrokeThickness.ToString(CultureInfo.InvariantCulture)));

            if (element.StrokeDashArray is not null)
            {
                result.Add(new XElement(Names.StrokeDashArray, element.StrokeDashArray));
            }

            if (element.ImageLocation is not null)
            {
                result.Add(new XElement(Names.ImageLocation, element.ImageLocation));
            }

            result.Add(attributes);

            return result;
        }

        private static XElement WriteKnowledgeBaseAttribute(KnowledgeBaseAttribute attribute)
        {
            XElement values = new XElement(Names.AttributeValues);
            foreach (string value in attribute.AttributeValues)
            {
                values.Add(new XElement(Names.Value, value));
            }

            return new XElement(
                Names.Attribute,
                new XElement(Names.Id, attribute.Id ?? string.Empty),
                BoolElement(Names.IsInherited, attribute.IsInherited),
                BoolElement(Names.IsOverrided, attribute.IsOverrided),
                new XElement(Names.Name, attribute.Name ?? string.Empty),
                new XElement(Names.DisplayName, attribute.DisplayName ?? string.Empty),
                new XElement(Names.Mode, attribute.Mode.ToString()),
                new XElement(Names.Type, attribute.Type.ToString()),
                new XElement(Names.Inheritance, attribute.Inheritance.ToString()),
                values);
        }

        private static XElement WriteThreatCategories(IEnumerable<ThreatCategory> categories)
        {
            XElement container = new XElement(Names.ThreatCategories);
            foreach (ThreatCategory category in categories)
            {
                container.Add(new XElement(
                    Names.ThreatCategory,
                    new XElement(Names.Name, category.Name ?? string.Empty),
                    new XElement(Names.Id, category.Id ?? string.Empty),
                    new XElement(Names.ShortDescription, category.ShortDescription ?? string.Empty),
                    new XElement(Names.LongDescription, category.LongDescription ?? string.Empty)));
            }

            return container;
        }

        private static XElement WriteThreatTypes(IEnumerable<ThreatType> threatTypes)
        {
            XElement container = new XElement(Names.ThreatTypes);
            foreach (ThreatType threatType in threatTypes)
            {
                XElement properties = new XElement(Names.PropertiesMetaData);
                foreach (ThreatMetaDatum datum in threatType.PropertiesMetaData)
                {
                    properties.Add(WriteThreatMetaDatum(datum));
                }

                XElement element = new XElement(
                    Names.ThreatType,
                    new XElement(
                        Names.GenerationFilters,
                        new XElement(Names.Include, threatType.GenerationFilters.Include),
                        new XElement(Names.Exclude, threatType.GenerationFilters.Exclude)),
                    new XElement(Names.Id, threatType.Id ?? string.Empty),
                    new XElement(Names.ShortTitle, threatType.ShortTitle ?? string.Empty),
                    new XElement(Names.Category, threatType.Category ?? string.Empty));

                if (threatType.RelatedCategory is not null)
                {
                    element.Add(new XElement(Names.RelatedCategory, threatType.RelatedCategory));
                }

                element.Add(
                    new XElement(Names.Description, threatType.Description ?? string.Empty),
                    properties);
                container.Add(element);
            }

            return container;
        }

        private static XElement BoolElement(string name, bool value)
        {
            return new XElement(name, value ? "true" : "false");
        }

        /// <summary>
        /// Element and attribute names used by the <c>.tb7</c> on-disk format.
        /// </summary>
        private static class Names
        {
            public const string KnowledgeBase = "KnowledgeBase";
            public const string Manifest = "Manifest";
            public const string ThreatMetaData = "ThreatMetaData";
            public const string GenericElements = "GenericElements";
            public const string StandardElements = "StandardElements";
            public const string ThreatCategories = "ThreatCategories";
            public const string ThreatTypes = "ThreatTypes";
            public const string ElementType = "ElementType";
            public const string ThreatType = "ThreatType";
            public const string ThreatCategory = "ThreatCategory";
            public const string Attributes = "Attributes";
            public const string Attribute = "Attribute";
            public const string AttributeValues = "AttributeValues";
            public const string PropertiesMetaData = "PropertiesMetaData";
            public const string ThreatMetaDatum = "ThreatMetaDatum";
            public const string Values = "Values";
            public const string Value = "Value";
            public const string GenerationFilters = "GenerationFilters";
            public const string Include = "Include";
            public const string Exclude = "Exclude";
            public const string Name = "Name";
            public const string DisplayName = "DisplayName";
            public const string Id = "Id";
            public const string ElementId = "ID";
            public const string Description = "Description";
            public const string ShortTitle = "ShortTitle";
            public const string ShortDescription = "ShortDescription";
            public const string LongDescription = "LongDescription";
            public const string Category = "Category";
            public const string RelatedCategory = "RelatedCategory";
            public const string ParentElement = "ParentElement";
            public const string Image = "Image";
            public const string ImageLocation = "ImageLocation";
            public const string Behavior = "Behavior";
            public const string Shape = "Shape";
            public const string Representation = "Representation";
            public const string StrokeThickness = "StrokeThickness";
            public const string StrokeDashArray = "StrokeDashArray";
            public const string Hidden = "Hidden";
            public const string HideFromUI = "HideFromUI";
            public const string IsPriorityUsed = "IsPriorityUsed";
            public const string IsStatusUsed = "IsStatusUsed";
            public const string IsInherited = "IsInherited";
            public const string IsOverrided = "IsOverrided";
            public const string AttributeType = "AttributeType";
            public const string Label = "Label";
            public const string Type = "Type";
            public const string Mode = "Mode";
            public const string Inheritance = "Inheritance";
            public const string AttributeMode = "AttributeMode";
            public const string AttributeInheritance = "AttributeInheritance";
            public const string NameAttribute = "name";
            public const string IdAttribute = "id";
            public const string VersionAttribute = "version";
            public const string AuthorAttribute = "author";
        }
    }
}
