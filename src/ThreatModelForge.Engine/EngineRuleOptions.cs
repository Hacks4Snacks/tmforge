namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Selects the custom rule content that contributes to one engine operation. The built-in rules
    /// always run; these sources are additive and strictly opt-in. Every rule-reading operation on
    /// <see cref="EngineService"/> accepts the same options, so one selection produces the same
    /// catalogs, findings, threats, reports, and exports on every transport.
    /// </summary>
    public sealed class EngineRuleOptions
    {
        /// <summary>Gets the custom rule packs to load alongside the built-in rules.</summary>
        public IReadOnlyList<RuleSourceDto> Sources { get; init; } = Array.Empty<RuleSourceDto>();
    }
}
