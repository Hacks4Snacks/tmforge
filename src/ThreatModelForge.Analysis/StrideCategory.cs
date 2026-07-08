namespace ThreatModelForge.Analysis
{
    /// <summary>
    /// The six STRIDE threat categories. A <see cref="Rule"/> may declare the category its finding
    /// represents (see <see cref="Rule.Stride"/>); a rule that leaves it unset is a structural or
    /// naming hygiene check, not a threat.
    /// </summary>
    public enum StrideCategory
    {
        /// <summary>Impersonating something or someone else.</summary>
        Spoofing,

        /// <summary>Unauthorized modification of data or code.</summary>
        Tampering,

        /// <summary>Denying an action without other parties being able to prove otherwise.</summary>
        Repudiation,

        /// <summary>Exposure of information to parties not authorized to see it.</summary>
        InformationDisclosure,

        /// <summary>Denying or degrading service to legitimate users.</summary>
        DenialOfService,

        /// <summary>Gaining capabilities without proper authorization.</summary>
        ElevationOfPrivilege,
    }
}
