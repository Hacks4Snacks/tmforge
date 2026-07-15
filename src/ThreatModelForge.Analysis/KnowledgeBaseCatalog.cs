namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using ThreatModelForge.Editing;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Builds Threat Model Forge's own, clean-room knowledge base: the default embedded when a model
    /// is exported to <c>.tm7</c> without one. It declares the generic element types (each drawn from
    /// its <see cref="ElementVisualRepresentation"/> and given a small, original stencil icon), a
    /// standard element type for every authoring stencil (so the Microsoft Threat Modeling Tool's
    /// palette mirrors Threat Model Forge's), the six STRIDE categories, and one threat type per
    /// threat-bearing analysis rule so the register that Threat Model Forge writes has matching type
    /// metadata in the tool.
    /// </summary>
    public static class KnowledgeBaseCatalog
    {
        /// <summary>The manifest name shown for the bundled knowledge base.</summary>
        public const string DefaultName = "Threat Model Forge Core";

        private static readonly Guid DefaultId = new Guid("7e3f1d52-0000-4000-8000-746d666f7267");

        // Maps a stencil's base primitive to the generic element type it derives from, plus the visual
        // representation, image placement, and icon that its standard element type inherits.
        private static readonly IReadOnlyDictionary<string, GenericMapping> GenericBases =
            new Dictionary<string, GenericMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["process"] = new GenericMapping("GE.P", ElementVisualRepresentation.Ellipse, "Centered on stencil", "process"),
                ["datastore"] = new GenericMapping("GE.DS", ElementVisualRepresentation.ParallelLines, "Lower right of stencil", "datastore"),
                ["external"] = new GenericMapping("GE.EI", ElementVisualRepresentation.Rectangle, "Lower right of stencil", "external"),
                ["dataflow"] = new GenericMapping("GE.DF", ElementVisualRepresentation.Line, "Before label", "dataflow"),
                ["boundary"] = new GenericMapping("GE.TB.B", ElementVisualRepresentation.BorderBoundary, "Before label", "trustborder"),
            };

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
            AddStandardElements(knowledgeBase.StandardElements);
            AddThreatMetaData(knowledgeBase, ruleSet);
            AddThreatCategories(knowledgeBase.ThreatCategories, ruleSet);
            AddThreatTypes(knowledgeBase.ThreatTypes, ruleSet);

            return knowledgeBase;
        }

        /// <summary>
        /// Determines whether a knowledge base is Threat Model Forge's own default (as opposed to a
        /// user-supplied or third-party one), by matching its manifest identifier. Used to decide when
        /// an export may safely rebuild and re-type the knowledge base versus leaving a foreign one
        /// untouched.
        /// </summary>
        /// <param name="knowledgeBase">The knowledge base to test.</param>
        /// <returns><see langword="true"/> when the knowledge base is the built-in default.</returns>
        public static bool IsDefault(KnowledgeBaseData knowledgeBase)
        {
            if (knowledgeBase == null)
            {
                throw new ArgumentNullException(nameof(knowledgeBase));
            }

            return knowledgeBase.Manifest != null && knowledgeBase.Manifest.Id == DefaultId;
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

        private static void AddStandardElements(List<ElementType> elements)
        {
            foreach (StencilDto stencil in StencilCatalog.All)
            {
                // The primitive stencils already exist as generic element types; only richer stencils
                // (Azure SQL, AKS, a human actor, and so on) become standard element subtypes.
                if (string.Equals(stencil.Id, stencil.Base, StringComparison.OrdinalIgnoreCase) ||
                    !GenericBases.TryGetValue(stencil.Base, out GenericMapping mapping))
                {
                    continue;
                }

                elements.Add(new ElementType
                {
                    Id = stencil.Id,
                    Name = stencil.Label,
                    Description = string.IsNullOrEmpty(stencil.Blurb) ? stencil.Label : stencil.Blurb,
                    ParentId = mapping.GenericId,
                    Representation = mapping.Representation,
                    ImageLocation = mapping.ImageLocation,
                    ImageSource = LoadIcon(mapping.Icon),
                    Hidden = false,
                });
            }
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
            string? resource = assembly.GetManifestResourceNames()
                .FirstOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));

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

        private static void AddThreatCategories(List<ThreatCategory> categories, RuleSet ruleSet)
        {
            categories.Add(ThreatCategoryFor("S", "Spoofing", "Impersonating something or someone else."));
            categories.Add(ThreatCategoryFor("T", "Tampering", "Unauthorized modification of data or code."));
            categories.Add(ThreatCategoryFor("R", "Repudiation", "Denying an action without other parties being able to prove otherwise."));
            categories.Add(ThreatCategoryFor("I", "Information Disclosure", "Exposure of information to parties not authorized to see it."));
            categories.Add(ThreatCategoryFor("D", "Denial of Service", "Denying or degrading service to legitimate users."));
            categories.Add(ThreatCategoryFor("E", "Elevation of Privilege", "Gaining capabilities without proper authorization."));

            HashSet<string> categoryIds = new HashSet<string>(
                categories.Select(category => category.Id!),
                StringComparer.OrdinalIgnoreCase);
            IEnumerable<RuleThreatCategory> declaredCategories = ruleSet.Rules
                .Select(rule => rule.PackDefinition)
                .Where(pack => pack != null)
                .Select(pack => pack!)
                .GroupBy(pack => pack.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .SelectMany(pack => pack.Categories.Select(category => RuleThreatCategory.FromPack(pack, category)));
            foreach (RuleThreatCategory category in declaredCategories
                .Concat(ruleSet.Rules.Select(rule => rule.ThreatCategory).Where(category => category != null).Select(category => category!))
                .GroupBy(category => category.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(category => category.Id, StringComparer.Ordinal))
            {
                if (!categoryIds.Add(category.Id))
                {
                    continue;
                }

                categories.Add(new ThreatCategory
                {
                    Id = category.Id,
                    Name = category.Name,
                    ShortDescription = FirstNonEmpty(category.ShortDescription, category.Name),
                    LongDescription = FirstNonEmpty(category.LongDescription, category.ShortDescription, category.Name),
                });
            }
        }

        private static void AddThreatMetaData(KnowledgeBaseData knowledgeBase, RuleSet ruleSet)
        {
            if (!ruleSet.Rules.Any(rule => rule.DefaultThreatPriority.HasValue))
            {
                return;
            }

            ThreatMetaData metadata = new ThreatMetaData { IsPriorityUsed = true };
            ThreatMetaDatum priority = new ThreatMetaDatum
            {
                Name = "Priority",
                Label = "Priority",
                Description = "Generated threat priority.",
                Id = "tmforge:priority",
                AttributeType = 1,
            };
            priority.Values.Add("High");
            priority.Values.Add("Medium");
            priority.Values.Add("Low");
            metadata.PropertiesMetaData.Add(priority);
            knowledgeBase.ThreatMetaData = metadata;
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
                if (rule.ThreatCategory is not RuleThreatCategory category)
                {
                    continue;
                }

                string description = string.IsNullOrEmpty(rule.HelpText) ? rule.FullDescription : rule.HelpText;
                ThreatType threatType = new ThreatType
                {
                    Id = rule.ID,
                    ShortTitle = rule.FullDescription,
                    Category = category.Id,
                    Description = description,
                };
                if (rule.DefaultThreatPriority.HasValue)
                {
                    ThreatMetaDatum priority = new ThreatMetaDatum
                    {
                        Name = "Priority",
                        Label = "Priority",
                        Description = "Default threat priority from the source rule.",
                        Id = "tmforge:priority",
                        AttributeType = 1,
                    };
                    priority.Values.Add(rule.DefaultThreatPriority.Value.ToString());
                    threatType.PropertiesMetaData.Add(priority);
                }

                threatTypes.Add(threatType);
            }
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value!;
                }
            }

            return string.Empty;
        }

        private static RuleSet LoadDefaultRuleSet()
        {
            return AnalysisRuleSources.Create();
        }

        private readonly struct GenericMapping
        {
            public GenericMapping(string genericId, ElementVisualRepresentation representation, string imageLocation, string icon)
            {
                this.GenericId = genericId;
                this.Representation = representation;
                this.ImageLocation = imageLocation;
                this.Icon = icon;
            }

            public string GenericId { get; }

            public ElementVisualRepresentation Representation { get; }

            public string ImageLocation { get; }

            public string Icon { get; }
        }
    }
}
