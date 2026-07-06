namespace ThreatModelForge.Analysis
{
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Listing for a component.
    /// </summary>
    public class ComponentListing : EntityListing
    {
        /// <summary>
        /// Creates a listing from a component.
        /// </summary>
        /// <param name="entity">The entity that is expected to be a component.</param>
        /// <param name="diagram">The diagram containing the component.</param>
        /// <returns>A new instance of the <see cref="ComponentListing"/> class.</returns>
        public static ComponentListing FromComponent(
            Entity entity,
            DrawingSurfaceModel diagram)
        {
            ComponentListing result = new ComponentListing();
            result.Populate(entity, diagram);
            return result;
        }
    }
}
