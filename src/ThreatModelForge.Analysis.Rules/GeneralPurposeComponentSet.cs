namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Collections.Generic;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Configuration to declare a set of general purpose component types.
    /// </summary>
    public class GeneralPurposeComponentSet
    {
        /// <summary>
        /// Variable name for the list of general purpose component types.
        /// </summary>
        public const string VariableName = "GENPURPOSECOMPTYPES";

        private static readonly IReadOnlyList<string> DefaultTypeIds = new string[]
            {
                "SE.P.TMCore.OSProcess",
                "SE.P.TMCore.Thread",
                "SE.P.TMCore.KernelThread",
                "SE.P.TMCore.WinApp",
                "SE.P.TMCore.NetApp",
                "SE.P.TMCore.ThickClient",
                "SE.P.TMCore.BrowserClient",
                "SE.P.TMCore.PlugIn",
                "SE.P.TMCore.WebServer",
                "SE.P.TMCore.Modern",
                "SE.P.TMCore.WebApp",
                "SE.P.TMCore.Win32Service",
                "SE.P.TMCore.WebSvc",
                "SE.P.TMCore.VM",
                "SE.P.TMCore.NonMS",
                "SE.P.TMCore.AzureDataFactory",
                "SE.P.TMCore.AzureEventHub",
                "SE.P.TMCore.ALA",
                "SE.P.TMCore.AzureWebJob",
                "SE.P.TMCore.Host",
                "SE.P.TMCore.WCF",
                "SE.P.TMCore.WebAPI",
                "SE.P.TMCore.AzureAppServiceApiApp",
                "SE.P.TMCore.AzureAppServiceMobileApp",
                "SE.P.TMCore.AzureAppServiceWebApp",
                "SE.EI.TMCore.Browser",
                "SE.EI.TMCore.AuthProvider",
                "SE.EI.TMCore.WebApp",
                "SE.EI.TMCore.WebSvc",
                "SE.EI.TMCore.Megasevrice", // NOTE: not a typo.
                "SE.EI.TMCore.IoTdevice",
                "SE.DS.TMCore.CloudStorage",
                "SE.DS.TMCore.SQL",
                "SE.DS.TMCore.NoSQL",
                "SE.DS.TMCore.Device",
                "SE.DS.TMCore.AzureKeyVault",
            };

        private GeneralPurposeComponentSet(IReadOnlyList<string> typeIds)
        {
            this.TypeIds = typeIds;
        }

        /// <summary>
        /// Gets the list of well known general purpose stencils curated from the Azure and default templates.
        /// </summary>
        public static GeneralPurposeComponentSet Default { get; } = new GeneralPurposeComponentSet(DefaultTypeIds);

        /// <summary>
        /// Gets the type ids in the set.
        /// </summary>
        public IReadOnlyList<string> TypeIds { get; }

        /// <summary>
        /// Creates a new instance from the variable in the context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>
        /// A new instance of the <see cref="GeneralPurposeComponentSet"/> class.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
        public static GeneralPurposeComponentSet FromContext(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));
            if (!context.Variables.TryGetValue(
                VariableName,
                out string? generalPurposeComponentTypes))
            {
                return Default;
            }

            var types = generalPurposeComponentTypes
                .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();
            return new GeneralPurposeComponentSet(types);
        }

        /// <summary>
        /// Tests if the given entity is a general purpose componment.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <returns><c>True</c> if the type is in the set; otherwise, <c>false</c>.</returns>
        public bool IsGeneralPurposeComponent(Entity entity)
        {
            string? typeId = entity?.TypeId;
            if (string.IsNullOrWhiteSpace(typeId))
            {
                return false;
            }

            return this.TypeIds.Contains(typeId);
        }
    }
}
