namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Checks that all components are connected.
    /// </summary>
    public class UnconnectedComponentsRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnconnectedComponentsRule"/> class.
        /// </summary>
        public UnconnectedComponentsRule()
            : base(RuleIDs.UnconnectedComponentsRule, MessageSeverity.Error, RulePackCatalog.CoreHygiene)
        {
            this.FullDescription = UnconnectedComponentsRuleResources.FullDescription;
            this.HelpText = UnconnectedComponentsRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc />
        public override void Evaluate(RuleEvaluationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                HashSet<Guid> nodes = new HashSet<Guid>();
                foreach (Guid id in diagram.Borders.Keys)
                {
                    nodes.Add(id);
                }

                // remove connected nodes from the list.
                foreach (Connector c in diagram.Lines.Values.OfType<Connector>())
                {
                    if (nodes.Contains(c.SourceGuid))
                    {
                        nodes.Remove(c.SourceGuid);
                    }

                    if (nodes.Contains(c.TargetGuid))
                    {
                        nodes.Remove(c.TargetGuid);
                    }
                }

                // generate an error for each remaining node.
                foreach (Guid id in nodes)
                {
                    if (!(diagram.Borders[id] is Entity entity) ||
                        entity is BorderBoundary ||
                        IsExcluded(entity))
                    {
                        continue;
                    }

                    Message m = this.GenerateViolation(diagram, entity);
                    context.Writer.Write(m);
                }
            }
        }

        /// <summary>
        /// Checks if the entity should be excluded for the check.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <returns><c>True</c> if the entity should be excluded; otherwise, <c>False</c>.</returns>
        private static bool IsExcluded(Entity entity)
        {
            return entity.IsTextAnnotation();
        }

        private static string GenerateText(Entity entity)
        {
            string name = GetEntityDisplayText(entity);

            return string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                Properties.Resources.UnconnectedComponentMessageText,
                name);
        }

        private Message GenerateViolation(DrawingSurfaceModel diagram, Entity entity)
        {
            Message error = this.CreateMessage(entity, diagram, GenerateText(entity));
            return error;
        }
    }
}
