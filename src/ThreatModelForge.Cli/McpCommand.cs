namespace ThreatModelForge.Cli
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Implements <c>tmforge mcp</c>: hosts a Model Context Protocol server over stdio, projecting the
    /// engine facade (read/analyze/threats/report/save/merge) and the authoring facade
    /// (apply/add/connect/set/rename/remove/export) as MCP tools so an AI agent can drive Threat Model
    /// Forge end to end. The server speaks JSON-RPC on stdin/stdout; every diagnostic goes to stderr so
    /// the protocol channel stays clean.
    /// </summary>
    internal static class McpCommand
    {
        /// <summary>
        /// Runs the MCP stdio server until the client disconnects.
        /// </summary>
        /// <param name="args">The command arguments (after the verb).</param>
        /// <returns>Zero on a clean shutdown.</returns>
        public static int Run(string[] args)
        {
            if (args != null && (Array.IndexOf(args, "--help") >= 0 || Array.IndexOf(args, "-h") >= 0 || Array.IndexOf(args, "-?") >= 0))
            {
                PrintUsage();
                return 0;
            }

            RunAsync(args ?? Array.Empty<string>()).GetAwaiter().GetResult();
            return 0;
        }

        private static async Task RunAsync(string[] args)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

            // The stdio transport owns stdout for JSON-RPC, so route every log to stderr and keep it quiet.
            builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly(typeof(McpCommand).Assembly);

            await builder.Build().RunAsync().ConfigureAwait(false);
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Run a Model Context Protocol (MCP) server over stdio for AI agents.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge mcp");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Exposes tmforge's engine and authoring facade as MCP tools: read, apply, add, connect,");
            Console.Error.WriteLine("set, rename, remove, analyze, threats, report, save, merge, export_manifest, plus grounding");
            Console.Error.WriteLine("(formats, stencils, property_schema, rules, rule_packs, manifest_schema, detect).");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Configure your MCP client to launch: command \"tmforge\", args [\"mcp\"].");
        }
    }
}
