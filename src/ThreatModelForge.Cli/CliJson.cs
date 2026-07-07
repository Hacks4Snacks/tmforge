namespace ThreatModelForge.Cli
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Shared serialization for the CLI's machine-readable <c>--json</c> output. Payloads are
    /// wrapped in a versioned envelope so callers (agents, CI) can pin the shape.
    /// </summary>
    internal static class CliJson
    {
        /// <summary>
        /// The schema version of the <c>--json</c> envelope.
        /// </summary>
        public const int SchemaVersion = 1;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(),
            },
        };

        /// <summary>
        /// Serializes a command payload inside the versioned envelope and writes it to standard output.
        /// </summary>
        /// <param name="command">The verb that produced the payload (for example, <c>open</c>).</param>
        /// <param name="data">The command-specific payload.</param>
        public static void WriteEnvelope(string command, object data)
        {
            object envelope = new
            {
                schemaVersion = SchemaVersion,
                command,
                data,
            };

            Console.Out.WriteLine(JsonSerializer.Serialize(envelope, Options));
        }

        /// <summary>
        /// Serializes a value with the shared options (camelCase, indented, string enums). Used for
        /// side artifacts such as the merge conflict report.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The indented JSON text.</returns>
        public static string Serialize(object value)
        {
            return JsonSerializer.Serialize(value, Options);
        }
    }
}
