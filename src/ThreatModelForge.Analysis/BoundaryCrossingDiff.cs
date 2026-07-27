namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Compares which trust boundaries each flow crosses between two revisions of a model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists because <see cref="ModelDiff"/> deliberately ignores geometry, and trust-boundary
    /// containment is derived entirely from geometry. Dragging a data store out of its boundary
    /// changes no stored property, so the structural diff correctly reports nothing — while the thing
    /// a security reviewer most needs to know has just changed. Comparing the derived crossing state
    /// closes that gap without weakening the structural diff's useful property of staying silent when
    /// a model is merely re-laid-out.
    /// </para>
    /// <para>
    /// Flows and boundaries are matched by their stable ids, so renaming a boundary is not a crossing
    /// change and moving a flow between diagrams does not read as one flow deleted and another added.
    /// </para>
    /// </remarks>
    public static class BoundaryCrossingDiff
    {
        /// <summary>
        /// Captures which trust boundaries every flow in the model crosses.
        /// </summary>
        /// <param name="model">The model to capture.</param>
        /// <returns>One entry per flow, in the order the diagrams and flows are stored.</returns>
        public static IReadOnlyList<FlowCrossings> Capture(ThreatModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            List<FlowCrossings> captured = new List<FlowCrossings>();
            foreach (DrawingSurfaceModel surface in model.DrawingSurfaceList)
            {
                string diagramName = string.IsNullOrEmpty(surface.Header) ? "Diagram" : surface.Header!;
                foreach (Connector connector in surface.Lines.Values.OfType<Connector>())
                {
                    List<CrossedBoundary> crossed = surface
                        .TrustBoundaryCrossings(connector)
                        .Select(boundary => new CrossedBoundary
                        {
                            Id = boundary.Guid,
                            Name = DiagramElementHelper.GetName(boundary),
                        })
                        .OrderBy(boundary => boundary.Id)
                        .ToList();

                    captured.Add(new FlowCrossings
                    {
                        FlowId = connector.Guid,
                        FlowName = DiagramElementHelper.GetName(connector),
                        DiagramName = diagramName,
                        DiagramId = surface.Guid,
                        Boundaries = crossed,
                    });
                }
            }

            return captured;
        }

        /// <summary>
        /// Compares the trust-boundary crossings of two models.
        /// </summary>
        /// <param name="baseModel">The base (left-hand) model.</param>
        /// <param name="revisedModel">The revised (right-hand) model.</param>
        /// <returns>The flows whose crossings differ; empty when nothing crosses differently.</returns>
        public static CrossingDifference Compare(ThreatModel baseModel, ThreatModel revisedModel)
        {
            if (baseModel == null)
            {
                throw new ArgumentNullException(nameof(baseModel));
            }

            if (revisedModel == null)
            {
                throw new ArgumentNullException(nameof(revisedModel));
            }

            Dictionary<Guid, FlowCrossings> before = Index(Capture(baseModel));
            Dictionary<Guid, FlowCrossings> after = Index(Capture(revisedModel));

            List<CrossingChange> changes = new List<CrossingChange>();

            foreach (KeyValuePair<Guid, FlowCrossings> entry in before)
            {
                if (after.TryGetValue(entry.Key, out FlowCrossings? revised))
                {
                    Add(changes, entry.Value, revised, ChangeKind.Modified);
                }
                else
                {
                    Add(changes, entry.Value, null, ChangeKind.Removed);
                }
            }

            foreach (KeyValuePair<Guid, FlowCrossings> entry in after.Where(entry => !before.ContainsKey(entry.Key)))
            {
                Add(changes, null, entry.Value, ChangeKind.Added);
            }

            changes.Sort(CompareChanges);
            return new CrossingDifference { Changes = changes };
        }

        private static Dictionary<Guid, FlowCrossings> Index(IReadOnlyList<FlowCrossings> crossings)
        {
            Dictionary<Guid, FlowCrossings> map = new Dictionary<Guid, FlowCrossings>();
            foreach (FlowCrossings crossing in crossings)
            {
                map[crossing.FlowId] = crossing;
            }

            return map;
        }

        /// <summary>
        /// Records the crossing delta for one flow, unless nothing crosses differently. A flow that was
        /// added or removed without ever crossing a boundary is left out: the structural diff already
        /// reports it, and repeating it here would bury the crossings that matter.
        /// </summary>
        /// <param name="changes">The list being built.</param>
        /// <param name="before">The flow's crossings in the base model, or <see langword="null"/> when it is new.</param>
        /// <param name="after">The flow's crossings in the revised model, or <see langword="null"/> when it was deleted.</param>
        /// <param name="kind">What happened to the flow itself.</param>
        private static void Add(
            ICollection<CrossingChange> changes,
            FlowCrossings? before,
            FlowCrossings? after,
            ChangeKind kind)
        {
            IReadOnlyList<CrossedBoundary> previous = before?.Boundaries ?? Array.Empty<CrossedBoundary>();
            IReadOnlyList<CrossedBoundary> current = after?.Boundaries ?? Array.Empty<CrossedBoundary>();

            List<CrossedBoundary> added = Except(current, previous);
            List<CrossedBoundary> removed = Except(previous, current);
            if (added.Count == 0 && removed.Count == 0)
            {
                return;
            }

            FlowCrossings identity = after ?? before!;
            changes.Add(new CrossingChange
            {
                FlowId = identity.FlowId,
                FlowName = identity.FlowName,
                DiagramName = identity.DiagramName,
                Kind = kind,
                Added = added,
                Removed = removed,
            });
        }

        private static List<CrossedBoundary> Except(
            IReadOnlyList<CrossedBoundary> source,
            IReadOnlyList<CrossedBoundary> exclude)
        {
            HashSet<Guid> excluded = new HashSet<Guid>(exclude.Select(boundary => boundary.Id));
            return source
                .Where(boundary => !excluded.Contains(boundary.Id))
                .OrderBy(boundary => boundary.Id)
                .ToList();
        }

        private static int CompareChanges(CrossingChange left, CrossingChange right)
        {
            int byDiagram = string.CompareOrdinal(left.DiagramName, right.DiagramName);
            if (byDiagram != 0)
            {
                return byDiagram;
            }

            int byName = string.CompareOrdinal(left.FlowName, right.FlowName);
            return byName != 0 ? byName : left.FlowId.CompareTo(right.FlowId);
        }
    }
}
