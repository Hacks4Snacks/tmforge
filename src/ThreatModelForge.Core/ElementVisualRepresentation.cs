namespace ThreatModelForge
{
    using System;
    using System.Xml.Serialization;

    /// <summary>
    /// The visual representation used to render a knowledge-base element type.
    /// </summary>
    [Serializable]
    public enum ElementVisualRepresentation
    {
        /// <summary>No specific representation.</summary>
        [XmlEnum("None")]
        None,

        /// <summary>An ellipse (process).</summary>
        [XmlEnum("Ellipse")]
        Ellipse,

        /// <summary>A pair of parallel lines (data store).</summary>
        [XmlEnum("ParallelLines")]
        ParallelLines,

        /// <summary>A rectangle (external entity).</summary>
        [XmlEnum("Rectangle")]
        Rectangle,

        /// <summary>A line.</summary>
        [XmlEnum("Line")]
        Line,

        /// <summary>A boundary drawn as a line.</summary>
        [XmlEnum("LineBoundary")]
        LineBoundary,

        /// <summary>A boundary drawn as a rectangle.</summary>
        [XmlEnum("BorderBoundary")]
        BorderBoundary,

        /// <summary>Inherited from the parent element type.</summary>
        [XmlEnum("Inherited")]
        Inherited,

        /// <summary>An annotation.</summary>
        [XmlEnum("Annotation")]
        Annotation,

        /// <summary>An undefined boundary.</summary>
        [XmlEnum("UndefinedBoundary")]
        UndefinedBoundary,

        /// <summary>An undefined target.</summary>
        [XmlEnum("UndefinedTarget")]
        UndefinedTarget,
    }
}
