namespace ThreatModelForge.Analysis.Rules
{
    /// <summary>
    /// Consolidated rule ids.
    /// </summary>
    internal class RuleIDs
    {
        /// <summary>
        /// The id of the unconnected components rule.
        /// </summary>
        public const int UnconnectedComponentsRule = 1000;

        /// <summary>
        /// The id of the unconnected edges rule.
        /// </summary>
        public const int UnconnectedEdgesRule = 1001;

        /// <summary>
        /// The id of the descriptive edge name rule.
        /// </summary>
        public const int DescriptiveEdgeNameRule = 1002;

        /// <summary>
        /// The id of the rule that checks that the model has at least one trust boundary.
        /// </summary>
        public const int MissingAnyTrustBoundaryRule = 1003;

        /// <summary>
        /// The id of the rule that checks at least one edge must cross a trust boundary.
        /// </summary>
        public const int MissingAnyTrustBoundaryCrossingRule = 1004;

        /// <summary>
        /// The rule that checks there must be at least 3 components per diagram.
        /// </summary>
        public const int MinimumComponentCountRule = 1005;

        /// <summary>
        /// Rule that checks at least one diagram has at least one external interactor.
        /// </summary>
        public const int MissingAnyExternalInteractorsRule = 1006;

        /// <summary>
        /// Rule that checks that outbound edges from storage components correctly describe data flow.
        /// </summary>
        public const int OutboundStorageEdgeRule = 1007;

        /// <summary>
        /// Rule that checks that an edge is missing model information about the protocol being used.
        /// </summary>
        public const int EdgeMissingProtocolRule = 1008;

        /// <summary>
        /// Rule that checks that an edge includes its protocol in the description text.
        /// </summary>
        public const int EdgeMissingProtocolDescriptionRule = 1009;

        /// <summary>
        /// Rule that checks for missing port information on an edge if it cannot be inferred from protocol.
        /// </summary>
        public const int EdgeMissingPortRule = 1010;

        /// <summary>
        /// The id of the descriptive component name rule for generic components.
        /// </summary>
        public const int DescriptiveGenericComponentNameRule = 1011;

        /// <summary>
        /// The id of the descriptive component name rule for specific named components.
        /// </summary>
        public const int DescriptiveSpecificComponentNameRule = 1012;

        /// <summary>
        /// Edge missing data classification rule.
        /// </summary>
        public const int EdgeMissingDataClassificationRule = 1013;

        /// <summary>
        /// Rule that checks that a storage component holding credentials is encrypted at rest.
        /// </summary>
        public const int UnencryptedSecretStoreRule = 1014;

        /// <summary>
        /// Rule that checks that a process receiving input across a trust boundary declares an authentication scheme.
        /// </summary>
        public const int UnauthenticatedBoundaryProcessRule = 1015;

        /// <summary>
        /// Rule that checks that an edge crossing a trust boundary does not use a cleartext protocol.
        /// </summary>
        public const int CleartextTrustBoundaryCrossingRule = 1016;

        /// <summary>
        /// Rule that checks that a process receiving input across a trust boundary sanitizes it.
        /// </summary>
        public const int UnsanitizedCrossBoundaryInputRule = 1017;

        /// <summary>
        /// Rule that checks that a process sending output to an external entity sanitizes it.
        /// </summary>
        public const int UnsanitizedExternalOutputRule = 1018;

        /// <summary>
        /// Rule that checks that a process receiving input across a trust boundary runs with isolation.
        /// </summary>
        public const int WeakProcessIsolationRule = 1019;

        /// <summary>
        /// Rule that checks that a data store holding credentials enforces meaningful access control.
        /// </summary>
        public const int UnprotectedCredentialStoreRule = 1020;

        /// <summary>
        /// Rule that checks that a data store holding log or audit data is signed.
        /// </summary>
        public const int UnsignedAuditLogStoreRule = 1021;

        /// <summary>
        /// Rule that checks that a data store recording log data does not also store credentials.
        /// </summary>
        public const int CredentialsInLogStoreRule = 1022;

        /// <summary>
        /// Rule that checks that an external entity initiating flows into the system authenticates itself.
        /// </summary>
        public const int UnauthenticatedExternalSourceRule = 1023;

        /// <summary>
        /// Rule that checks that a process does not run as a highly privileged account.
        /// </summary>
        public const int OverPrivilegedProcessRule = 1024;

        /// <summary>
        /// Rule that flags an element or flow declaring a non-approved encryption algorithm.
        /// </summary>
        public const int WeakCipherRule = 1025;

        /// <summary>
        /// Rule that flags a single static identity asserted by flows from multiple distinct sources.
        /// </summary>
        public const int SharedStaticIdentityRule = 1026;

        /// <summary>
        /// Rule that flags a flow that caches a credential read from a credential store.
        /// </summary>
        public const int CachedCredentialReadRule = 1027;
    }
}
