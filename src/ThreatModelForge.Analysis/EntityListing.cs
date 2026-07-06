namespace ThreatModelForge.Analysis
{
    using System;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Base class for stencil listings.
    /// </summary>
    /// <seealso cref="ModelListing"/>
    public abstract class EntityListing
    {
        /// <summary>
        /// Gets or sets the ID of the entity.
        /// </summary>
        public Guid ID { get; set; }

        /// <summary>
        /// Gets or sets the type of stencil for the entity.
        /// </summary>
        public string? TypeID { get; set; }

        /// <summary>
        /// Gets or sets the text displayed in the TM Tool.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the text displayed in the TM Tool for the type of entity.
        /// </summary>
        public string? HeaderName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the entity is an external interactor.
        /// </summary>
        public bool IsExternalInteractor { get; set; }

        /// <summary>
        /// Gets or sets the ID of the diagram that contains this entity.
        /// </summary>
        public Guid DiagramID { get; set; }

        /// <summary>
        /// Gets or sets the text displayed to the user for the diagram.
        /// </summary>
        public string? DiagramHeader { get; set; }

        /// <summary>
        /// Populates common properties in the listing from the source entity and the diagram.
        /// </summary>
        /// <param name="source">The source entity.</param>
        /// <param name="diagram">The diagram the contains the source entity.</param>
        protected void Populate(Entity source, DrawingSurfaceModel diagram)
        {
            if (diagram == null)
            {
                throw new ArgumentNullException(nameof(diagram));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            this.DiagramHeader = diagram.Header ?? string.Empty;
            this.DiagramID = diagram.Guid;
            this.ID = source.Guid;
            this.Name = source.Name();
            this.HeaderName = source.HeaderName();
            this.IsExternalInteractor = source.IsExternalInteractor();
            this.TypeID = source.TypeId;
        }
    }
}
