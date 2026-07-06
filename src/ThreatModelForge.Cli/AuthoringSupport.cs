namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Shared helpers for the imperative authoring verbs (<c>new</c>, <c>add</c>, <c>connect</c>,
    /// <c>remove</c>, <c>rename</c>): diagram resolution, deterministic placement, element-kind
    /// parsing, and atomic model writes.
    /// </summary>
    internal static class AuthoringSupport
    {
        /// <summary>
        /// Returns the model's first diagram, creating an empty "Diagram 1" when it has none.
        /// </summary>
        /// <param name="model">The model to inspect.</param>
        /// <returns>The first (or newly created) diagram.</returns>
        public static DrawingSurfaceModel GetOrCreateFirstDiagram(ThreatModel model)
        {
            if (model.DrawingSurfaceList.Count > 0)
            {
                return model.DrawingSurfaceList[0];
            }

            DrawingSurfaceModel created = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Diagram 1" };
            model.DrawingSurfaceList.Add(created);
            return created;
        }

        /// <summary>
        /// Returns the model's first diagram, or <see langword="null"/> when it has none.
        /// </summary>
        /// <param name="model">The model to inspect.</param>
        /// <returns>The first diagram, or <see langword="null"/>.</returns>
        public static DrawingSurfaceModel? FirstDiagram(ThreatModel model)
        {
            return model.DrawingSurfaceList.Count > 0 ? model.DrawingSurfaceList[0] : null;
        }

        /// <summary>
        /// Maps a CLI element-kind noun (case-insensitive) to a <see cref="StencilKind"/>.
        /// </summary>
        /// <param name="kind">The kind noun (for example, <c>process</c> or <c>store</c>).</param>
        /// <param name="result">On success, the mapped stencil kind.</param>
        /// <returns><see langword="true"/> if the noun was recognized.</returns>
        public static bool TryParseKind(string kind, out StencilKind result)
        {
            switch (kind.ToLowerInvariant())
            {
                case "process":
                    result = StencilKind.Process;
                    return true;
                case "store":
                case "datastore":
                case "data-store":
                    result = StencilKind.DataStore;
                    return true;
                case "external":
                case "externalinteractor":
                case "interactor":
                case "entity":
                    result = StencilKind.ExternalEntity;
                    return true;
                case "boundary":
                case "trustboundary":
                case "trust-boundary":
                    result = StencilKind.TrustBoundary;
                    return true;
                default:
                    result = StencilKind.Process;
                    return false;
            }
        }

        /// <summary>
        /// Computes a deterministic, non-overlapping grid position for the next element, based on the
        /// current element count, so successive additions do not stack.
        /// </summary>
        /// <param name="diagram">The diagram receiving the element.</param>
        /// <returns>The left and top coordinates for the new element.</returns>
        public static (int Left, int Top) NextPosition(DrawingSurfaceModel diagram)
        {
            int index = diagram.Borders.Count;
            int column = index % 4;
            int row = index / 4;
            return (48 + (column * 220), 48 + (row * 140));
        }

        /// <summary>
        /// Maps a CLI element kind to its property-schema base primitive
        /// (<c>process</c>/<c>datastore</c>/<c>external</c>), or <see langword="null"/> when the kind
        /// has no typed schema (a trust boundary).
        /// </summary>
        /// <param name="kind">The element kind.</param>
        /// <returns>The schema base primitive, or <see langword="null"/>.</returns>
        public static string? SchemaBase(StencilKind kind)
        {
            switch (kind)
            {
                case StencilKind.Process:
                    return "process";
                case StencilKind.DataStore:
                    return "datastore";
                case StencilKind.ExternalEntity:
                    return "external";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Maps an existing element or flow to its property-schema base primitive
        /// (<c>flow</c>/<c>process</c>/<c>datastore</c>/<c>external</c>), or <see langword="null"/>
        /// when it has no typed schema (a trust boundary).
        /// </summary>
        /// <param name="entity">The element or connector.</param>
        /// <returns>The schema base primitive, or <see langword="null"/>.</returns>
        public static string? SchemaBase(Entity entity)
        {
            switch (entity)
            {
                case Connector:
                    return "flow";
                case StencilEllipse:
                    return "process";
                case StencilParallelLines:
                    return "datastore";
                case BorderBoundary:
                    return null;
                case DrawingElement:
                    return "external";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Applies <c>key=value</c> custom-property assignments (from repeated <c>--property</c>) to an
        /// element or flow, validating each against the typed property schema for
        /// <paramref name="appliesTo"/>. Unknown properties and values outside a closed enum are
        /// rejected (so a typo can't silently make a rule fail to match) unless <paramref name="force"/>
        /// is set, in which case they are stored and reported as warnings. Matched enum values are
        /// canonicalized to the schema's casing. Validation is skipped when the base has no schema.
        /// </summary>
        /// <param name="element">The element or connector to annotate.</param>
        /// <param name="assignments">The raw <c>key=value</c> assignments.</param>
        /// <param name="appliesTo">The schema base primitive, or <see langword="null"/> to skip validation.</param>
        /// <param name="force">Whether to store unknown/invalid assignments (reported as warnings) instead of rejecting them.</param>
        /// <param name="error">On failure, a message describing the blocking assignments.</param>
        /// <param name="warnings">On return, non-blocking warnings for forced overrides.</param>
        /// <returns><see langword="true"/> when the assignments were applied (or forced).</returns>
        public static bool TryApplyProperties(
            Entity element,
            IReadOnlyList<string> assignments,
            string? appliesTo,
            bool force,
            out string? error,
            out IReadOnlyList<string> warnings)
        {
            error = null;
            List<string> warn = new List<string>();
            warnings = warn;

            List<(string Key, string Value)> parsedAssignments = new List<(string Key, string Value)>();
            foreach (string assignment in assignments)
            {
                int separator = assignment.IndexOf('=');
                if (separator <= 0)
                {
                    error = "Invalid --property (expected KEY=VALUE): " + assignment;
                    return false;
                }

                parsedAssignments.Add((assignment.Substring(0, separator), assignment.Substring(separator + 1)));
            }

            bool schemaKnown = !string.IsNullOrEmpty(appliesTo) && PropertySchemaCatalog.For(appliesTo!).Count > 0;
            List<string> blocking = new List<string>();
            List<(string Key, string Value)> toApply = new List<(string Key, string Value)>();
            foreach ((string key, string value) in parsedAssignments)
            {
                string canonical = value;
                if (schemaKnown)
                {
                    PropertySchemaIssue? issue = PropertySchemaCatalog.Validate(appliesTo!, key, value, out canonical);
                    if (issue != null)
                    {
                        if (force)
                        {
                            warn.Add("warning: " + Describe(issue) + "; stored anyway (--force).");
                        }
                        else
                        {
                            blocking.Add(Describe(issue));
                            continue;
                        }
                    }
                }

                toApply.Add((key, canonical));
            }

            if (blocking.Count > 0)
            {
                blocking.Add("Pass --force to store unknown properties/values, or run 'tmforge properties" +
                    (string.IsNullOrEmpty(appliesTo) ? string.Empty : " --base " + appliesTo) +
                    "' to see valid names and values.");
                error = string.Join(Environment.NewLine, blocking);
                return false;
            }

            foreach ((string key, string value) in toApply)
            {
                DiagramElementHelper.SetCustomProperty(element, key, value);
            }

            return true;
        }

        /// <summary>
        /// Writes the model to <paramref name="path"/> atomically: it serializes to a temporary file
        /// in the same directory and renames it over the target, so a failed write never corrupts an
        /// existing file.
        /// </summary>
        /// <param name="model">The model to write.</param>
        /// <param name="path">The destination path.</param>
        /// <param name="format">The format provider to write with.</param>
        public static void Save(ThreatModel model, string path, IThreatModelFormat format)
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath) ?? ".";
            string temp = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream stream = File.Create(temp))
                {
                    format.Write(model, stream);
                }

                File.Move(temp, fullPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
        }

        private static string Describe(PropertySchemaIssue issue)
        {
            return issue.Kind == PropertySchemaIssueKind.UnknownProperty
                ? "unknown property '" + issue.Property + "' for " + issue.AppliesTo + " (known: " + string.Join(", ", issue.Allowed) + ")"
                : "invalid value '" + issue.Value + "' for " + issue.AppliesTo + " property '" + issue.Property + "' (allowed: " + string.Join(", ", issue.Allowed) + ")";
        }
    }
}
