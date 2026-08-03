namespace ThreatModelForge.Cli
{
    using System;
    using System.IO;
    using ThreatModelForge.Engine;
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

            // An authoring manifest is a threat model's reviewable source, not a model document, so no
            // provider claims it. Saying so beats the registry's generic "specify a format id", which
            // sends the reader looking for a format that does not exist.
            if (format == null && IsManifest(path))
            {
                throw new NotSupportedException(
                    "'" + path + "' is a tmforge authoring manifest, not a threat model. Build a model " +
                    "from it first: tmforge apply \"" + path + "\" --out model.tm7");
            }

            ThreatModel model = registry.Load(path, format?.Id);
            return (model, format);
        }

        /// <summary>
        /// Reports whether the file is a declarative authoring manifest. Only reached once no provider
        /// has claimed the content, so reading the file a second time costs nothing on the success path.
        /// </summary>
        /// <param name="path">The file to inspect.</param>
        /// <returns><see langword="true"/> when the file declares the manifest schema.</returns>
        private static bool IsManifest(string path)
        {
            try
            {
                return ManifestSupport.LooksLikeManifest(File.ReadAllText(path));
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
