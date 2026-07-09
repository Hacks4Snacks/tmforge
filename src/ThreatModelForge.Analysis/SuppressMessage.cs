namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json.Serialization;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Defines a suppression of a single message analogous to
    /// <see cref="System.Diagnostics.CodeAnalysis.SuppressMessageAttribute"/>.
    /// </summary>
    public class SuppressMessage
    {
        private const string SuppressionResolutionWarningMessageID = "TM0001";

        /// <summary>
        /// Gets or sets the rule ID.
        /// </summary>
        /// <seealso cref="Rule.ID" />
        [JsonPropertyName("rule")]
        public string? RuleID { get; set; }

        /// <summary>
        /// Gets or sets the justification.
        /// </summary>
        public string? Justification { get; set; }

        /// <summary>
        /// Gets or sets the model diagram to which this suppression applies.
        /// </summary>
        /// <remarks>
        /// The format of the string is the model's header text which is displayed in the tool.
        /// It can be null or empty string for rules that are file scope.
        /// </remarks>
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets the target entity within the model to which this suppression applies.
        /// </summary>
        /// <remarks>
        /// The format of the string could be the GUID id of the entity or the name qualified by the type.
        /// It can be null or empty string for rules that are scoped to the model.
        /// </remarks>
        public string? Target { get; set; }

        /// <summary>
        /// Gets the resolved rule.
        /// </summary>
        internal Rule? ResolvedRule { get; private set; }

        /// <summary>
        /// Gets the resolved model.
        /// </summary>
        internal DrawingSurfaceModel? ResolvedModel { get; private set; }

        /// <summary>
        /// Gets the resolved entity.
        /// </summary>
        internal Entity? ResolvedEntity { get; private set; }

        /// <summary>
        /// Tries to resolve the suppression to specific objects in the model.
        /// </summary>
        /// <param name="ruleSet">The active rule set.</param>
        /// <param name="model">The active model.</param>
        /// <param name="writer">The writer.</param>
        /// <returns><c>True</c> if the suppression could be resolved; otherwise, <c>false</c>.</returns>
        internal bool TryResolve(
            RuleSet ruleSet,
            ThreatModel model,
            MessageWriter writer)
        {
            if (ruleSet == null)
            {
                throw new ArgumentNullException(nameof(ruleSet));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            this.ResolvedRule = ruleSet.Rules.FirstOrDefault(
                e => string.Equals(e.ID, this.RuleID ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            if (this.ResolvedRule == null)
            {
                writer.WriteCore(
                    MessageSeverity.Warning,
                    SuppressionResolutionWarningMessageID,
                    string.Format(CultureInfo.CurrentCulture, Properties.Resources.UnresolvedRuleFormatString, this.RuleID));
                return false;
            }

            if (!string.IsNullOrEmpty(this.Model))
            {
                this.ResolvedModel = model.DrawingSurfaceList.FirstOrDefault(
                    e => string.Equals(e.Header, this.Model, StringComparison.OrdinalIgnoreCase));
                if (this.ResolvedModel == null)
                {
                    writer.WriteCore(
                        MessageSeverity.Warning,
                        SuppressionResolutionWarningMessageID,
                        string.Format(CultureInfo.CurrentCulture, Properties.Resources.UnresolvedDrawingSurfaceFormatString, this.Model));
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(this.Target))
            {
                if (this.ResolvedModel == null)
                {
                    writer.WriteCore(
                        MessageSeverity.Warning,
                        SuppressionResolutionWarningMessageID,
                        string.Format(CultureInfo.CurrentCulture, Properties.Resources.EntityRequiresDrawingSurfaceFormatString, this.Target));
                    return false;
                }

                foreach (Entity entity in AllEntities(this.ResolvedModel).Where(entity => IsEntityMatch(entity, this.Target!)))
                {
                    this.ResolvedEntity = entity;
                    break;
                }

                if (this.ResolvedEntity == null)
                {
                    writer.WriteCore(
                        MessageSeverity.Warning,
                        SuppressionResolutionWarningMessageID,
                        string.Format(CultureInfo.CurrentCulture, Properties.Resources.UnresolvedEntityFormatString, this.Target, this.Model));
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Tests whether or not the given message matches this suppression.
        /// </summary>
        /// <param name="message">The message to match.</param>
        /// <returns><c>True</c> if it is a match; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// The call assumes the suppression has previously been resolved.
        /// </remarks>
        internal bool IsMatch(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return object.ReferenceEquals(this.ResolvedRule, message.Source) &&
                object.ReferenceEquals(this.ResolvedModel, message.Model) &&
                object.ReferenceEquals(this.ResolvedEntity, message.Target);
        }

        private static IEnumerable<Entity> AllEntities(DrawingSurfaceModel model)
        {
            return model.Borders.Values.OfType<Entity>()
                .Union(model.Lines.Values.OfType<Entity>());
        }

        private static bool IsEntityMatch(Entity entity, string target)
        {
            if (string.Equals(entity.DisplayText(), target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string targetUpper = target.ToUpperInvariant();

            if (targetUpper.Contains(entity.Guid.ToString().ToUpperInvariant()))
            {
                return true;
            }

            string? name = entity.Name();
            string? headerName = entity.HeaderName();
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(headerName) &&
                targetUpper.Contains(name!.ToUpperInvariant()) &&
                targetUpper.Contains(headerName!.ToUpperInvariant()))
            {
                return true;
            }

            return false;
        }
    }
}
