namespace ThreatModelForge.Cli
{
    using System.IO;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// Loads a threat model for the read-only inspection verbs, resolving the provider the same way
    /// the registry does (explicit extension, then content sniff) so the resolved format can be
    /// reported back to the user.
    /// </summary>
    internal static class CliModelLoader
    {
        /// <summary>
        /// Loads the model at <paramref name="path"/> and reports the format that was used to read it.
        /// </summary>
        /// <param name="path">The model file path.</param>
        /// <returns>The loaded model and the resolved format, or a <see langword="null"/> format if
        /// none could be identified before falling back to registry resolution.</returns>
        public static (ThreatModel Model, IThreatModelFormat? Format) Load(string path)
        {
            ThreatModelFormatRegistry registry = ThreatModelFormatRegistry.CreateDefault();
            IThreatModelFormat? format = registry.FindByExtension(path);
            if (format == null || !format.Capabilities.CanRead)
            {
                using FileStream stream = File.OpenRead(path);
                format = registry.Sniff(stream);
            }

            ThreatModel model = registry.Load(path, format?.Id);
            return (model, format);
        }
    }
}
