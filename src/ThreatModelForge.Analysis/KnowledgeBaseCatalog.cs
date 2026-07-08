namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using ThreatModelForge.KnowledgeBase;

    /// <summary>
    /// Builds Threat Model Forge's own, clean-room knowledge base: the default embedded when a model
    /// is exported to <c>.tm7</c> without one. It declares the standard generic element types (each
    /// drawn from its <see cref="ElementVisualRepresentation"/> and given a small, original stencil
    /// icon), the six STRIDE categories, and one threat type per threat-bearing analysis rule so the
    /// register that Threat Model Forge writes has matching type metadata in the Microsoft Threat
    /// Modeling Tool.
    /// </summary>
    public static class KnowledgeBaseCatalog
    {
        /// <summary>The manifest name shown for the bundled knowledge base.</summary>
        public const string DefaultName = "Threat Model Forge Core";

        private static readonly Guid DefaultId = new Guid("7e3f1d52-0000-4000-8000-746d666f7267");

        /// <summary>
        /// Builds the default knowledge base using the built-in analysis rule set for its threat types.
        /// </summary>
        /// <returns>A newly constructed <see cref="KnowledgeBaseData"/>.</returns>
        public static KnowledgeBaseData CreateDefault()
        {
            using (RuleSet ruleSet = LoadDefaultRuleSet())
            {
                return CreateDefault(ruleSet);
            }
        }

        /// <summary>
        /// Builds the default knowledge base, deriving one threat type per threat-bearing rule in
        /// <paramref name="ruleSet"/>.
        /// </summary>
        /// <param name="ruleSet">The rule set whose threat-bearing rules become threat types.</param>
        /// <returns>A newly constructed <see cref="KnowledgeBaseData"/>.</returns>
        public static KnowledgeBaseData CreateDefault(RuleSet ruleSet)
        {
            if (ruleSet == null)
            {
                throw new ArgumentNullException(nameof(ruleSet));
            }

            KnowledgeBaseData knowledgeBase = new KnowledgeBaseData
            {
                Manifest = new Manifest
                {
                    Name = DefaultName,
                    Id = DefaultId,
                    Version = "1.0.0",
                    Author = "Threat Model Forge",
                },
            };

            AddGenericElements(knowledgeBase.GenericElements);
            AddThreatCategories(knowledgeBase.ThreatCategories);
            AddThreatTypes(knowledgeBase.ThreatTypes, ruleSet);

            return knowledgeBase;
        }

        private static void AddGenericElements(List<ElementType> elements)
        {
            elements.Add(GenericElement("GE.P", "Generic Process", "A representation of a generic process.", ElementVisualRepresentation.Ellipse, "Centered on stencil", "process"));
            elements.Add(GenericElement("GE.EI", "Generic External Interactor", "A representation of a generic external interactor.", ElementVisualRepresentation.Rectangle, "Lower right of stencil", "external"));
            elements.Add(GenericElement("GE.DS", "Generic Data Store", "A representation of a generic data store.", ElementVisualRepresentation.ParallelLines, "Lower right of stencil", "datastore"));
            elements.Add(GenericElement("GE.DF", "Generic Data Flow", "A representation of a generic data flow.", ElementVisualRepresentation.Line, "Before label", "dataflow"));
            elements.Add(GenericElement("GE.TB.L", "Generic Trust Line Boundary", "A representation of a trust boundary drawn as a line.", ElementVisualRepresentation.LineBoundary, "Before label", "trustline"));
            elements.Add(GenericElement("GE.TB.B", "Generic Trust Border Boundary", "A representation of a trust boundary drawn as a border.", ElementVisualRepresentation.BorderBoundary, "Before label", "trustborder"));
            elements.Add(GenericElement("GE.A", "Generic Annotation", "A representation of a free-text annotation.", ElementVisualRepresentation.Annotation, "Before label", "annotation"));
        }

        private static ElementType GenericElement(string id, string name, string description, ElementVisualRepresentation representation, string imageLocation, string icon)
        {
            return new ElementType
            {
                Id = id,
                Name = name,
                Description = description,
                ParentId = "ROOT",
                Representation = representation,
                ImageLocation = imageLocation,
                ImageSource = LoadIcon(icon),
                Hidden = false,
            };
        }

        private static string LoadIcon(string name)
        {
            Assembly assembly = typeof(KnowledgeBaseCatalog).Assembly;
            string suffix = ".Stencils." + name + ".png";
            string? resource = null;
            foreach (string candidate in assembly.GetManifestResourceNames())
            {
                if (candidate.EndsWith(suffix, StringComparison.Ordinal))
                {
                    resource = candidate;
                    break;
                }
            }

            if (resource == null)
            {
                return string.Empty;
            }

            using (Stream? stream = assembly.GetManifestResourceStream(resource))
            {
                if (stream == null)
                {
                    return string.Empty;
                }

                using (MemoryStream buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    return Convert.ToBase64String(buffer.ToArray());
                }
            }
        }

        private static void AddThreatCategories(List<ThreatCategory> categories)
        {
            categories.Add(ThreatCategoryFor("S", "Spoofing", "Impersonating something or someone else."));
            categories.Add(ThreatCategoryFor("T", "Tampering", "Unauthorized modification of data or code."));
            categories.Add(ThreatCategoryFor("R", "Repudiation", "Denying an action without other parties being able to prove otherwise."));
            categories.Add(ThreatCategoryFor("I", "Information Disclosure", "Exposure of information to parties not authorized to see it."));
            categories.Add(ThreatCategoryFor("D", "Denial of Service", "Denying or degrading service to legitimate users."));
            categories.Add(ThreatCategoryFor("E", "Elevation of Privilege", "Gaining capabilities without proper authorization."));
        }

        private static ThreatCategory ThreatCategoryFor(string id, string name, string description)
        {
            return new ThreatCategory
            {
                Id = id,
                Name = name,
                ShortDescription = description,
                LongDescription = description,
            };
        }

        private static void AddThreatTypes(List<ThreatType> threatTypes, RuleSet ruleSet)
        {
            foreach (Rule rule in ruleSet.Rules)
            {
                if (rule.Stride is not StrideCategory category)
                {
                    continue;
                }

                string description = string.IsNullOrEmpty(rule.HelpText) ? rule.FullDescription : rule.HelpText;
                ThreatType threatType = new ThreatType
                {
                    Id = rule.ID,
                    ShortTitle = rule.FullDescription,
                    Category = CategoryLetter(category),
                    Description = description,
                };

                threatTypes.Add(threatType);
            }
        }

        private static string CategoryLetter(StrideCategory category)
        {
            return category switch
            {
                StrideCategory.Spoofing => "S",
                StrideCategory.Tampering => "T",
                StrideCategory.Repudiation => "R",
                StrideCategory.InformationDisclosure => "I",
                StrideCategory.DenialOfService => "D",
                StrideCategory.ElevationOfPrivilege => "E",
                _ => string.Empty,
            };
        }

        private static RuleSet LoadDefaultRuleSet()
        {
            Assembly rules = Assembly.Load("ThreatModelForge.Analysis.Rules");
            return RuleSet.LoadDefault(new[] { rules });
        }
    }
}
