namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>Tests MTMT template compilation into version 2 declarative rule packs.</summary>
    [TestClass]
    public class MtmtRulePackCompilerTests
    {
        private const string FixtureDirectory = "Fixtures/OfficialTb7";

        /// <summary>The same source bytes produce a strict, byte-stable, executable pack.</summary>
        [TestMethod]
        public void CompileProducesDeterministicLoadableVersionTwoPack()
        {
            KnowledgeBaseData knowledgeBase = CreateKnowledgeBase();
            byte[] source = Save(knowledgeBase);

            MtmtRulePackCompilation first = MtmtRulePackCompiler.Compile(source, "medical.tb7");
            MtmtRulePackCompilation second = MtmtRulePackCompiler.Compile(source, "medical.tb7");

            CollectionAssert.AreEqual(first.Content, second.Content);
            Assert.AreEqual(1, first.SourceThreatCount);
            Assert.AreEqual(1, first.EmittedRuleCount);
            Assert.AreEqual(0, first.SkippedThreatCount);
            Assert.AreEqual(1, first.CategoryDistribution["privacy"]);
            using (JsonDocument json = JsonDocument.Parse(first.Content))
            {
                JsonElement root = json.RootElement;
                Assert.AreEqual("tmforge-rules", root.GetProperty("schema").GetString());
                Assert.AreEqual(2, root.GetProperty("version").GetInt32());
                Assert.AreEqual("urn:tmforge:rules:interaction-v1", root.GetProperty("dialect").GetString());
                Assert.AreEqual("Privacy Reviewed", root.GetProperty("properties")[0].GetProperty("name").GetString());
                Assert.AreEqual("Privacy Reviewed", root.GetProperty("rules")[0]
                    .GetProperty("expression").GetProperty("allOf")[0]
                    .GetProperty("allOf")[1].GetProperty("property").GetString());
                JsonElement sourceMetadata = root.GetProperty("pack").GetProperty("source").GetProperty("metadata");
                Assert.IsTrue(sourceMetadata.EnumerateArray().Any(item =>
                    item.GetProperty("key").GetString() == "urn:tmforge:source:mtmt-tb7:manifest-author"));
            }

            string directory = Path.Join(Path.GetTempPath(), "tmforge-compiler-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Join(directory, "medical.tmrules.json");
                File.WriteAllBytes(path, first.Content);
                System.Collections.Generic.List<string> diagnostics = new System.Collections.Generic.List<string>();

                RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { path }, diagnostics.Add);

                Assert.AreEqual(0, diagnostics.Count, string.Join(Environment.NewLine, diagnostics));
                Assert.AreEqual(1, bundle.Rules.Count);
                Assert.IsTrue(bundle.Packs.Single().Source!.Metadata.ContainsKey(
                    "urn:tmforge:source:mtmt-tb7:manifest-author"));
                Rule rule = bundle.Rules.Single();
                Assert.IsTrue(rule.ID.EndsWith("/PRIV-1", StringComparison.Ordinal));
                Assert.AreEqual("Privacy", rule.ThreatCategory!.Name);
                Assert.AreEqual("privacy", rule.Provenance!.CategoryId);
                Assert.HasCount(2, rule.Provenance.Expressions);
                Assert.AreEqual("include", rule.Provenance.Expressions[0].Role);
                Assert.AreEqual("exclude", rule.Provenance.Expressions[1].Role);

                using RuleSet ruleSet = new RuleSet();
                ruleSet.Rules.Add(rule);
                GenerationResult generated = ThreatGenerator.Generate(CreateMatchingModel(), ruleSet);
                GeneratedThreat generatedThreat = generated.Threats.Single();
                Assert.AreEqual(rule.ID, generatedThreat.RuleId);
                Assert.AreEqual("Privacy", generatedThreat.ThreatCategory.Name);
                StringAssert.Contains(generatedThreat.Title, "Records");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>Every pinned official threat compiles into a deterministic, strict-loadable rule.</summary>
        /// <param name="fileName">The compressed fixture name.</param>
        /// <param name="threatCount">The expected source and emitted threat count.</param>
        /// <param name="high">The expected High priority count.</param>
        /// <param name="medium">The expected Medium priority count.</param>
        /// <param name="low">The expected Low priority count.</param>
        /// <param name="mitigations">The expected non-empty mitigation count.</param>
        /// <param name="warnings">The expected preserved unsupported-metadata warning count.</param>
        [TestMethod]
        [DataRow("default.tb7.gz", 47, 0, 0, 0, 0, 2)]
        [DataRow("Azure Cloud Services.tb7.gz", 191, 164, 24, 3, 190, 2)]
        [DataRow("MedicalDeviceTemplate.tb7.gz", 125, 0, 0, 0, 0, 2)]
        public void OfficialCorpusCompilesWithoutSkippedThreats(
            string fileName,
            int threatCount,
            int high,
            int medium,
            int low,
            int mitigations,
            int warnings)
        {
            byte[] source = Decompress(Path.Join(AppContext.BaseDirectory, FixtureDirectory, fileName));
            using MemoryStream stream = new MemoryStream(source);
            _ = KnowledgeBaseData.Load(stream);

            MtmtRulePackCompilation first = MtmtRulePackCompiler.Compile(
                source,
                fileName.Substring(0, fileName.Length - ".gz".Length));
            MtmtRulePackCompilation second = MtmtRulePackCompiler.Compile(
                source,
                fileName.Substring(0, fileName.Length - ".gz".Length));

            CollectionAssert.AreEqual(first.Content, second.Content, fileName);
            Assert.AreEqual(threatCount, first.SourceThreatCount, fileName);
            Assert.AreEqual(threatCount, first.EmittedRuleCount, fileName);
            Assert.AreEqual(0, first.SkippedThreatCount, fileName);
            Assert.IsFalse(first.Diagnostics.Any(diagnostic => diagnostic.IsError), fileName);
            Assert.AreEqual(warnings, first.WarningCount, fileName);

            using (JsonDocument json = JsonDocument.Parse(first.Content))
            {
                JsonElement rules = json.RootElement.GetProperty("rules");
                Assert.AreEqual(threatCount, rules.GetArrayLength(), fileName);
                Assert.AreEqual(high, CountValue(rules, "defaultPriority", "High"), fileName);
                Assert.AreEqual(medium, CountValue(rules, "defaultPriority", "Medium"), fileName);
                Assert.AreEqual(low, CountValue(rules, "defaultPriority", "Low"), fileName);
                int mitigationCount = rules.EnumerateArray().Count(rule =>
                    rule.TryGetProperty("helpText", out JsonElement help) &&
                    !string.IsNullOrWhiteSpace(help.GetString()));
                Assert.AreEqual(mitigations, mitigationCount, fileName);
                int strideCount = rules.EnumerateArray().Count(rule => rule.TryGetProperty("stride", out _));
                int expectedStride = string.Equals(fileName, "MedicalDeviceTemplate.tb7.gz", StringComparison.Ordinal)
                    ? 106
                    : threatCount;
                Assert.AreEqual(expectedStride, strideCount, fileName);
                JsonElement sourceMetadata = json.RootElement.GetProperty("pack").GetProperty("source").GetProperty("metadata");
                Assert.IsTrue(sourceMetadata.GetArrayLength() > 0, fileName);
            }

            if (string.Equals(fileName, "MedicalDeviceTemplate.tb7.gz", StringComparison.Ordinal))
            {
                Assert.AreEqual(12, first.CategoryDistribution["7aa66497-ce96-4295-b2cc-ef556daf0c13"]);
                Assert.AreEqual(6, first.CategoryDistribution["A"]);
                Assert.AreEqual(1, first.CategoryDistribution["963cfa0d-0d15-4186-9b4a-4c88154a687a"]);
            }

            string directory = Path.Join(Path.GetTempPath(), "tmforge-corpus-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Join(directory, "pack.tmrules.json");
                File.WriteAllBytes(path, first.Content);
                List<string> diagnostics = new List<string>();

                RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { path }, diagnostics.Add);

                Assert.AreEqual(0, diagnostics.Count, string.Join(Environment.NewLine, diagnostics));
                Assert.AreEqual(threatCount, bundle.Rules.Count, fileName);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>Malformed and oversized source bytes fail through a controlled data boundary.</summary>
        [TestMethod]
        public void InvalidSourceBytesAreRejected()
        {
            InvalidDataException wrongRoot = Assert.Throws<InvalidDataException>(() =>
                MtmtRulePackCompiler.Compile(Encoding.UTF8.GetBytes("<NotKnowledgeBase />"), "invalid.tb7"));
            StringAssert.Contains(wrongRoot.Message, "Invalid MTMT template");

            byte[] oversized = new byte[MtmtRulePackCompiler.MaxSourceBytes + 1];
            InvalidDataException tooLarge = Assert.Throws<InvalidDataException>(() =>
                MtmtRulePackCompiler.Compile(oversized, "large.tb7"));
            StringAssert.Contains(tooLarge.Message, "exceeds the limit");

            byte[] invalidUtf8 = Encoding.UTF8.GetBytes("<KnowledgeBase><Manifest name=\"x");
            invalidUtf8 = invalidUtf8.Concat(new byte[] { 0xC3, 0x28 })
                .Concat(Encoding.UTF8.GetBytes("\" /></KnowledgeBase>"))
                .ToArray();
            InvalidDataException malformed = Assert.Throws<InvalidDataException>(() =>
                MtmtRulePackCompiler.Compile(invalidUtf8, "utf8.tb7"));
            StringAssert.Contains(malformed.Message, "Invalid MTMT template");

            byte[] namespaced = Encoding.UTF8.GetBytes(
                "<KnowledgeBase xmlns=\"urn:foreign\"><ThreatTypes /></KnowledgeBase>");
            InvalidDataException foreignNamespace = Assert.Throws<InvalidDataException>(() =>
                MtmtRulePackCompiler.Compile(namespaced, "namespace.tb7"));
            StringAssert.Contains(foreignNamespace.Message, "unqualified");

            byte[] utf16 = Encoding.Unicode.GetPreamble()
                .Concat(Encoding.Unicode.GetBytes("<KnowledgeBase />"))
                .ToArray();
            InvalidDataException unsupportedEncoding = Assert.Throws<InvalidDataException>(() =>
                MtmtRulePackCompiler.Compile(utf16, "utf16.tb7"));
            StringAssert.Contains(unsupportedEncoding.Message, "Invalid MTMT template");
        }

        /// <summary>Duplicate threat ids and broken catalogs are pack-fatal rather than order-dependent.</summary>
        [TestMethod]
        public void InvalidPackCatalogIsRejectedBeforeSerialization()
        {
            KnowledgeBaseData duplicate = CreateKnowledgeBase();
            duplicate.ThreatTypes.Add(new ThreatType
            {
                Id = "priv-1",
                Category = "privacy",
                ShortTitle = "Duplicate",
                GenerationFilters = new GenerationFilters { Include = "source is 'GE.P'" },
            });
            InvalidDataException duplicateError = Assert.Throws<InvalidDataException>(() =>
                MtmtRulePackCompiler.Compile(Save(duplicate), "duplicate.tb7"));
            StringAssert.Contains(duplicateError.Message, "duplicate threat id");

            KnowledgeBaseData undefinedCategory = CreateKnowledgeBase();
            undefinedCategory.ThreatTypes[0].Category = "missing";
            InvalidDataException categoryError = Assert.Throws<InvalidDataException>(() =>
                MtmtRulePackCompiler.Compile(Save(undefinedCategory), "category.tb7"));
            StringAssert.Contains(categoryError.Message, "references unknown category");

            KnowledgeBaseData missingAncestor = CreateKnowledgeBase();
            missingAncestor.GenericElements.Add(new ElementType { Id = "A", Name = "A", ParentId = "B" });
            missingAncestor.GenericElements.Add(new ElementType { Id = "B", Name = "B", ParentId = "MISSING" });
            InvalidDataException ancestorError = Assert.Throws<InvalidDataException>(() =>
                MtmtRulePackCompiler.Compile(Save(missingAncestor), "ancestor.tb7"));
            StringAssert.Contains(ancestorError.Message, "unknown parent");

            KnowledgeBaseData missingTitle = CreateKnowledgeBase();
            missingTitle.ThreatTypes[0].ShortTitle = null;
            MtmtRulePackCompilation partial = MtmtRulePackCompiler.Compile(Save(missingTitle), "title.tb7");
            Assert.AreEqual(0, partial.EmittedRuleCount);
            Assert.AreEqual(1, partial.ErrorCount);
            StringAssert.Contains(partial.Diagnostics.Single().Message, "ShortTitle");
        }

        private static int CountValue(JsonElement rules, string property, string expected)
        {
            return rules.EnumerateArray().Count(rule =>
                rule.TryGetProperty(property, out JsonElement value) &&
                string.Equals(value.GetString(), expected, StringComparison.Ordinal));
        }

        private static byte[] Decompress(string path)
        {
            using FileStream compressed = File.OpenRead(path);
            using GZipStream gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using MemoryStream content = new MemoryStream();
            gzip.CopyTo(content);
            return content.ToArray();
        }

        private static byte[] Save(KnowledgeBaseData knowledgeBase)
        {
            using MemoryStream stream = new MemoryStream();
            knowledgeBase.Save(stream);
            return stream.ToArray();
        }

        private static KnowledgeBaseData CreateKnowledgeBase()
        {
            KnowledgeBaseData knowledgeBase = new KnowledgeBaseData
            {
                Manifest = new Manifest
                {
                    Id = new Guid("11111111-1111-1111-1111-111111111111"),
                    Name = "Medical Template",
                    Version = "1.0",
                    Author = "Microsoft",
                },
            };
            ThreatMetaData globalMetadata = new ThreatMetaData();
            ThreatMetaDatum priority = new ThreatMetaDatum { Id = "priority", Name = "Priority", Label = "Priority" };
            priority.Values.Add("High");
            priority.Values.Add("Medium");
            priority.Values.Add("Low");
            globalMetadata.PropertiesMetaData.Add(priority);
            knowledgeBase.ThreatMetaData = globalMetadata;
            knowledgeBase.ThreatCategories.Add(new ThreatCategory
            {
                Id = "privacy",
                Name = "Privacy",
                ShortDescription = "Patient privacy",
                LongDescription = "Privacy harm.",
            });
            knowledgeBase.GenericElements.Add(new ElementType { Id = "GE.P", Name = "Process", ParentId = "ROOT" });
            knowledgeBase.GenericElements.Add(new ElementType { Id = "GE.TB.B", Name = "Boundary", ParentId = "ROOT" });
            ElementType store = new ElementType { Id = "GE.DS", Name = "Data store", ParentId = "ROOT" };
            KnowledgeBaseAttribute reviewed = new KnowledgeBaseAttribute
            {
                Id = "attribute-id",
                Name = "attribute-internal-name",
                DisplayName = "Privacy Reviewed",
                Mode = AttributeMode.Static,
            };
            reviewed.AttributeValues.Add("No");
            reviewed.AttributeValues.Add("Yes");
            store.Attributes.Add(reviewed);
            knowledgeBase.GenericElements.Add(store);
            knowledgeBase.ThreatTypes.Add(new ThreatType
            {
                Id = "PRIV-1",
                Category = "privacy",
                ShortTitle = "Privacy exposure in {target.Name}",
                Description = "Patient data has not been reviewed.",
                GenerationFilters = new GenerationFilters
                {
                    Include = "source is 'GE.P' and target.attribute-internal-name is 'No'",
                    Exclude = "flow crosses 'GE.TB.B'",
                },
            });
            return knowledgeBase;
        }

        private static ThreatModel CreateMatchingModel()
        {
            StencilEllipse process = new StencilEllipse { Guid = Guid.NewGuid(), GenericTypeId = "GE.P" };
            process.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = "Service" });
            StencilParallelLines store = new StencilParallelLines { Guid = Guid.NewGuid(), GenericTypeId = "GE.DS" };
            store.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = "Records" });
            store.Properties.Add(new CustomStringDisplayAttribute { Value = "Privacy Reviewed:No" });
            Connector flow = new Connector
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = "GE.DF",
                SourceGuid = process.Guid,
                TargetGuid = store.Guid,
            };
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "DFD" };
            diagram.Borders.Add(process.Guid, process);
            diagram.Borders.Add(store.Guid, store);
            diagram.Lines.Add(flow.Guid, flow);
            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(diagram);
            return model;
        }
    }
}
