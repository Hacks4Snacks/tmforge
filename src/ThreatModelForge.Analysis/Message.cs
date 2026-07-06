namespace ThreatModelForge.Analysis
{
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// A message that is output to the user as a result of a <see cref="Rule"/> evaluation.
    /// </summary>
    public class Message
    {
        /// <summary>
        /// Gets or sets the severity of the message.
        /// </summary>
        public MessageSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets human-readable text for the message.
        /// </summary>
        public string? Text { get; set; }

        /// <summary>
        /// Gets or sets the source rule generating this message.
        /// </summary>
        public Rule? Source { get; set; }

        /// <summary>
        /// Gets or sets the model that contains the <see cref="Target"/>.
        /// </summary>
        public DrawingSurfaceModel? Model { get; set; }

        /// <summary>
        /// Gets or sets the target entity in the model that the rule is scanning.
        /// </summary>
        public Entity? Target { get; set; }
    }
}
