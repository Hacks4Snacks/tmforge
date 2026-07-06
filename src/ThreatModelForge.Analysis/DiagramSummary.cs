namespace ThreatModelForge.Analysis
{
    using System;
    using System.Linq;
    using ThreatModelForge.Model;

    /// <summary>
    /// Summary information about a diagram used in the <see cref="ModelReport"/>.
    /// </summary>
    public class DiagramSummary
    {
        /// <summary>
        /// Gets or sets the diagram id.
        /// </summary>
        public Guid ID { get; set; }

        /// <summary>
        /// Gets or sets the header of the diagram.
        /// </summary>
        public string? Header { get; set; }

        /// <summary>
        /// Gets or sets the number of components that are not annotations.
        /// </summary>
        public int ComponentCount { get; set; }

        /// <summary>
        /// Gets or sets the number of connectors.
        /// </summary>
        public int ConnectorCount { get; set; }

        /// <summary>
        /// Gets or sets the number of trust boundaries - either lines or boxes.
        /// </summary>
        public int TrustBoundaryCount { get; set; }

        /// <summary>
        /// Creates a summary for the given diagram.
        /// </summary>
        /// <param name="diagram">The source diagram.</param>
        /// <returns>A new instance of the <see cref="DiagramSummary"/> class.</returns>
        public static DiagramSummary FromDrawingSurfaceModel(DrawingSurfaceModel diagram)
        {
            if (diagram == null)
            {
                throw new ArgumentNullException(nameof(diagram));
            }

            return new DiagramSummary
            {
                ID = diagram.Guid,
                Header = diagram.Header ?? string.Empty,
                ComponentCount = diagram.Components().Count(),
                ConnectorCount = diagram.Lines.Values.OfType<Connector>().Count(),
                TrustBoundaryCount = diagram.TrustBoundaryBorders().Count() + diagram.TrustBoundaryLines().Count(),
            };
        }
    }
}
