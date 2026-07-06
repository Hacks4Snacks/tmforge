namespace ThreatModelForge.Analysis
{
    using ThreatModelForge.Model;

    /// <summary>
    /// A listing for a trust boundary in a diagram.
    /// </summary>
    public class TrustBoundaryListing : EntityListing
    {
        /// <summary>
        /// Creates a new listing from a <see cref="BorderBoundary"/> object.
        /// </summary>
        /// <param name="border">The source object.</param>
        /// <param name="diagram">The diagram that contains the border.</param>
        /// <returns>A new instance of the <see cref="TrustBoundaryListing"/> class.</returns>
        public static TrustBoundaryListing FromBorderBoundary(
            BorderBoundary border,
            DrawingSurfaceModel diagram)
        {
            TrustBoundaryListing result = new TrustBoundaryListing();
            result.Populate(border, diagram);
            return result;
        }

        /// <summary>
        /// Creates a new listing from a <see cref="LineBoundary"/> object.
        /// </summary>
        /// <param name="line">The source trust boundary.</param>
        /// <param name="diagram">The diagram that contains the line.</param>
        /// <returns>A new instance of the <see cref="TrustBoundaryListing"/> class.</returns>
        public static TrustBoundaryListing FromLineBoundary(
            LineBoundary line,
            DrawingSurfaceModel diagram)
        {
            TrustBoundaryListing result = new TrustBoundaryListing();
            result.Populate(line, diagram);
            return result;
        }
    }
}
