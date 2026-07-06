namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that flags an element or flow declaring a non-approved encryption algorithm. The approved
    /// authenticated ciphers are AES-GCM, AES-CBC with HMAC, and ChaCha20-Poly1305; any other declared
    /// cipher (for example secretbox / XSalsa20-Poly1305, AES-CBC without a MAC, 3DES, or RC4) is
    /// reported so it can be migrated to an approved cipher.
    /// </summary>
    public class WeakCipherRule : Rule
    {
        private const string AlgorithmCustomAttributeName = "Algorithm";

        private const string CipherCustomAttributeName = "Cipher";

        /// <summary>Values that indicate no cipher is declared, so the rule does not apply.</summary>
        private static readonly HashSet<string> NotDeclaredValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "None",
            "N/A",
            "NA",
            "Unset",
            "Unknown",
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="WeakCipherRule"/> class.
        /// </summary>
        public WeakCipherRule()
            : base(RuleIDs.WeakCipherRule, MessageSeverity.Warning, RulePackCatalog.DataProtection)
        {
            this.FullDescription = WeakCipherRuleResources.FullDescription;
            this.HelpText = WeakCipherRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("datastore", AlgorithmCustomAttributeName, "AES-CBC", "3DES", "RC4", "secretbox", "Other"),
            new PropertyBinding("flow", AlgorithmCustomAttributeName, "AES-CBC", "3DES", "RC4", "secretbox", "Other"),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity component in diagram.Components())
                {
                    this.EvaluateEntity(context, diagram, component);
                }

                foreach (Connector connector in diagram.Lines.Values.OfType<Connector>())
                {
                    this.EvaluateEntity(context, diagram, connector);
                }
            }
        }

        private static bool TryGetDeclaredCipher(Entity entity, out string cipher)
        {
            cipher = string.Empty;
            if (!entity.TryGetCustomPropertyValue(AlgorithmCustomAttributeName, out string? value))
            {
                entity.TryGetCustomPropertyValue(CipherCustomAttributeName, out value);
            }

            if (string.IsNullOrWhiteSpace(value) || NotDeclaredValues.Contains(value!.Trim()))
            {
                return false;
            }

            cipher = value!.Trim();
            return true;
        }

        private static bool IsApproved(string cipher)
        {
            // Normalize to alphanumeric upper-case so "AES-256-GCM", "aes_gcm", etc. compare equal.
            string normalized = new string(cipher.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

            if (Contains(normalized, "CHACHA20POLY1305"))
            {
                return true;
            }

            if (Contains(normalized, "AES") && Contains(normalized, "GCM"))
            {
                return true;
            }

            return Contains(normalized, "AES") && Contains(normalized, "CBC") && Contains(normalized, "HMAC");
        }

        private static bool Contains(string haystack, string needle)
        {
            return haystack.IndexOf(needle, StringComparison.Ordinal) >= 0;
        }

        private void EvaluateEntity(RuleEvaluationContext context, DrawingSurfaceModel diagram, Entity entity)
        {
            if (!TryGetDeclaredCipher(entity, out string cipher) || IsApproved(cipher))
            {
                return;
            }

            string text = string.Format(
                CultureInfo.CurrentCulture,
                WeakCipherRuleResources.MessageText,
                GetEntityDisplayText(entity),
                cipher);
            context.Writer.Write(this.CreateMessage(entity, diagram, text));
        }
    }
}
