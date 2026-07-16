namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Xml;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Compiles an MTMT <c>.tb7</c> knowledge base into a deterministic version 2 declarative rule
    /// pack. Source filters are parsed by the existing interaction-expression engine; no second
    /// threat-detection implementation is introduced.
    /// </summary>
    public static class MtmtRulePackCompiler
    {
        /// <summary>The maximum accepted source-template size.</summary>
        public const int MaxSourceBytes = 32 * 1024 * 1024;

        private const string Dialect = "urn:tmforge:rules:interaction-v1";
        private const string FilterLanguage = "urn:tmforge:source:mtmt-generation-filter";
        private const string MetadataLanguage = "urn:tmforge:source:mtmt-threat-metadata";
        private const string MetadataPrefix = "urn:tmforge:source:mtmt-tb7:";
        private const string SourceType = "urn:tmforge:source:mtmt-tb7";
        private const int MaxDiagnosticCount = 256;
        private const int MaxDiagnosticText = 4096;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 256,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        /// <summary>Parses and compiles one MTMT template to deterministic UTF-8 rule-pack JSON.</summary>
        /// <param name="sourceContent">The exact source bytes used for parsing, identity, and fingerprinting.</param>
        /// <param name="sourceName">The source file name recorded in pack provenance.</param>
        /// <param name="packId">An optional stable pack-id override.</param>
        /// <returns>The compiled pack and translation summary.</returns>
        public static MtmtRulePackCompilation Compile(
            byte[] sourceContent,
            string sourceName,
            string? packId = null)
        {
            _ = sourceContent ?? throw new ArgumentNullException(nameof(sourceContent));
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                throw new ArgumentException("A source name is required.", nameof(sourceName));
            }

            if (sourceContent.Length > MaxSourceBytes)
            {
                throw new InvalidDataException(
                    $"MTMT template exceeds the limit of {MaxSourceBytes} bytes.");
            }

            KnowledgeBaseData knowledgeBase = LoadKnowledgeBase(sourceContent);
            int elementTypeCount = knowledgeBase.GenericElements.Count + knowledgeBase.StandardElements.Count;
            long attributeCount = knowledgeBase.GenericElements
                .Concat(knowledgeBase.StandardElements)
                .Sum(element => (long)element.Attributes.Count);
            if (knowledgeBase.ThreatTypes.Count > 4096 ||
                knowledgeBase.ThreatCategories.Count > 512 ||
                elementTypeCount > 4096 ||
                attributeCount > 65536)
            {
                throw new InvalidDataException("MTMT template catalog exceeds the rule-pack resource limits.");
            }

            string packName = FirstNonEmpty(knowledgeBase.Manifest?.Name, sourceName);
            string effectivePackId = string.IsNullOrWhiteSpace(packId)
                ? RulePackIdentity.CreatePackId(packName, sourceContent)
                : packId!;
            _ = RulePackIdentity.CreateEffectiveRuleId(effectivePackId, "validation");

            List<ElementType> elementTypes = knowledgeBase.GenericElements
                .Concat(knowledgeBase.StandardElements)
                .Where(element => !string.IsNullOrWhiteSpace(element.Id))
                .OrderBy(element => element.Id, StringComparer.Ordinal)
                .ToList();
            MtmtGenerationFilterCatalog catalog = new MtmtGenerationFilterCatalog(knowledgeBase);
            MtmtGenerationFilterParser parser = new MtmtGenerationFilterParser(catalog);
            List<MtmtRulePackDiagnostic> diagnostics = new List<MtmtRulePackDiagnostic>();
            List<DeclarativeRuleSpec> rules = new List<DeclarativeRuleSpec>();
            Dictionary<string, int> categoryDistribution = new Dictionary<string, int>(StringComparer.Ordinal);
            HashSet<string> unsupportedMetadata = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int errorCount = 0;
            string? duplicateThreatId = knowledgeBase.ThreatTypes
                .Where(threat => !string.IsNullOrWhiteSpace(threat.Id))
                .GroupBy(threat => threat.Id!, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(id => id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (duplicateThreatId != null)
            {
                throw new InvalidDataException(
                    $"MTMT template contains duplicate threat id '{duplicateThreatId}'.");
            }

            foreach (var item in knowledgeBase.ThreatTypes.Select((threat, index) => new { Threat = threat, Index = index })
                .OrderBy(item => item.Threat.Id, StringComparer.Ordinal))
            {
                ThreatType threat = item.Threat;
                string sourceId = threat.Id ?? "(missing id)";
                try
                {
                    DeclarativeRuleSpec rule = CompileRule(threat, item.Index, parser, unsupportedMetadata);
                    rules.Add(rule);
                    string categoryId = rule.CategoryId!;
                    categoryDistribution[categoryId] = categoryDistribution.TryGetValue(categoryId, out int count)
                        ? count + 1
                        : 1;
                }
                catch (Exception ex) when (ex is ArgumentException || ex is FormatException ||
                    ex is InvalidDataException || ex is InvalidOperationException)
                {
                    errorCount++;
                    AddDiagnostic(diagnostics, new MtmtRulePackDiagnostic
                    {
                        SourceThreatId = Truncate(sourceId),
                        Message = Truncate(ex.Message),
                        SourceExpression = TruncateOptional(SourceExpressionText(threat)),
                        IsError = true,
                    });
                }
            }

            int warningCount = 0;
            foreach (string metadataName in unsupportedMetadata.OrderBy(name => name, StringComparer.Ordinal))
            {
                warningCount++;
                AddDiagnostic(diagnostics, new MtmtRulePackDiagnostic
                {
                    Message = Truncate($"Preserved unsupported MTMT metadata '{metadataName}' in rule provenance."),
                    IsError = false,
                });
            }

            if (diagnostics.Count == MaxDiagnosticCount && errorCount + warningCount > MaxDiagnosticCount)
            {
                diagnostics[MaxDiagnosticCount - 1] = new MtmtRulePackDiagnostic
                {
                    Message = $"Additional diagnostics omitted; total errors={errorCount}, warnings={warningCount}.",
                    IsError = false,
                };
            }

            DeclarativeRuleFile document = new DeclarativeRuleFile
            {
                Schema = "tmforge-rules",
                Version = 2,
                Dialect = Dialect,
                Pack = BuildPack(knowledgeBase, sourceName, sourceContent, effectivePackId, packName),
                Categories = knowledgeBase.ThreatCategories
                    .Where(category => !string.IsNullOrWhiteSpace(category.Id))
                    .OrderBy(category => category.Id, StringComparer.Ordinal)
                    .Select(category => new DeclarativeRuleFile.CategorySpec
                    {
                        Id = category.Id,
                        Name = FirstNonEmpty(category.Name, category.Id!),
                        ShortDescription = category.ShortDescription,
                        LongDescription = category.LongDescription,
                    })
                    .ToList(),
                ElementTypes = elementTypes.Select(element => new DeclarativeRuleFile.ElementTypeSpec
                {
                    Id = element.Id,
                    Name = FirstNonEmpty(element.Name, element.Id!),
                    ParentId = string.IsNullOrWhiteSpace(element.ParentId) ? null : element.ParentId,
                }).ToList(),
                Properties = BuildProperties(elementTypes),
                Rules = rules,
            };

            string? validationError = DeclarativeRuleProvider.ValidateCompiledPack(
                Dialect,
                document.Pack,
                document.Categories,
                document.ElementTypes,
                document.Properties,
                document.Rules);
            if (validationError != null)
            {
                throw new InvalidDataException("Compiled rule pack is invalid: " + validationError);
            }

            byte[] content = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
            if (content.Length > DeclarativeRuleProvider.MaxRuleFileBytes)
            {
                throw new InvalidDataException(
                    $"Compiled rule pack exceeds the limit of {DeclarativeRuleProvider.MaxRuleFileBytes} bytes.");
            }

            return new MtmtRulePackCompilation(
                content,
                effectivePackId,
                packName,
                knowledgeBase.ThreatTypes.Count,
                rules.Count,
                new SortedDictionary<string, int>(categoryDistribution, StringComparer.Ordinal),
                diagnostics.AsReadOnly(),
                warningCount,
                errorCount);
        }

        private static void AddDiagnostic(
            ICollection<MtmtRulePackDiagnostic> diagnostics,
            MtmtRulePackDiagnostic diagnostic)
        {
            if (diagnostics.Count < MaxDiagnosticCount)
            {
                diagnostics.Add(diagnostic);
            }
        }

        private static string Truncate(string value)
        {
            return value.Length <= MaxDiagnosticText
                ? value
                : value.Substring(0, MaxDiagnosticText) + "...";
        }

        private static string? TruncateOptional(string? value)
        {
            return value == null ? null : Truncate(value);
        }

        private static KnowledgeBaseData LoadKnowledgeBase(byte[] sourceContent)
        {
            try
            {
                using MemoryStream stream = new MemoryStream(sourceContent, writable: false);
                return KnowledgeBaseData.Load(stream);
            }
            catch (Exception ex) when (ex is XmlException || ex is InvalidOperationException ||
                ex is FormatException || ex is ArgumentException || ex is DecoderFallbackException)
            {
                throw new InvalidDataException("Invalid MTMT template: " + ex.Message, ex);
            }
        }

        private static DeclarativeRuleFile.PackSpec BuildPack(
            KnowledgeBaseData knowledgeBase,
            string sourceName,
            byte[] sourceContent,
            string packId,
            string packName)
        {
            Manifest? manifest = knowledgeBase.Manifest;
            return new DeclarativeRuleFile.PackSpec
            {
                Id = packId,
                Name = packName,
                Version = NullIfWhiteSpace(manifest?.Version),
                Source = new DeclarativeRuleFile.SourceSpec
                {
                    Type = SourceType,
                    Name = sourceName,
                    Id = manifest?.Id == Guid.Empty ? null : manifest?.Id.ToString(),
                    Version = NullIfWhiteSpace(manifest?.Version),
                    Fingerprint = RulePackIdentity.CreateFingerprint(sourceContent),
                    Metadata = BuildSourceMetadata(knowledgeBase),
                },
            };
        }

        private static List<DeclarativeRuleFile.SourceMetadataSpec> BuildSourceMetadata(
            KnowledgeBaseData knowledgeBase)
        {
            List<DeclarativeRuleFile.SourceMetadataSpec> metadata = new List<DeclarativeRuleFile.SourceMetadataSpec>();
            string? author = knowledgeBase.Manifest?.Author;
            if (!string.IsNullOrWhiteSpace(author))
            {
                metadata.Add(new DeclarativeRuleFile.SourceMetadataSpec
                {
                    Key = MetadataPrefix + "manifest-author",
                    Value = author,
                });
            }

            IReadOnlyList<ThreatMetaDatum> globalMetadata =
                knowledgeBase.ThreatMetaData?.PropertiesMetaData ?? new List<ThreatMetaDatum>();
            for (int index = 0; index < globalMetadata.Count; index++)
            {
                ThreatMetaDatum datum = globalMetadata[index];
                metadata.Add(new DeclarativeRuleFile.SourceMetadataSpec
                {
                    Key = MetadataPrefix + "threat-metadata:" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture),
                    Value = SerializeMetadata(datum),
                });
            }

            return metadata;
        }

        private static List<DeclarativeRuleFile.PropertySpec> BuildProperties(
            IReadOnlyList<ElementType> elementTypes)
        {
            var bindings = elementTypes
                .SelectMany(element => element.Attributes.Select(attribute => new { Element = element, Attribute = attribute }))
                .Where(item => !string.IsNullOrWhiteSpace(item.Attribute.DisplayName) ||
                    !string.IsNullOrWhiteSpace(item.Attribute.Name) ||
                    !string.IsNullOrWhiteSpace(item.Attribute.Id))
                .Select(item => new
                {
                    item.Element,
                    item.Attribute,
                    Canonical = FirstNonEmpty(item.Attribute.DisplayName, item.Attribute.Name, item.Attribute.Id!),
                })
                .ToList();
            Dictionary<string, HashSet<string>> aliasOwners = new Dictionary<string, HashSet<string>>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var binding in bindings)
            {
                foreach (string? token in new[]
                {
                    binding.Attribute.DisplayName,
                    binding.Attribute.Name,
                    binding.Attribute.Id,
                })
                {
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        continue;
                    }

                    if (!aliasOwners.TryGetValue(token!, out HashSet<string>? owners))
                    {
                        owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        aliasOwners[token!] = owners;
                    }

                    owners.Add(binding.Canonical);
                }
            }

            return bindings
                .GroupBy(item => item.Canonical, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new DeclarativeRuleFile.PropertySpec
                {
                    Name = group.Key,
                    Aliases = group.SelectMany(item => new[]
                    {
                        item.Attribute.Name,
                        item.Attribute.Id,
                    })
                        .Where(alias => !string.IsNullOrWhiteSpace(alias) &&
                            aliasOwners[alias!].Count == 1 &&
                            !string.Equals(alias, group.Key, StringComparison.OrdinalIgnoreCase))
                        .Select(alias => alias!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(alias => alias, StringComparer.Ordinal)
                        .ToList(),
                    AllowedValues = group.Any(item => item.Attribute.Mode == AttributeMode.Dynamic)
                        ? new List<string>()
                        : group.SelectMany(item => item.Attribute.AttributeValues)
                            .Where(value => value != null)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .ToList(),
                    ElementTypeIds = group.Select(item => item.Element.Id!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .ToList(),
                })
                .ToList();
        }

        private static DeclarativeRuleSpec CompileRule(
            ThreatType threat,
            int sourceIndex,
            MtmtGenerationFilterParser parser,
            ISet<string> unsupportedMetadata)
        {
            if (!RulePackIdentity.IsValidSegment(threat.Id))
            {
                throw new FormatException($"Threat id '{threat.Id}' is not a valid rule identity segment.");
            }

            if (!RulePackIdentity.IsValidSegment(threat.Category))
            {
                throw new FormatException($"Threat '{threat.Id}' has no valid category id.");
            }

            if (string.IsNullOrWhiteSpace(threat.GenerationFilters.Include))
            {
                throw new FormatException($"Threat '{threat.Id}' has no Include generation filter.");
            }

            if (string.IsNullOrWhiteSpace(threat.ShortTitle))
            {
                throw new FormatException($"Threat '{threat.Id}' has no ShortTitle.");
            }

            InteractionExpression expression = parser.Compile(
                threat.GenerationFilters.Include!,
                threat.GenerationFilters.Exclude);
            List<DeclarativeRuleSpec.SourceExpressionSpec> sourceExpressions = new List<DeclarativeRuleSpec.SourceExpressionSpec>
            {
                SourceExpression("include", threat.GenerationFilters.Include!),
            };
            if (!string.IsNullOrWhiteSpace(threat.GenerationFilters.Exclude))
            {
                sourceExpressions.Add(SourceExpression("exclude", threat.GenerationFilters.Exclude!));
            }

            foreach (ThreatMetaDatum metadata in threat.PropertiesMetaData)
            {
                sourceExpressions.Add(SourceExpression("metadata", SerializeMetadata(metadata), MetadataLanguage));
                if (!IsMappedMetadata(metadata.Name))
                {
                    unsupportedMetadata.Add(FirstNonEmpty(metadata.Name, "(unnamed)"));
                }
            }

            if (!string.IsNullOrWhiteSpace(threat.RelatedCategory))
            {
                sourceExpressions.Add(SourceExpression("related-category", threat.RelatedCategory!, MetadataLanguage));
            }

            return new DeclarativeRuleSpec
            {
                Id = threat.Id,
                Severity = "warning",
                Message = threat.ShortTitle,
                FullDescription = threat.Description,
                HelpText = MetadataValue(threat, "PossibleMitigations", allowEmpty: true),
                CategoryId = threat.Category,
                Stride = ToStride(threat.Category),
                DefaultPriority = MetadataValue(threat, "Priority", allowEmpty: false),
                Expression = ToSpec(expression),
                Provenance = new DeclarativeRuleSpec.RuleProvenanceSpec
                {
                    SourceId = threat.Id,
                    CategoryId = threat.Category,
                    Location = $"ThreatTypes/{sourceIndex}",
                    Expressions = sourceExpressions,
                },
            };
        }

        private static string? ToStride(string? categoryId)
        {
            return categoryId switch
            {
                "S" => StrideCategory.Spoofing.ToString(),
                "T" => StrideCategory.Tampering.ToString(),
                "R" => StrideCategory.Repudiation.ToString(),
                "I" => StrideCategory.InformationDisclosure.ToString(),
                "D" => StrideCategory.DenialOfService.ToString(),
                "E" => StrideCategory.ElevationOfPrivilege.ToString(),
                _ => null,
            };
        }

        private static DeclarativeRuleSpec.SourceExpressionSpec SourceExpression(
            string role,
            string text,
            string language = FilterLanguage)
        {
            return new DeclarativeRuleSpec.SourceExpressionSpec
            {
                Role = role,
                Language = language,
                Text = text,
            };
        }

        private static bool IsMappedMetadata(string? name)
        {
            return string.Equals(name, "Priority", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "PossibleMitigations", StringComparison.OrdinalIgnoreCase);
        }

        private static string? MetadataValue(ThreatType threat, string name, bool allowEmpty)
        {
            List<string> values = threat.PropertiesMetaData
                .Where(metadata => string.Equals(metadata.Name, name, StringComparison.OrdinalIgnoreCase))
                .SelectMany(metadata => metadata.Values)
                .Where(value => value != null && (allowEmpty || !string.IsNullOrWhiteSpace(value)))
                .Select(value => value ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (values.Count == 0 || (allowEmpty && values.All(string.IsNullOrWhiteSpace)))
            {
                return null;
            }

            if (values.Count != 1)
            {
                throw new FormatException(
                    $"Threat '{threat.Id}' metadata '{name}' has {values.Count} values and cannot be represented exactly.");
            }

            string value = values[0];
            if (string.Equals(name, "Priority", StringComparison.OrdinalIgnoreCase) &&
                !Enum.GetNames(typeof(ThreatPriority)).Contains(value, StringComparer.Ordinal))
            {
                throw new FormatException($"Threat '{threat.Id}' has unknown priority '{value}'.");
            }

            return value;
        }

        private static string SerializeMetadata(ThreatMetaDatum metadata)
        {
            return JsonSerializer.Serialize(
                new
                {
                    metadata.Name,
                    metadata.Label,
                    metadata.Description,
                    metadata.HideFromUI,
                    values = metadata.Values,
                    metadata.Id,
                    metadata.AttributeType,
                },
                JsonOptions);
        }

        private static string? SourceExpressionText(ThreatType threat)
        {
            if (string.IsNullOrWhiteSpace(threat.GenerationFilters.Exclude))
            {
                return threat.GenerationFilters.Include;
            }

            return threat.GenerationFilters.Include + " AND NOT (" + threat.GenerationFilters.Exclude + ")";
        }

        private static DeclarativeRuleSpec.InteractionExpressionSpec ToSpec(InteractionExpression expression)
        {
            return expression.Operation switch
            {
                InteractionExpression.OperationKind.All => new DeclarativeRuleSpec.InteractionExpressionSpec
                {
                    AllOf = expression.Children.Select(ToSpec).ToList(),
                },
                InteractionExpression.OperationKind.Any => new DeclarativeRuleSpec.InteractionExpressionSpec
                {
                    AnyOf = expression.Children.Select(ToSpec).ToList(),
                },
                InteractionExpression.OperationKind.Not => new DeclarativeRuleSpec.InteractionExpressionSpec
                {
                    Not = ToSpec(expression.Child!),
                },
                InteractionExpression.OperationKind.Type => new DeclarativeRuleSpec.InteractionExpressionSpec
                {
                    Subject = expression.Subject,
                    Type = expression.Type,
                },
                InteractionExpression.OperationKind.Property => new DeclarativeRuleSpec.InteractionExpressionSpec
                {
                    Subject = expression.Subject,
                    Property = expression.Property,
                    ValueIn = expression.Values.ToList(),
                },
                InteractionExpression.OperationKind.Crosses => new DeclarativeRuleSpec.InteractionExpressionSpec
                {
                    Crosses = expression.BoundaryType,
                },
                _ => throw new InvalidOperationException(
                    $"MTMT import cannot serialize interaction operation '{expression.Operation}'."),
            };
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            string? value = values.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
            return value ?? throw new InvalidOperationException("At least one non-empty value is required.");
        }

        private static string? NullIfWhiteSpace(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
