namespace ThreatModelForge.Cli
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;

    /// <summary>
    /// Implements <c>tmforge git-setup</c>: registers the tmforge diff textconv and merge driver in
    /// git config and maps <c>*.tm7</c> to them via <c>.git/info/attributes</c> (this repository) or a
    /// global attributes file (<c>--global</c>), so the git integration works from just the binary —
    /// with no access to any repository's source and no committed <c>.gitattributes</c>. The
    /// <c>tmforge diff</c> / <c>tmforge merge</c> verbs also work standalone with no git config at all;
    /// this command only wires up the automatic <c>git diff</c> / <c>git merge</c> behavior.
    /// </summary>
    internal static class GitSetupCommand
    {
        private const string AttributesMapping = "*.tm7 diff=tmforge merge=tmforge";

        /// <summary>
        /// Runs the git-setup command.
        /// </summary>
        /// <param name="args">The command arguments (after the verb).</param>
        /// <returns>Zero on success; a non-zero value on error.</returns>
        public static int Run(string[] args)
        {
            CliArgs parsed = CliArgs.Parse(args, Array.Empty<string>(), new[] { "global", "print", "dry-run" });
            if (parsed.Help)
            {
                PrintUsage();
                return 0;
            }

            bool global = parsed.HasFlag("global");
            string tool = ResolveToolInvocation(Environment.ProcessPath);

            if (parsed.HasFlag("print") || parsed.HasFlag("dry-run"))
            {
                Console.Out.Write(RenderManualSetup(global, tool));
                return 0;
            }

            return global ? ApplyGlobal(tool) : ApplyLocal(tool);
        }

        private static int ApplyLocal(string tool)
        {
            if (!TryRunGit(new[] { "rev-parse", "--absolute-git-dir" }, out string gitDir, out _, out int exit) || exit != 0)
            {
                Console.Error.WriteLine("Not inside a git repository. Run this from a repository, or pass --global.");
                return 1;
            }

            if (!ConfigureDrivers(global: false, tool))
            {
                return 1;
            }

            string attributesPath = Path.Join(gitDir.Trim(), "info", "attributes");
            bool added = EnsureMapping(attributesPath);
            Console.Error.WriteLine("Configured this repository: .tm7 files now use tmforge for 'git diff' and 'git merge'.");
            Console.Error.WriteLine(added
                ? "Mapped *.tm7 in " + attributesPath + " (local; not committed)."
                : "The *.tm7 mapping was already present in " + attributesPath + ".");
            return 0;
        }

        private static int ApplyGlobal(string tool)
        {
            if (!ConfigureDrivers(global: true, tool))
            {
                return 1;
            }

            string attributesPath = ResolveGlobalAttributesPath();
            bool added = EnsureMapping(attributesPath);
            Console.Error.WriteLine("Configured globally: every repository now uses tmforge for .tm7 'git diff' and 'git merge'.");
            Console.Error.WriteLine(added
                ? "Mapped *.tm7 in " + attributesPath + "."
                : "The *.tm7 mapping was already present in " + attributesPath + ".");
            return 0;
        }

        private static bool ConfigureDrivers(bool global, string tool)
        {
            return SetConfig(global, "diff.tmforge.textconv", ToolCommand(tool, "diff --textconv"))
                && SetConfig(global, "merge.tmforge.name", "Threat Model Forge semantic merge")
                && SetConfig(global, "merge.tmforge.driver", ToolCommand(tool, "merge %O %A %B %P"));
        }

        private static string ResolveGlobalAttributesPath()
        {
            if (TryRunGit(new[] { "config", "--global", "core.attributesFile" }, out string configured, out _, out int exit)
                && exit == 0
                && !string.IsNullOrWhiteSpace(configured))
            {
                return ExpandHome(configured.Trim());
            }

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string path = Path.Join(home, ".config", "git", "attributes");
            SetConfig(global: true, "core.attributesFile", path);
            return path;
        }

        private static string ExpandHome(string path)
        {
            if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return path.Length == 1 ? home : Path.Join(home, path.Substring(2));
            }

            return path;
        }

        private static bool EnsureMapping(string attributesPath)
        {
            string? directory = Path.GetDirectoryName(attributesPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            if (File.Exists(attributesPath))
            {
                foreach (string trimmed in File.ReadAllLines(attributesPath)
                    .Select(line => line.Trim())
                    .Where(trimmed => trimmed.StartsWith("*.tm7", StringComparison.Ordinal) && trimmed.Contains("tmforge", StringComparison.Ordinal)))
                {
                    return false; // Already mapped; leave any existing mapping untouched.
                }
            }

            using (StreamWriter writer = File.AppendText(attributesPath))
            {
                writer.WriteLine(AttributesMapping);
            }

            return true;
        }

        private static bool SetConfig(bool global, string key, string value)
        {
            string[] args = global
                ? new[] { "config", "--global", key, value }
                : new[] { "config", key, value };
            if (!TryRunGit(args, out _, out string error, out int exit) || exit != 0)
            {
                Console.Error.WriteLine("git config failed for " + key
                    + (string.IsNullOrWhiteSpace(error) ? " (is git installed and on PATH?)." : ": " + error.Trim()));
                return false;
            }

            return true;
        }

        private static bool TryRunGit(string[] args, out string standardOutput, out string standardError, out int exitCode)
        {
            standardOutput = string.Empty;
            standardError = string.Empty;
            exitCode = -1;

            ProcessStartInfo info = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (string arg in args)
            {
                info.ArgumentList.Add(arg);
            }

            try
            {
                using Process process = Process.Start(info) ?? throw new InvalidOperationException("git did not start.");
                standardOutput = process.StandardOutput.ReadToEnd();
                standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();
                exitCode = process.ExitCode;
                return true;
            }
            catch (Win32Exception)
            {
                return false; // git is not installed or not on PATH.
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static string ResolveToolInvocation(string? processPath)
        {
            if (!string.IsNullOrEmpty(processPath)
                && Path.GetFileNameWithoutExtension(processPath!).Equals("tmforge", StringComparison.OrdinalIgnoreCase))
            {
                return processPath!;
            }

            return "tmforge";
        }

        private static string ToolCommand(string tool, string suffix)
        {
            string invocation = tool.Contains(' ') ? "'" + tool + "'" : tool;
            return invocation + " " + suffix;
        }

        private static string RenderManualSetup(bool global, string tool)
        {
            string scope = global ? " --global" : string.Empty;
            string textconv = ToolCommand(tool, "diff --textconv");
            string driver = ToolCommand(tool, "merge %O %A %B %P");
            string attributes = global
                ? "git config --global core.attributesFile ~/.config/git/attributes\n"
                    + "mkdir -p ~/.config/git && echo '" + AttributesMapping + "' >> ~/.config/git/attributes\n"
                : "echo '" + AttributesMapping + "' >> \"$(git rev-parse --git-dir)/info/attributes\"\n";

            return "# Enable tmforge for .tm7 in "
                + (global ? "ALL your repositories" : "THIS repository")
                + " (no committed .gitattributes needed):\n"
                + "git config" + scope + " diff.tmforge.textconv \"" + textconv + "\"\n"
                + "git config" + scope + " merge.tmforge.name \"Threat Model Forge semantic merge\"\n"
                + "git config" + scope + " merge.tmforge.driver \"" + driver + "\"\n"
                + attributes;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Configure git to use tmforge for .tm7 diffs and merges (no committed .gitattributes needed).");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge git-setup [--global] [--print]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Registers a diff textconv and a merge driver in git config, and maps *.tm7 to them via");
            Console.Error.WriteLine(".git/info/attributes (this repo) or your global attributes file (--global) — so it works");
            Console.Error.WriteLine("from just the binary, with no access to any repository's source. --print shows the exact");
            Console.Error.WriteLine("commands instead of applying them.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Note: 'tmforge diff' and 'tmforge merge' also work standalone, with no git configuration.");
        }
    }
}
