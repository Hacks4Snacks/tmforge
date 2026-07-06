namespace ThreatModelForge.Analysis
{
    /// <summary>
    /// The orientation of a 3 points in 2D space.
    /// </summary>
    internal enum Orientation
    {
        /// <summary>
        /// The points are colinear.
        /// </summary>
        Colinear,

        /// <summary>
        /// The points are oriented clockwise.
        /// </summary>
        Clockwise,

        /// <summary>
        /// The points are oriented counter-clockwise.
        /// </summary>
        CounterClockwise,
    }

    /// <summary>
    /// A point in 2D space.
    /// </summary>
    internal struct Point2D
    {
        /// <summary>
        /// Gets or sets the X coordinate.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the Y coordinate.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Gets the orientation of 3 points.
        /// </summary>
        /// <param name="pt0">The 1st point.</param>
        /// <param name="pt1">The 2nd point.</param>
        /// <param name="pt2">The 3rd point.</param>
        /// <returns>The orientation of the 3 points.</returns>
        public static Orientation GetOrientation(Point2D pt0, Point2D pt1, Point2D pt2)
        {
            // NOTE: this is -CrossP(pt1 - pt0, pt2 - pt1);
            int val = ((pt1.Y - pt0.Y) * (pt2.X - pt1.X)) - ((pt1.X - pt0.X) * (pt2.Y - pt1.Y));
            if (val == 0)
            {
                return Orientation.Colinear;
            }

            return val > 0 ? Orientation.Clockwise : Orientation.CounterClockwise;
        }
    }
}
