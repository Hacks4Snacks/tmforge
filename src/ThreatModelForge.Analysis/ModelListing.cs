namespace ThreatModelForge.Analysis
{
    using System.Collections.ObjectModel;

    /// <summary>
    /// A listing report of stencils that is written to a separate file.
    /// </summary>
    public class ModelListing
    {
        /// <summary>
        /// Gets the listings for each component in the model.
        /// </summary>
        public Collection<ComponentListing> Components { get; } =
            new Collection<ComponentListing>();

        /// <summary>
        /// Gets the listing for each connector in the model.
        /// </summary>
        public Collection<ConnectorListing> Connectors { get; } =
            new Collection<ConnectorListing>();

        /// <summary>
        /// Gets the listing for each trust boundary in the model.
        /// </summary>
        public Collection<TrustBoundaryListing> TrustBoundaries { get; } =
            new Collection<TrustBoundaryListing>();
    }
}
