namespace ThreatModelForge.Editing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Deterministic, dependency-free automatic layout for a diagram. Given a logical graph whose
    /// components may lack coordinates — for example, an agent- or CLI-authored model — it assigns
    /// readable, non-overlapping positions using a layered left-to-right placement derived from the
    /// data-flow connectors, then re-routes connector endpoints to the element edges. The result is
    /// a pure function of the input, so repeated runs are byte-identical, and every component ends
    /// up at a distinct coordinate so geometry-dependent analysis (for example, boundary crossing)
    /// has meaningful input. Trust boundaries are left where they are.
    /// </summary>
    public static class DiagramLayout
    {
        /// <summary>
        /// Applies the layered auto-layout to the supplied diagram in place.
        /// </summary>
        /// <param name="diagram">The diagram to lay out.</param>
        /// <param name="options">Spacing options, or <see langword="null"/> for the defaults.</param>
        public static void Apply(DrawingSurfaceModel diagram, LayoutOptions? options = null)
        {
            if (diagram == null)
            {
                throw new ArgumentNullException(nameof(diagram));
            }

            LayoutOptions effectiveOptions = options ?? new LayoutOptions();

            List<DrawingElement> components = CollectComponents(diagram);
            if (components.Count == 0)
            {
                return;
            }

            HashSet<Guid> componentGuids = new HashSet<Guid>(components.Select(element => element.Guid));
            List<(Guid Source, Guid Target)> edges = CollectEdges(diagram, componentGuids);
            List<List<DrawingElement>> layers = AssignLayers(components, edges);

            PlaceColumns(layers, effectiveOptions);
            RerouteConnectors(diagram, componentGuids);
        }

        private static List<DrawingElement> CollectComponents(DrawingSurfaceModel diagram)
        {
            return diagram.Borders.Values
                .OfType<DrawingElement>()
                .Where(element => !(element is BorderBoundary))
                .OrderBy(element => element.Guid)
                .ToList();
        }

        private static List<(Guid Source, Guid Target)> CollectEdges(DrawingSurfaceModel diagram, HashSet<Guid> componentGuids)
        {
            return diagram.Lines.Values
                .OfType<Connector>()
                .Where(connector => connector.SourceGuid != connector.TargetGuid
                    && componentGuids.Contains(connector.SourceGuid)
                    && componentGuids.Contains(connector.TargetGuid))
                .Select(connector => (connector.SourceGuid, connector.TargetGuid))
                .OrderBy(edge => edge.SourceGuid)
                .ThenBy(edge => edge.TargetGuid)
                .ToList();
        }

        private static List<List<DrawingElement>> AssignLayers(
            List<DrawingElement> components,
            List<(Guid Source, Guid Target)> edges)
        {
            // Longest-path layering by bounded relaxation: layer[target] = max(layer[source] + 1).
            // The pass count is capped at the node count so a cyclic graph terminates deterministically
            // instead of relaxing forever.
            Dictionary<Guid, int> layerOf = components.ToDictionary(element => element.Guid, _ => 0);
            for (int pass = 0; pass < components.Count; pass++)
            {
                bool changed = false;
                foreach ((Guid source, Guid target) in edges)
                {
                    int candidate = layerOf[source] + 1;
                    if (candidate > layerOf[target])
                    {
                        layerOf[target] = candidate;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    break;
                }
            }

            return components
                .GroupBy(element => layerOf[element.Guid])
                .OrderBy(group => group.Key)
                .Select(group => group.OrderBy(element => element.Guid).ToList())
                .ToList();
        }

        private static void PlaceColumns(List<List<DrawingElement>> layers, LayoutOptions options)
        {
            int columnX = options.OriginX;
            foreach (List<DrawingElement> column in layers)
            {
                int columnWidth = column.Max(element => element.Width);
                int y = options.OriginY;
                foreach (DrawingElement element in column)
                {
                    element.Left = columnX + ((columnWidth - element.Width) / 2);
                    element.Top = y;
                    y += element.Height + options.NodeSpacing;
                }

                columnX += columnWidth + options.LayerSpacing;
            }
        }

        private static void RerouteConnectors(DrawingSurfaceModel diagram, HashSet<Guid> componentGuids)
        {
            foreach (Connector connector in diagram.Lines.Values.OfType<Connector>())
            {
                if (!componentGuids.Contains(connector.SourceGuid) || !componentGuids.Contains(connector.TargetGuid))
                {
                    continue;
                }

                (int sourceCenterX, int sourceCenterY) = DiagramGeometry.CenterOf(diagram, connector.SourceGuid);
                (int targetCenterX, int targetCenterY) = DiagramGeometry.CenterOf(diagram, connector.TargetGuid);
                (int sourceX, int sourceY) = DiagramGeometry.EdgePoint(diagram, connector.SourceGuid, targetCenterX, targetCenterY);
                (int targetX, int targetY) = DiagramGeometry.EdgePoint(diagram, connector.TargetGuid, sourceCenterX, sourceCenterY);

                connector.SourceX = sourceX;
                connector.SourceY = sourceY;
                connector.TargetX = targetX;
                connector.TargetY = targetY;
                connector.HandleX = (sourceX + targetX) / 2;
                connector.HandleY = (sourceY + targetY) / 2;
            }
        }
    }
}
