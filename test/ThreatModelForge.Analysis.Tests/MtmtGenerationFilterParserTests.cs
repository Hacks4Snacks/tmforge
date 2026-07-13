namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;

    /// <summary>
    /// Tests the MTMT GenerationFilters parser against representative syntax and the pinned corpus.
    /// </summary>
    [TestClass]
    public class MtmtGenerationFilterParserTests
    {
        private const string FixtureDirectory = "Fixtures/OfficialTb7";

        /// <summary>Logical precedence and quoted values survive canonical parser round-trips.</summary>
        [TestMethod]
        public void PreservesPrecedenceAndQuotedValues()
        {
            MtmtGenerationFilterParser parser = new MtmtGenerationFilterParser();
            const string source =
                "source is 'GE.P' or target is 'GE.P' and not flow.Protocol is 'Not Selected'";

            string canonical = MtmtGenerationFilterFormatter.Format(parser.Parse(source));
            string reparsed = MtmtGenerationFilterFormatter.Format(parser.Parse(canonical));

            Assert.AreEqual(
                "(source is 'GE.P' or (target is 'GE.P' and not flow.Protocol is 'Not Selected'))",
                canonical);
            Assert.AreEqual(canonical, reparsed);
        }

        /// <summary>Canonical formatting preserves literal backslashes and apostrophes.</summary>
        [TestMethod]
        public void PreservesEscapedQuotedValues()
        {
            MtmtGenerationFilterParser parser = new MtmtGenerationFilterParser();
            const string source = "flow.Path is 'C:\\\\temp\\\\O''Brien\\\\'";

            InteractionExpression parsed = parser.Parse(source);
            string canonical = MtmtGenerationFilterFormatter.Format(parsed);
            InteractionExpression reparsed = parser.Parse(canonical);

            Assert.AreEqual(parsed.Values.Single(), reparsed.Values.Single());
            Assert.AreEqual(canonical, MtmtGenerationFilterFormatter.Format(reparsed));
        }

        /// <summary>Include and Exclude compile to one firing expression with Exclude negated.</summary>
        [TestMethod]
        public void CompilesIncludeAndNotExclude()
        {
            MtmtGenerationFilterParser parser = new MtmtGenerationFilterParser();

            InteractionExpression expression = parser.Compile(
                "target is 'GE.P'",
                "flow.Protocol is 'HTTP'");

            Assert.AreEqual(
                "(target is 'GE.P' and not flow.Protocol is 'HTTP')",
                MtmtGenerationFilterFormatter.Format(expression));
        }

        /// <summary>Include/Exclude composition enforces limits on the final evaluator tree.</summary>
        [TestMethod]
        public void ValidatesComposedExpressionLimits()
        {
            MtmtGenerationFilterParser parser = new MtmtGenerationFilterParser();

            _ = parser.Compile("source is 'GE.P'", Negated("target is 'GE.P'", 61));
            InvalidDataException depth = Assert.Throws<InvalidDataException>(() =>
                parser.Compile("source is 'GE.P'", Negated("target is 'GE.P'", 62)));

            string manyPredicates = string.Join(
                " or ",
                Enumerable.Repeat("not flow is 'GE.DF'", 2600));
            InvalidDataException nodes = Assert.Throws<InvalidDataException>(() =>
                parser.Compile(manyPredicates, manyPredicates));

            StringAssert.Contains(depth.Message, "depth limit of 64");
            StringAssert.Contains(nodes.Message, "node limit of 10000");
        }

        /// <summary>ROOT is reserved for source type predicates in parser output.</summary>
        [TestMethod]
        public void RejectsInvalidRootSubject()
        {
            MtmtGenerationFilterParser parser = new MtmtGenerationFilterParser();

            _ = parser.Parse("source is 'ROOT'");
            FormatException error = Assert.Throws<FormatException>(() => parser.Parse("target is 'ROOT'"));

            StringAssert.Contains(error.Message, "ROOT");
        }

        /// <summary>Static MTMT attributes reject unknown values while dynamic attributes remain open.</summary>
        [TestMethod]
        public void ValidatesStaticAttributeValues()
        {
            KnowledgeBaseAttribute protocol = new KnowledgeBaseAttribute
            {
                DisplayName = "Protocol",
                Mode = AttributeMode.Static,
            };
            protocol.AttributeValues.Add("HTTP");
            protocol.AttributeValues.Add("TLS");
            KnowledgeBaseAttribute label = new KnowledgeBaseAttribute
            {
                DisplayName = "Label",
                Mode = AttributeMode.Dynamic,
            };
            ElementType flow = new ElementType { Id = "GE.DF", Name = "Data Flow" };
            flow.Attributes.Add(protocol);
            flow.Attributes.Add(label);
            KnowledgeBaseData knowledgeBase = new KnowledgeBaseData();
            knowledgeBase.GenericElements.Add(flow);
            MtmtGenerationFilterParser parser = new MtmtGenerationFilterParser(
                new MtmtGenerationFilterCatalog(knowledgeBase));

            _ = parser.Parse("flow.Protocol is 'HTTP'");
            _ = parser.Parse("flow.Label is 'runtime-defined'");
            FormatException error = Assert.Throws<FormatException>(() =>
                parser.Parse("flow.Protocol is 'SMTP'"));

            StringAssert.Contains(error.Message, "does not allow value 'SMTP'");
        }

        /// <summary>
        /// Every official expression parses without fallback, round-trips canonically, resolves all
        /// referenced types/properties, and compiles each threat's Include/Exclude pair.
        /// </summary>
        [TestMethod]
        public void ParsesAndResolvesOfficialCorpus()
        {
            string[] fixtureNames =
            {
                "default.tb7.gz",
                "Azure Cloud Services.tb7.gz",
                "MedicalDeviceTemplate.tb7.gz",
            };
            int expressionCount = 0;
            int threatCount = 0;

            foreach (string fixtureName in fixtureNames)
            {
                KnowledgeBaseData knowledgeBase = LoadFixture(fixtureName);
                MtmtGenerationFilterParser syntaxParser = new MtmtGenerationFilterParser();
                MtmtGenerationFilterParser resolvingParser = new MtmtGenerationFilterParser(
                    new MtmtGenerationFilterCatalog(knowledgeBase));

                foreach (ThreatType threat in knowledgeBase.ThreatTypes)
                {
                    threatCount++;
                    List<string> expressions = new List<string>();
                    if (!string.IsNullOrWhiteSpace(threat.GenerationFilters.Include))
                    {
                        expressions.Add(threat.GenerationFilters.Include);
                    }

                    if (!string.IsNullOrWhiteSpace(threat.GenerationFilters.Exclude))
                    {
                        expressions.Add(threat.GenerationFilters.Exclude);
                    }

                    foreach (string source in expressions)
                    {
                        expressionCount++;
                        string canonical = MtmtGenerationFilterFormatter.Format(syntaxParser.Parse(source));
                        Assert.AreEqual(
                            canonical,
                            MtmtGenerationFilterFormatter.Format(syntaxParser.Parse(canonical)),
                            $"Canonical round-trip failed for {fixtureName} threat {threat.Id}.");
                        _ = resolvingParser.Parse(source);
                    }

                    Assert.IsFalse(string.IsNullOrWhiteSpace(threat.GenerationFilters.Include));
                    _ = resolvingParser.Compile(
                        threat.GenerationFilters.Include,
                        threat.GenerationFilters.Exclude);
                }
            }

            Assert.AreEqual(363, threatCount);
            Assert.AreEqual(431, expressionCount);
        }

        private static KnowledgeBaseData LoadFixture(string fileName)
        {
            string path = Path.Join(AppContext.BaseDirectory, FixtureDirectory, fileName);
            using FileStream compressed = File.OpenRead(path);
            using GZipStream gzip = new GZipStream(compressed, CompressionMode.Decompress);
            return KnowledgeBaseData.Load(gzip);
        }

        private static string Negated(string expression, int count)
        {
            return string.Concat(Enumerable.Repeat("not ", count)) + expression;
        }
    }
}
