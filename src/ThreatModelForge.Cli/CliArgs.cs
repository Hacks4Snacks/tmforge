namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Shared command-line argument parser using GNU-style options (<c>--name value</c> /
    /// <c>--name=value</c>), the global <c>--json</c> flag, help flags (<c>-?</c>, <c>-h</c>,
    /// <c>--help</c>), repeatable defines (<c>--define name=value</c>), and repeatable properties
    /// (<c>--property key=value</c>).
    /// </summary>
    internal sealed class CliArgs
    {
        private readonly Dictionary<string, string> options;
        private readonly List<string> positionals;
        private readonly List<string> defines;
        private readonly List<string> propertyAssignments;
        private readonly List<string> unknownFlags;
        private readonly HashSet<string> flags;

        private CliArgs(
            bool json,
            bool help,
            Dictionary<string, string> options,
            List<string> positionals,
            List<string> defines,
            List<string> propertyAssignments,
            List<string> unknownFlags,
            HashSet<string> flags)
        {
            this.Json = json;
            this.Help = help;
            this.options = options;
            this.positionals = positionals;
            this.defines = defines;
            this.propertyAssignments = propertyAssignments;
            this.unknownFlags = unknownFlags;
            this.flags = flags;
        }

        /// <summary>
        /// Gets a value indicating whether machine-readable <c>--json</c> output was requested.
        /// </summary>
        public bool Json { get; }

        /// <summary>
        /// Gets a value indicating whether a help flag was supplied.
        /// </summary>
        public bool Help { get; }

        /// <summary>
        /// Gets the positional (non-option) arguments, in order.
        /// </summary>
        public IReadOnlyList<string> Positionals => this.positionals;

        /// <summary>
        /// Gets the raw <c>name=value</c> defines, in order.
        /// </summary>
        public IReadOnlyList<string> Defines => this.defines;

        /// <summary>
        /// Gets the raw <c>key=value</c> assignments from repeated <c>--property</c>, in order.
        /// </summary>
        public IReadOnlyList<string> Properties => this.propertyAssignments;

        /// <summary>
        /// Gets the option tokens that were not recognized as a value option, help, or define.
        /// </summary>
        public IReadOnlyList<string> UnknownFlags => this.unknownFlags;

        /// <summary>
        /// Parses the supplied arguments.
        /// </summary>
        /// <param name="args">The raw arguments (after the verb).</param>
        /// <param name="valueOptionNames">The names of options that take a value, without dashes.</param>
        /// <param name="flagNames">The names of boolean flags, without dashes.</param>
        /// <returns>A parsed <see cref="CliArgs"/> instance.</returns>
        public static CliArgs Parse(string[] args, IReadOnlyCollection<string> valueOptionNames, IReadOnlyCollection<string>? flagNames = null)
        {
            HashSet<string> valueOptions = new HashSet<string>(valueOptionNames, StringComparer.OrdinalIgnoreCase);
            HashSet<string> flagOptions = new HashSet<string>(flagNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<string> positionals = new List<string>();
            List<string> defines = new List<string>();
            List<string> propertyAssignments = new List<string>();
            List<string> unknownFlags = new List<string>();
            HashSet<string> flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool json = false;
            bool help = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (string.Equals(arg, "--json", StringComparison.Ordinal))
                {
                    json = true;
                }
                else if (string.Equals(arg, "-?", StringComparison.Ordinal)
                    || string.Equals(arg, "-h", StringComparison.Ordinal)
                    || string.Equals(arg, "--help", StringComparison.Ordinal))
                {
                    help = true;
                }
                else if (string.Equals(arg, "--define", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        defines.Add(args[++i]);
                    }
                    else
                    {
                        unknownFlags.Add(arg);
                    }
                }
                else if (arg.StartsWith("--define=", StringComparison.OrdinalIgnoreCase))
                {
                    defines.Add(arg.Substring("--define=".Length));
                }
                else if (string.Equals(arg, "--property", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        propertyAssignments.Add(args[++i]);
                    }
                    else
                    {
                        unknownFlags.Add(arg);
                    }
                }
                else if (arg.StartsWith("--property=", StringComparison.OrdinalIgnoreCase))
                {
                    propertyAssignments.Add(arg.Substring("--property=".Length));
                }
                else if (arg.StartsWith("--", StringComparison.Ordinal))
                {
                    string body = arg.Substring(2);
                    if (flagOptions.Contains(body))
                    {
                        flags.Add(body);
                    }
                    else
                    {
                        int equals = body.IndexOf('=', StringComparison.Ordinal);
                        if (equals >= 0)
                        {
                            string name = body.Substring(0, equals);
                            if (name.Length > 0 && valueOptions.Contains(name))
                            {
                                options[name] = body.Substring(equals + 1);
                            }
                            else
                            {
                                unknownFlags.Add(arg);
                            }
                        }
                        else if (valueOptions.Contains(body) && i + 1 < args.Length)
                        {
                            options[body] = args[++i];
                        }
                        else
                        {
                            unknownFlags.Add(arg);
                        }
                    }
                }
                else if (arg.StartsWith("-", StringComparison.Ordinal) && arg.Length > 1)
                {
                    unknownFlags.Add(arg);
                }
                else
                {
                    positionals.Add(arg);
                }
            }

            return new CliArgs(json, help, options, positionals, defines, propertyAssignments, unknownFlags, flags);
        }

        /// <summary>
        /// Gets the value of a named option (case-insensitive), or <see langword="null"/> if it was
        /// not supplied.
        /// </summary>
        /// <param name="name">The option name, without dashes.</param>
        /// <returns>The option value, or <see langword="null"/>.</returns>
        public string? Get(string name)
        {
            return this.options.TryGetValue(name, out string? value) ? value : null;
        }

        /// <summary>
        /// Gets a value indicating whether a boolean flag was supplied.
        /// </summary>
        /// <param name="name">The flag name, without dashes.</param>
        /// <returns><see langword="true"/> if the flag was present.</returns>
        public bool HasFlag(string name)
        {
            return this.flags.Contains(name);
        }
    }
}
