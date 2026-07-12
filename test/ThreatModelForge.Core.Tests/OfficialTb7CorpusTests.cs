namespace ThreatModelForge.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Xml.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Regression tests derived from the pinned official Microsoft TB7 corpus.
    /// </summary>
    [TestClass]
    public class OfficialTb7CorpusTests
    {
        private const string FixtureDirectory = "Fixtures/OfficialTb7";

        /// <summary>
        /// Verifies fixture integrity and semantic stability across two serializer passes.
        /// </summary>
        /// <param name="fileName">The compressed fixture name.</param>
        /// <param name="checksum">The expected compressed fixture SHA-256.</param>
        /// <param name="elementCount">The expected element-type count.</param>
        /// <param name="threatCount">The expected threat-type count.</param>
        /// <param name="staticAttributeCount">The expected Static attribute count.</param>
        /// <param name="dynamicAttributeCount">The expected Dynamic attribute count.</param>
        [TestMethod]
        [DataRow("default.tb7.gz", "7925a08ccf55c1d049433765c5741a44372fec0e34dd7209bc386329cf06a4e1", 59, 47, 40, 69)]
        [DataRow("Azure Cloud Services.tb7.gz", "f36363e1156604f99bc20661181431f5eee07bf28e2cb43ed2b7dcd3f5b9a0a2", 55, 191, 0, 68)]
        [DataRow("MedicalDeviceTemplate.tb7.gz", "f10ef8f862f9cc649bff4cfef2a42727f5a1e76dcc2fe3af52d3d5d299dab988", 87, 125, 105, 78)]
        public void OfficialTemplateSemanticallyRoundTrips(
            string fileName,
            string checksum,
            int elementCount,
            int threatCount,
            int staticAttributeCount,
            int dynamicAttributeCount)
        {
            string path = Path.Join(AppContext.BaseDirectory, FixtureDirectory, fileName);
            byte[] compressed = File.ReadAllBytes(path);
            string actualChecksum = Convert.ToHexString(SHA256.HashData(compressed)).ToLowerInvariant();
            Assert.AreEqual(checksum, actualChecksum, $"Fixture checksum mismatch for {fileName}.");

            byte[] decompressed = Decompress(compressed);
            XDocument sourceDocument = XDocument.Parse(Encoding.UTF8.GetString(decompressed));
            AssertKnownVocabulary(sourceDocument, fileName);

            KnowledgeBaseData source = Load(decompressed);
            Assert.AreEqual(elementCount, source.GenericElements.Count + source.StandardElements.Count);
            Assert.AreEqual(threatCount, source.ThreatTypes.Count);
            Assert.AreEqual(SemanticSnapshot(sourceDocument), SemanticSnapshot(source), $"Source load lost data for {fileName}.");

            IEnumerable<KnowledgeBaseAttribute> attributes = source.GenericElements
                .Concat(source.StandardElements)
                .SelectMany(element => element.Attributes);
            Assert.AreEqual(staticAttributeCount, attributes.Count(attribute => attribute.Mode == AttributeMode.Static));
            Assert.AreEqual(dynamicAttributeCount, attributes.Count(attribute => attribute.Mode == AttributeMode.Dynamic));

            byte[] firstSave = Save(source);
            KnowledgeBaseData reloaded = Load(firstSave);
            byte[] secondSave = Save(reloaded);

            Assert.AreEqual(SemanticSnapshot(source), SemanticSnapshot(reloaded), fileName);
            CollectionAssert.AreEqual(firstSave, secondSave, $"Canonical serialization changed for {fileName}.");

            XDocument document = XDocument.Parse(Encoding.UTF8.GetString(firstSave));
            Assert.AreEqual(0, document.Descendants("AttributeMode").Count());
            Assert.AreEqual(0, document.Descendants("AttributeInheritance").Count());
            Assert.IsTrue(document.Descendants("Mode").Any());
            Assert.IsTrue(document.Descendants("Inheritance").Any());
        }

        private static byte[] Decompress(byte[] content)
        {
            using MemoryStream compressed = new MemoryStream(content);
            using GZipStream gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using MemoryStream decompressed = new MemoryStream();
            gzip.CopyTo(decompressed);
            return decompressed.ToArray();
        }

        private static KnowledgeBaseData Load(byte[] content)
        {
            using MemoryStream stream = new MemoryStream(content);
            return KnowledgeBaseData.Load(stream);
        }

        private static byte[] Save(KnowledgeBaseData knowledgeBase)
        {
            using MemoryStream stream = new MemoryStream();
            knowledgeBase.Save(stream);
            return stream.ToArray();
        }

        private static string SemanticSnapshot(KnowledgeBaseData knowledgeBase)
        {
            StringBuilder snapshot = new StringBuilder();
            Manifest manifest = knowledgeBase.Manifest ?? new Manifest();
            Append(snapshot, manifest.Name, manifest.Id.ToString(), manifest.Version, manifest.Author);

            AppendThreatMetaData(snapshot, knowledgeBase.ThreatMetaData);
            AppendElementTypes(snapshot, knowledgeBase.GenericElements);
            AppendElementTypes(snapshot, knowledgeBase.StandardElements);

            foreach (ThreatCategory category in knowledgeBase.ThreatCategories)
            {
                Append(snapshot, category.Id, category.Name, category.ShortDescription, category.LongDescription);
            }

            foreach (ThreatType threat in knowledgeBase.ThreatTypes)
            {
                Append(
                    snapshot,
                    threat.Id,
                    threat.ShortTitle,
                    threat.Category,
                    threat.RelatedCategory,
                    threat.Description,
                    threat.GenerationFilters.Include,
                    threat.GenerationFilters.Exclude);
                AppendThreatMetaData(snapshot, threat.PropertiesMetaData);
            }

            return snapshot.ToString();
        }

        private static string SemanticSnapshot(XDocument document)
        {
            XElement? documentRoot = document.Root;
            Assert.IsNotNull(documentRoot);
            XElement root = documentRoot;
            StringBuilder snapshot = new StringBuilder();
            XElement? manifestElement = root.Element("Manifest");
            Assert.IsNotNull(manifestElement);
            XElement manifest = manifestElement;
            XAttribute? idAttribute = manifest.Attribute("id");
            Assert.IsNotNull(idAttribute);
            Guid manifestId = Guid.Parse(idAttribute.Value);
            Append(
                snapshot,
                manifest.Attribute("name")?.Value,
                manifestId.ToString(),
                manifest.Attribute("version")?.Value,
                manifest.Attribute("author")?.Value);

            AppendThreatMetaData(snapshot, root.Element("ThreatMetaData"));
            AppendElementTypes(snapshot, root.Element("GenericElements")?.Elements("ElementType") ?? Enumerable.Empty<XElement>());
            AppendElementTypes(snapshot, root.Element("StandardElements")?.Elements("ElementType") ?? Enumerable.Empty<XElement>());

            foreach (XElement category in root.Element("ThreatCategories")?.Elements("ThreatCategory") ?? Enumerable.Empty<XElement>())
            {
                Append(
                    snapshot,
                    Text(category, "Id"),
                    Text(category, "Name"),
                    Text(category, "ShortDescription"),
                    Text(category, "LongDescription"));
            }

            foreach (XElement threat in root.Element("ThreatTypes")?.Elements("ThreatType") ?? Enumerable.Empty<XElement>())
            {
                XElement? filters = threat.Element("GenerationFilters");
                Append(
                    snapshot,
                    Text(threat, "Id"),
                    Text(threat, "ShortTitle"),
                    Text(threat, "Category"),
                    Text(threat, "RelatedCategory"),
                    Text(threat, "Description"),
                    Text(filters, "Include") ?? string.Empty,
                    Text(filters, "Exclude") ?? string.Empty);
                AppendThreatMetaData(snapshot, threat.Element("PropertiesMetaData"));
            }

            return snapshot.ToString();
        }

        private static void AppendElementTypes(StringBuilder snapshot, IEnumerable<ElementType> elements)
        {
            foreach (ElementType element in elements)
            {
                Append(
                    snapshot,
                    element.Id,
                    element.Name,
                    element.Description,
                    element.ParentId,
                    element.ImageSource,
                    element.ImageLocation,
                    element.Behavior,
                    element.Shape,
                    element.StrokeDashArray,
                    element.Hidden.ToString(),
                    element.Representation.ToString(),
                    element.StrokeThickness.ToString("R", System.Globalization.CultureInfo.InvariantCulture));

                foreach (KnowledgeBaseAttribute attribute in element.Attributes)
                {
                    Append(
                        snapshot,
                        attribute.Id,
                        attribute.Name,
                        attribute.DisplayName,
                        attribute.Type.ToString(),
                        attribute.Mode.ToString(),
                        attribute.Inheritance.ToString(),
                        attribute.IsInherited.ToString(),
                        attribute.IsOverrided.ToString());
                    Append(snapshot, attribute.AttributeValues);
                }
            }
        }

        private static void AppendElementTypes(StringBuilder snapshot, IEnumerable<XElement> elements)
        {
            foreach (XElement element in elements)
            {
                Append(
                    snapshot,
                    Text(element, "ID"),
                    Text(element, "Name"),
                    (Text(element, "Description") ?? string.Empty).Trim(),
                    Text(element, "ParentElement"),
                    Text(element, "Image"),
                    Text(element, "ImageLocation"),
                    Text(element, "Behavior"),
                    Text(element, "Shape"),
                    Text(element, "StrokeDashArray"),
                    ReadBool(element, "Hidden").ToString(),
                    ReadEnum(element, "Representation", ElementVisualRepresentation.None).ToString(),
                    ReadDouble(element, "StrokeThickness").ToString("R", CultureInfo.InvariantCulture));

                foreach (XElement attribute in element.Element("Attributes")?.Elements("Attribute") ?? Enumerable.Empty<XElement>())
                {
                    Append(
                        snapshot,
                        Text(attribute, "Id"),
                        Text(attribute, "Name"),
                        Text(attribute, "DisplayName"),
                        ReadEnum(attribute, "Type", AttributeType.List).ToString(),
                        ReadEnum(attribute, "Mode", AttributeMode.Dynamic).ToString(),
                        ReadEnum(attribute, "Inheritance", AttributeInheritance.Virtual).ToString(),
                        ReadBool(attribute, "IsInherited").ToString(),
                        ReadBool(attribute, "IsOverrided").ToString());
                    IEnumerable<string> values = attribute.Element("AttributeValues")?.Elements("Value").Select(value => value.Value)
                        ?? Enumerable.Empty<string>();
                    Append(snapshot, values);
                }
            }
        }

        private static void AppendThreatMetaData(StringBuilder snapshot, ThreatMetaData? metaData)
        {
            if (metaData is null)
            {
                Append(snapshot, (string?)null);
                return;
            }

            Append(snapshot, metaData.IsPriorityUsed.ToString(), metaData.IsStatusUsed.ToString());
            AppendThreatMetaData(snapshot, metaData.PropertiesMetaData);
        }

        private static void AppendThreatMetaData(StringBuilder snapshot, IEnumerable<ThreatMetaDatum> metadata)
        {
            foreach (ThreatMetaDatum datum in metadata)
            {
                Append(
                    snapshot,
                    datum.Id,
                    datum.Name,
                    datum.Label,
                    datum.Description,
                    datum.HideFromUI.ToString(),
                    datum.AttributeType.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Append(snapshot, datum.Values);
            }
        }

        private static void AppendThreatMetaData(StringBuilder snapshot, XElement? metaData)
        {
            if (metaData is null)
            {
                Append(snapshot, (string?)null);
                return;
            }

            if (string.Equals(metaData.Name.LocalName, "ThreatMetaData", StringComparison.Ordinal))
            {
                Append(
                    snapshot,
                    ReadBool(metaData, "IsPriorityUsed").ToString(),
                    ReadBool(metaData, "IsStatusUsed").ToString());
                metaData = metaData.Element("PropertiesMetaData");
            }

            foreach (XElement datum in metaData?.Elements("ThreatMetaDatum") ?? Enumerable.Empty<XElement>())
            {
                Append(
                    snapshot,
                    Text(datum, "Id"),
                    Text(datum, "Name"),
                    Text(datum, "Label"),
                    Text(datum, "Description"),
                    ReadBool(datum, "HideFromUI").ToString(),
                    ReadInt(datum, "AttributeType").ToString(CultureInfo.InvariantCulture));
                IEnumerable<string> values = datum.Element("Values")?.Elements("Value").Select(value => value.Value)
                    ?? Enumerable.Empty<string>();
                Append(snapshot, values);
            }
        }

        private static void AssertKnownVocabulary(XDocument document, string fileName)
        {
            AssertChildren(document.Descendants("ElementType"), fileName, "Name", "ID", "Description", "ParentElement", "Image", "Hidden", "Behavior", "Shape", "Representation", "StrokeThickness", "StrokeDashArray", "ImageLocation", "Attributes");
            AssertChildren(document.Descendants("Attribute"), fileName, "Id", "IsInherited", "IsOverrided", "Name", "DisplayName", "Mode", "Type", "Inheritance", "AttributeValues");
            AssertChildren(document.Descendants("ThreatType"), fileName, "GenerationFilters", "Id", "ShortTitle", "Category", "RelatedCategory", "Description", "PropertiesMetaData");
            AssertChildren(document.Descendants("ThreatCategory"), fileName, "Name", "Id", "ShortDescription", "LongDescription");
            AssertChildren(document.Descendants("ThreatMetaDatum"), fileName, "Name", "Label", "HideFromUI", "Values", "Description", "Id", "AttributeType");
            AssertChildren(document.Descendants("GenerationFilters"), fileName, "Include", "Exclude");
        }

        private static void AssertChildren(IEnumerable<XElement> elements, string fileName, params string[] allowedNames)
        {
            HashSet<string> allowed = new HashSet<string>(allowedNames, StringComparer.Ordinal);
            foreach (XElement element in elements)
            {
                foreach (XElement child in element.Elements())
                {
                    Assert.IsTrue(
                        allowed.Contains(child.Name.LocalName),
                        $"Unaccounted <{child.Name.LocalName}> in <{element.Name.LocalName}> for {fileName}.");
                }
            }
        }

        private static string? Text(XElement? parent, string childName)
        {
            return parent?.Element(childName)?.Value;
        }

        private static bool ReadBool(XElement parent, string childName)
        {
            return string.Equals(Text(parent, childName), "true", StringComparison.OrdinalIgnoreCase);
        }

        private static int ReadInt(XElement parent, string childName)
        {
            return int.TryParse(Text(parent, childName), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }

        private static double ReadDouble(XElement parent, string childName)
        {
            return double.TryParse(Text(parent, childName), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0.0;
        }

        private static TEnum ReadEnum<TEnum>(XElement parent, string childName, TEnum fallback)
            where TEnum : struct
        {
            return Enum.TryParse(Text(parent, childName), out TEnum value) ? value : fallback;
        }

        private static void Append(StringBuilder snapshot, IEnumerable<string> values)
        {
            foreach (string value in values)
            {
                Append(snapshot, value);
            }
        }

        private static void Append(StringBuilder snapshot, params string?[] values)
        {
            foreach (string? value in values)
            {
                snapshot.Append(value?.Length ?? -1).Append(':').Append(value).Append('|');
            }
        }
    }
}
