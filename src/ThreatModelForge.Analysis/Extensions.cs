namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Extension methods.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// The entity type for text annotations.
        /// </summary>
        private const string TextAnnotationGenericTypeId = "GE.A";

        private const string ExternalInteractorGenericTypeId = "GE.EI";

        private const string StorageComponentGenericTypeId = "GE.DS";

        /// <summary>
        /// Gets the name of the entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>The name or null if the entity has no name.</returns>
        public static string? Name(this Entity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            return entity!
                .Properties
                .OfType<StringDisplayAttribute>()
                .Where(e => string.Equals(e.DisplayName, "Name", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Value as string)
                .Where(e => !string.IsNullOrEmpty(e))
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets the entity header name which is also the type of the entity displayed to the user.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>The header or null if the entity does not have a header.</returns>
        public static string? HeaderName(this Entity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            return entity
                .Properties
                .OfType<HeaderDisplayAttribute>()
                .Select(e => e.DisplayName)
                .Where(e => !string.IsNullOrEmpty(e))
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets appropriate display text to show in messages for the given entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>
        /// Human readable text that can uniquely identify an entity in a message.
        /// </returns>
        public static string DisplayText(this Entity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            string? name = entity.Name();
            string? headerName = entity.HeaderName();

            string text;
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(headerName))
            {
                text = string.Format(
                    CultureInfo.CurrentCulture,
                    Properties.Resources.UnnamedEntityWithIDFormatString,
                    entity.Guid);
            }
            else if (string.IsNullOrEmpty(name))
            {
                text = string.Format(
                    CultureInfo.CurrentCulture,
                    Properties.Resources.UnnamedEntityWithKnownTypeFormatString,
                    headerName,
                    entity.Guid);
            }
            else if (string.IsNullOrEmpty(headerName))
            {
                text = string.Format(
                    CultureInfo.CurrentCulture,
                    Properties.Resources.NamedEntityWithUnknownTypeFormatString,
                    name,
                    entity.Guid);
            }
            else
            {
                text = string.Format(
                    CultureInfo.CurrentCulture,
                    Properties.Resources.NamedEntityWithKnownTypeFormatString,
                    name,
                    headerName,
                    entity.Guid);
            }

            return text;
        }

        /// <summary>
        /// Gets all the trust boundary borders in the diagram.
        /// </summary>
        /// <param name="model">The drawing surface model.</param>
        /// <returns>The list of trust boundary borders.</returns>
        public static IEnumerable<BorderBoundary> TrustBoundaryBorders(
            this DrawingSurfaceModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            return model.Borders.Values.OfType<BorderBoundary>();
        }

        /// <summary>
        /// Gets all the trust boundary lines on the given drawing surface.
        /// </summary>
        /// <param name="model">The drawing surface model.</param>
        /// <returns>The list of trust boundary lines.</returns>
        public static IEnumerable<LineBoundary> TrustBoundaryLines(
            this DrawingSurfaceModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            return model.Lines.Values.OfType<LineBoundary>();
        }

        /// <summary>
        /// Gets the list of components that are not annotations or trust boundary boxes.
        /// </summary>
        /// <param name="model">The diagram.</param>
        /// <returns>The list of components.</returns>
        public static IEnumerable<Entity> Components(
            this DrawingSurfaceModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            return model
                .Borders
                .Values
                .OfType<Entity>()
                .Where(IsComponent);
        }

        /// <summary>
        /// Checks if the given entity is a component.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <returns>
        /// <c>True</c> if the entity is a component; otherwise <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="entity"/> is <see langword="null"/>.
        /// </exception>
        public static bool IsComponent(this Entity entity) =>
            !entity.IsTextAnnotation() && entity is not BorderBoundary;

        /// <summary>
        /// Tests if the given point exists within the bounding rectangle of the drawing element.
        /// </summary>
        /// <param name="element">The drawing element.</param>
        /// <param name="x">The x coordinate of the point.</param>
        /// <param name="y">The y coordinate of the point.</param>
        /// <returns><c>True</c> if the point is within the bounds of the element; otherwise, <c>false</c>.</returns>
        public static bool Contains(this DrawingElement element, int x, int y)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            int x0 = x - element.Left;
            int y0 = y - element.Top;
            return x0 >= 0 && x0 < element.Width && y0 >= 0 && y0 < element.Height;
        }

        /// <summary>
        /// Tests if the given connector crosses the given trust boundary.
        /// </summary>
        /// <param name="connector">The connector edge.</param>
        /// <param name="boundary">The trust boundary.</param>
        /// <returns><c>True</c> if the connector has either source or target within the boundary; otherwise, <c>false</c>.</returns>
        public static bool Crosses(this Connector connector, BorderBoundary boundary)
        {
            if (connector == null)
            {
                throw new ArgumentNullException(nameof(connector));
            }

            if (boundary == null)
            {
                throw new ArgumentNullException(nameof(boundary));
            }

            bool containsSource = boundary.Contains(connector.SourceX, connector.SourceY);
            bool containsTarget = boundary.Contains(connector.TargetX, connector.TargetY);

            return containsSource ?
                !containsTarget :
                containsTarget;
        }

        /// <summary>
        /// Tests if the given connector intersects linearly with the given line trust boundary.
        /// </summary>
        /// <param name="connector">The connector edge.</param>
        /// <param name="boundary">The trust boundary line.</param>
        /// <returns><c>True</c> if the lines intersect; otherwise, <c>false</c>.</returns>
        public static bool Crosses(this Connector connector, LineBoundary boundary)
        {
            if (connector == null)
            {
                throw new ArgumentNullException(nameof(connector));
            }

            if (boundary == null)
            {
                throw new ArgumentNullException(nameof(boundary));
            }

            return Intersects(connector, boundary);
        }

        /// <summary>
        /// Enumerates all the trust boundaries that the edge crosses.
        /// </summary>
        /// <param name="model">The drawing surface model containing the edge to test.</param>
        /// <param name="connector">The edge to test.</param>
        /// <returns>A list of trust boundary entities the edge crosses.</returns>
        public static IEnumerable<Entity> TrustBoundaryCrossings(
            this DrawingSurfaceModel model,
            Connector connector)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (connector == null)
            {
                throw new ArgumentNullException(nameof(connector));
            }

            return model
                .TrustBoundaryBorders()
                .Where(e => connector.Crosses(e))
                .OfType<Entity>()
                .Union(model.TrustBoundaryLines().Where(e => connector.Crosses(e)));
        }

        /// <summary>
        /// Gets the external interactors on the given diagram.
        /// </summary>
        /// <param name="model">The diagram.</param>
        /// <returns>The list of external interactors.</returns>
        public static IEnumerable<Entity> ExternalInteractors(this DrawingSurfaceModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            return model
                .Borders
                .Values
                .OfType<Entity>()
                .Where(IsExternalInteractor);
        }

        /// <summary>
        /// Checks if the entity is a text annotation.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <returns><c>True</c> if the entity is a text annotation; otherwise, <c>false</c>.</returns>
        public static bool IsTextAnnotation(this Entity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            return !string.IsNullOrEmpty(entity.GenericTypeId) &&
                string.Equals(entity.GenericTypeId, TextAnnotationGenericTypeId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the given entity is an external interactor.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <returns><c>True</c> if the entity is an external interactor; otherwise, <c>false</c>.</returns>
        public static bool IsExternalInteractor(this Entity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            return !string.IsNullOrEmpty(entity.GenericTypeId) &&
                string.Equals(entity.GenericTypeId, ExternalInteractorGenericTypeId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the entity is an instance of a generic component.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <returns><c>True</c> is the entity is a generic component; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// TMT only supports one level of inheritance for entities, so the &quot;base&quot; will be generic.
        /// You can see this on the right in the tool where specific entity types are nested underneath their generic types.
        /// </remarks>
        public static bool IsGenericComponent(this Entity entity)
        {
            _ = entity ?? throw new ArgumentNullException(nameof(entity));
            return
                IsComponent(entity) &&
                (string.IsNullOrEmpty(entity.GenericTypeId) ||
                string.Equals(entity.GenericTypeId, entity.TypeId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if the given entity is a storage component.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <returns><c>True</c> if the entity is a storage component; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
        public static bool IsStorageComponent(this Entity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            return !string.IsNullOrEmpty(entity.GenericTypeId) &&
                string.Equals(entity.GenericTypeId, StorageComponentGenericTypeId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tries to get the value of a custom property if it is defined.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">On success, receives the value of the property. If the property is defined more than once, it receives the 1st entry.</param>
        /// <returns>
        /// <c>True</c> if the property is defined; otherwise, <c>false</c>.
        /// </returns>
        public static bool TryGetCustomPropertyValue(this Entity entity, string propertyName, out string? value)
        {
            value = null;

            if (!TryGetCustomPropertyValues(entity, propertyName, out IList<string> values))
            {
                return false;
            }

            value = values[0];
            return true;
        }

        /// <summary>
        /// Tries to get the custom property values on all custom attributes on the entity.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <param name="propertyName">The name of the property to search.</param>
        /// <param name="values">On success, receives the list of values.</param>
        /// <returns>
        /// <c>True</c> if the property is defined; otherwise, <c>false</c>.
        /// </returns>
        public static bool TryGetCustomPropertyValues(this Entity entity, string propertyName, out IList<string> values)
        {
            values = new List<string>();
            if (entity == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            string propKey = $"{propertyName}:";

            foreach (var property in entity.Properties.OfType<CustomStringDisplayAttribute>())
            {
                string propValue = property.Value?.ToString() ?? string.Empty;
                if (propValue.StartsWith(propKey, StringComparison.InvariantCultureIgnoreCase))
                {
                    values.Add(propValue.Substring(propKey.Length));
                }
            }

            return values.Count > 0;
        }

        /// <summary>
        /// Parses a semi-colon delimited list of name=value pairs into a
        /// property list.
        /// </summary>
        /// <param name="value">The string to parse.</param>
        /// <returns>Name value pairs.</returns>
        public static IReadOnlyDictionary<string, string> AsProperties(this string value)
        {
            IDictionary<string, string> parsedValue = (value ?? string.Empty).Split(';')
                .Select(e => e.Trim().Split('=').Select(f => f.Trim()).Where(f => !string.IsNullOrWhiteSpace(f)).ToArray())
                .Where(e => e.Length == 2)
                .ToDictionary(e => e[0], f => f[1], StringComparer.OrdinalIgnoreCase);
            return new ReadOnlyDictionary<string, string>(parsedValue);
        }

        /// <summary>
        /// Tokenizes a text field.
        /// </summary>
        /// <param name="value">The input to tokenize.</param>
        /// <returns>Individual tokens.</returns>
        /// <remarks>
        /// This is used by rules that need to derive information from free form fields in the model.
        /// </remarks>
        public static IEnumerable<string> TokenizeText(this string value)
        {
            return TokenizeText(new StringReader(value));
        }

        /// <summary>
        /// Tokenizes a text field.
        /// </summary>
        /// <param name="reader">The input to tokenize.</param>
        /// <returns>Individual tokens.</returns>
        public static IEnumerable<string> TokenizeText(this TextReader reader)
        {
            _ = reader ?? throw new ArgumentNullException(nameof(reader));
            int ch = reader.Peek();
            while (ch >= 0)
            {
                SkipWhitespace(reader);
                ch = reader.Peek();
                if (char.IsPunctuation((char)ch) || char.IsSeparator((char)ch))
                {
                    reader.Read();
                    yield return new string((char)ch, 1);
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append((char)ch);
                    reader.Read();
                    ch = reader.Peek();
                    while (ch >= 0 && !char.IsWhiteSpace((char)ch) && !char.IsPunctuation((char)ch) && !char.IsSeparator((char)ch))
                    {
                        sb.Append((char)ch);
                        reader.Read();
                        ch = reader.Peek();
                    }

                    yield return sb.ToString();
                }

                ch = reader.Peek();
            }
        }

        private static void SkipWhitespace(this TextReader reader)
        {
            int ch = reader.Peek();
            while (ch >= 0 && char.IsWhiteSpace((char)ch))
            {
                reader.Read();
                ch = reader.Peek();
            }
        }

        private static bool Intersects(LineElement line1, LineElement line2)
        {
            bool Between(Point2D start, Point2D end, Point2D pt)
            {
                return pt.X <= Math.Max(start.X, end.X) && pt.X >= Math.Min(start.X, end.X)
                    && pt.Y <= Math.Max(start.Y, end.Y) && pt.Y >= Math.Min(start.Y, end.Y);
            }

            Point2D pt0 = new Point2D { X = line1.SourceX, Y = line1.SourceY };
            Point2D pt1 = new Point2D { X = line1.TargetX, Y = line1.TargetY };
            Point2D pt2 = new Point2D { X = line2.SourceX, Y = line2.SourceY };
            Point2D pt3 = new Point2D { X = line2.TargetX, Y = line2.TargetY };

            Orientation orientation0 = Point2D.GetOrientation(pt0, pt1, pt2);
            Orientation orientation1 = Point2D.GetOrientation(pt0, pt1, pt3);
            Orientation orientation2 = Point2D.GetOrientation(pt2, pt3, pt0);
            Orientation orientation3 = Point2D.GetOrientation(pt2, pt3, pt1);
            if (orientation0 != orientation1 && orientation2 != orientation3)
            {
                return true;
            }

            // Test if points are on segments if colinear.
            if (orientation0 == Orientation.Colinear && Between(pt0, pt1, pt2))
            {
                return true;
            }

            if (orientation1 == Orientation.Colinear && Between(pt0, pt1, pt3))
            {
                return true;
            }

            if (orientation2 == Orientation.Colinear && Between(pt2, pt3, pt0))
            {
                return true;
            }

            if (orientation3 == Orientation.Colinear && Between(pt2, pt3, pt1))
            {
                return true;
            }

            return false;
        }
    }
}
