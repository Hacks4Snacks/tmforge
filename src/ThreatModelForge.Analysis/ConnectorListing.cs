namespace ThreatModelForge.Analysis
{
    using System;
    using ThreatModelForge.Model;

    /// <summary>
    /// A listing for an edge between two components in diagram.
    /// </summary>
    public class ConnectorListing : EntityListing
    {
        /// <summary>
        /// Gets or sets the ID of the source component.
        /// </summary>
        public Guid SourceComponentID { get; set; }

        /// <summary>
        /// Gets or sets the ID of the target component.
        /// </summary>
        public Guid TargetComponentID { get; set; }

        /// <summary>
        /// Creates a new listinf from a <see cref="Connector"/> object.
        /// </summary>
        /// <param name="connector">The source connector.</param>
        /// <param name="diagram">The diagram that contains the connector.</param>
        /// <returns>A new instance of the <see cref="ConnectorListing"/> class.</returns>
        internal static ConnectorListing FromConnector(
            Connector connector,
            DrawingSurfaceModel diagram)
        {
            ConnectorListing result = new ConnectorListing();
            result.Populate(connector, diagram);
            result.SourceComponentID = connector.SourceGuid;
            result.TargetComponentID = connector.TargetGuid;

            return result;
        }
    }
}
