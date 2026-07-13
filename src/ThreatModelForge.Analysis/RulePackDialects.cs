namespace ThreatModelForge.Analysis
{
    /// <summary>
    /// Names the rule languages supported by version 2 rule-pack envelopes.
    /// </summary>
    public static class RulePackDialects
    {
        /// <summary>The original element/flow guard-and-requirement language.</summary>
        public const string FlatV1 = "urn:tmforge:rules:flat-v1";

        /// <summary>The recursive source/target/flow interaction language.</summary>
        public const string InteractionV1 = "urn:tmforge:rules:interaction-v1";
    }
}
