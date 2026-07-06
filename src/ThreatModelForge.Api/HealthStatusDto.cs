namespace ThreatModelForge.Api
{
    /// <summary>
    /// The response body for the <c>/v1/health</c> readiness probe.
    /// </summary>
    public sealed class HealthStatusDto
    {
        /// <summary>Gets the health status token (for example, <c>ok</c>).</summary>
        public string Status { get; init; } = "ok";
    }
}
