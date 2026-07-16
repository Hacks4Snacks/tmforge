namespace ThreatModelForge.Cli
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization;
    using System.Text.Json;
    using System.Xml;
    using ThreatModelForge.Analysis;

    /// <summary>Implements rule-pack operations, beginning with MTMT template import.</summary>
    internal static class RulesCommand
    {
        /// <summary>Runs the <c>tmforge rules</c> command.</summary>
        /// <param name="args">The command arguments after <c>rules</c>.</param>
        /// <returns>Zero on success; non-zero on invalid input or strict translation failure.</returns>
        public static int Run(string[] args)
        {
            args ??= Array.Empty<string>();
            bool jsonRequested = args.Any(argument => string.Equals(argument, "--json", StringComparison.Ordinal));
            string? incompleteOption = FindIncompleteValueOption(args);
            if (incompleteOption != null)
            {
                return Fail(jsonRequested, "Missing value for option: " + incompleteOption);
            }

            CliArgs parsed = CliArgs.Parse(
                args,
                new[] { "from", "out", "pack-id" },
                new[] { "strict" });
            if (parsed.Help)
            {
                PrintUsage();
                return 0;
            }

            if (parsed.UnknownFlags.Count > 0)
            {
                return Fail(parsed.Json, "Unknown option: " + parsed.UnknownFlags[0]);
            }

            if (parsed.Positionals.Count != 1 ||
                !string.Equals(parsed.Positionals[0], "import", StringComparison.OrdinalIgnoreCase))
            {
                return Fail(parsed.Json, "Expected the 'import' rules operation.");
            }

            string? input = parsed.Get("from");
            string? output = parsed.Get("out");
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
            {
                return Fail(parsed.Json, "rules import requires --from <template.tb7> and --out <pack.tmrules.json>.");
            }

            try
            {
                if (!File.Exists(input))
                {
                    return Fail(parsed.Json, "Template not found: " + input);
                }

                StringComparison pathComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal;
                string resolvedInput = ResolveExistingPath(input!);
                string resolvedOutput = File.Exists(output)
                    ? ResolveExistingPath(output!)
                    : ResolveExistingParent(output!);
                if (string.Equals(resolvedInput, resolvedOutput, pathComparison))
                {
                    return Fail(parsed.Json, "Input and output paths must be different.");
                }

                byte[] source = ReadSource(resolvedInput);
                MtmtRulePackCompilation compilation = MtmtRulePackCompiler.Compile(
                    source,
                    Path.GetFileName(input),
                    parsed.Get("pack-id"));
                bool strictFailure = parsed.HasFlag("strict") && compilation.ErrorCount > 0;
                if (!strictFailure)
                {
                    string revalidatedOutput = File.Exists(output)
                        ? ResolveExistingPath(output!)
                        : ResolveExistingParent(output!);
                    if (!string.Equals(resolvedOutput, revalidatedOutput, pathComparison))
                    {
                        throw new IOException("Output path changed during validation.");
                    }

                    WriteAtomically(resolvedOutput, compilation.Content);
                }

                if (parsed.Json)
                {
                    CliJson.WriteEnvelope("rules", new
                    {
                        operation = "import",
                        input,
                        output = strictFailure ? null : output,
                        strict = parsed.HasFlag("strict"),
                        status = strictFailure ? "failed" : compilation.ErrorCount > 0 ? "partial" : "success",
                        packId = compilation.PackId,
                        packName = compilation.PackName,
                        sourceCount = compilation.SourceThreatCount,
                        emittedCount = compilation.EmittedRuleCount,
                        skippedCount = compilation.SkippedThreatCount,
                        warningCount = compilation.WarningCount,
                        categoryDistribution = compilation.CategoryDistribution,
                        diagnostics = compilation.Diagnostics.Select(diagnostic => new
                        {
                            sourceThreatId = diagnostic.SourceThreatId,
                            diagnostic.Message,
                            sourceExpression = diagnostic.SourceExpression,
                            severity = diagnostic.IsError ? "error" : "warning",
                        }).ToList(),
                    });
                }
                else
                {
                    WriteSummary(compilation, output!, strictFailure);
                }

                return strictFailure ? 2 : 0;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                ex is SerializationException || ex is XmlException || ex is ArgumentException ||
                ex is FormatException || ex is InvalidDataException || ex is JsonException ||
                ex is InvalidOperationException || ex is NotSupportedException)
            {
                return Fail(parsed.Json, "Rule import failed: " + ex.Message);
            }
        }

        private static string? FindIncompleteValueOption(IReadOnlyList<string> args)
        {
            string[] options = new[] { "--from", "--out", "--pack-id" };
            for (int index = 0; index < args.Count; index++)
            {
                if (!options.Contains(args[index], StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index + 1 >= args.Count || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    return args[index];
                }

                index++;
            }

            return null;
        }

        private static int Fail(bool json, string message)
        {
            if (json)
            {
                CliJson.WriteEnvelope("rules", new
                {
                    operation = "import",
                    status = "failed",
                    error = message,
                });
            }
            else
            {
                Console.Error.WriteLine(message);
            }

            return 1;
        }

        private static byte[] ReadSource(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.Device)) != 0)
            {
                throw new InvalidDataException("MTMT template must be a regular file.");
            }

            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.CanSeek && stream.Length > MtmtRulePackCompiler.MaxSourceBytes)
            {
                throw new InvalidDataException(
                    $"MTMT template exceeds the limit of {MtmtRulePackCompiler.MaxSourceBytes} bytes.");
            }

            using MemoryStream content = new MemoryStream();
            byte[] buffer = new byte[81920];
            while (true)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                if (content.Length > MtmtRulePackCompiler.MaxSourceBytes - read)
                {
                    throw new InvalidDataException(
                        $"MTMT template exceeds the limit of {MtmtRulePackCompiler.MaxSourceBytes} bytes.");
                }

                content.Write(buffer, 0, read);
            }

            return content.ToArray();
        }

        private static string ResolveExistingPath(string path)
        {
            return ResolveExistingComponents(Path.GetFullPath(path));
        }

        private static string ResolveExistingParent(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
            {
                return fullPath;
            }

            string resolvedParent = ResolveExistingComponents(directory);
            return Path.Combine(resolvedParent, Path.GetFileName(fullPath));
        }

        private static string ResolveExistingComponents(string fullPath)
        {
            return ResolveExistingComponents(fullPath, depth: 0);
        }

        private static string ResolveExistingComponents(string fullPath, int depth)
        {
            if (depth > 64)
            {
                throw new IOException("Path contains too many symbolic links.");
            }

            string? root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root))
            {
                throw new IOException("Could not determine the filesystem root.");
            }

            string current = Path.GetFullPath(root!);
            string relative = Path.GetRelativePath(current, fullPath);
            foreach (string segment in relative.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries))
            {
                string next = Path.Combine(current, segment);
                FileSystemInfo? link = LinkAt(next);
                if (link != null)
                {
                    FileSystemInfo? target = link.ResolveLinkTarget(returnFinalTarget: true);
                    if (target == null)
                    {
                        throw new IOException("Path contains a dangling symbolic link: " + next);
                    }

                    current = ResolveExistingComponents(target.FullName, depth + 1);
                }
                else
                {
                    current = Path.GetFullPath(next);
                }
            }

            return current;
        }

        private static FileSystemInfo? LinkAt(string path)
        {
            DirectoryInfo directory = new DirectoryInfo(path);
            if (directory.LinkTarget != null)
            {
                return directory;
            }

            FileInfo file = new FileInfo(path);
            return file.LinkTarget == null ? null : file;
        }

        private static void WriteSummary(
            MtmtRulePackCompilation compilation,
            string output,
            bool strictFailure)
        {
            foreach (MtmtRulePackDiagnostic diagnostic in compilation.Diagnostics)
            {
                string source = string.IsNullOrEmpty(diagnostic.SourceThreatId)
                    ? string.Empty
                    : " [" + diagnostic.SourceThreatId + "]";
                Console.Error.WriteLine((diagnostic.IsError ? "error" : "warning") + source + ": " + diagnostic.Message);
                if (!string.IsNullOrWhiteSpace(diagnostic.SourceExpression))
                {
                    Console.Error.WriteLine("  expression: " + diagnostic.SourceExpression);
                }
            }

            Console.Error.WriteLine(
                "Imported " + compilation.EmittedRuleCount + " of " + compilation.SourceThreatCount +
                " threats from " + compilation.PackName + ".");
            Console.Error.WriteLine(
                "Warnings: " + compilation.WarningCount + "; skipped: " + compilation.SkippedThreatCount + ".");
            Console.Error.WriteLine(
                "Categories: " + string.Join(", ", compilation.CategoryDistribution.Select(pair => pair.Key + "=" + pair.Value)));
            Console.Error.WriteLine(strictFailure ? "Strict import failed; no output was written." : "Wrote " + output + ".");
        }

        private static void WriteAtomically(string path, byte[] content)
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath) ?? ".";
            string temporary = Path.Join(
                directory,
                "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllBytes(temporary, content);
                File.Move(temporary, fullPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Compile an MTMT template into a Threat Model Forge rule pack.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge rules import --from <template.tb7> --out <pack.tmrules.json> [--pack-id <id>] [--strict] [--json]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Without --strict, representable threats are written and skipped threats are reported.");
            Console.Error.WriteLine("With --strict, any skipped threat fails the import and no output is written.");
        }
    }
}
