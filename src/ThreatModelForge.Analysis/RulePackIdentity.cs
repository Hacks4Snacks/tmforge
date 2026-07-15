namespace ThreatModelForge.Analysis
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Creates deterministic pack fingerprints, pack identifiers, and effective rule identifiers.
    /// </summary>
    public static class RulePackIdentity
    {
        /// <summary>The maximum length of one pack or source-rule identity segment.</summary>
        internal const int IdentitySegmentLength = 512;

        private const int PackSlugLength = 64;

        /// <summary>Creates a lowercase SHA-256 content fingerprint.</summary>
        /// <param name="content">The source pack bytes.</param>
        /// <returns>A <c>sha256:</c>-prefixed fingerprint.</returns>
        public static string CreateFingerprint(byte[] content)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            using SHA256 sha256 = SHA256.Create();
            return "sha256:" + ToHex(sha256.ComputeHash(content));
        }

        /// <summary>Creates a normalized pack id from a source name and source bytes.</summary>
        /// <param name="name">The source pack or manifest name.</param>
        /// <param name="content">The source bytes whose fingerprint disambiguates the name.</param>
        /// <returns>A stable id in the form <c>normalized-name-&lt;32 hex characters&gt;</c>.</returns>
        public static string CreatePackId(string name, byte[] content)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A pack name is required.", nameof(name));
            }

            string slug = CreateSlug(name);
            string fingerprint = CreateFingerprint(content);
            return slug + "-" + fingerprint.Substring("sha256:".Length, 32);
        }

        /// <summary>Combines a pack id and source rule id into the stable runtime rule id.</summary>
        /// <param name="packId">The validated pack id.</param>
        /// <param name="sourceRuleId">The rule id within the source pack.</param>
        /// <returns>The effective id in the form <c>pack-id/source-id</c>.</returns>
        public static string CreateEffectiveRuleId(string packId, string sourceRuleId)
        {
            ValidateSegment(packId, nameof(packId));
            ValidateSegment(sourceRuleId, nameof(sourceRuleId));
            return packId + "/" + sourceRuleId;
        }

        /// <summary>Combines a pack id and source category id into a stable runtime category id.</summary>
        /// <param name="packId">The validated pack id.</param>
        /// <param name="sourceCategoryId">The category id within the source pack.</param>
        /// <returns>The effective id in the form <c>pack-id/source-id</c>.</returns>
        public static string CreateEffectiveCategoryId(string packId, string sourceCategoryId)
        {
            ValidateSegment(packId, nameof(packId));
            ValidateSegment(sourceCategoryId, nameof(sourceCategoryId));
            return packId + "/" + sourceCategoryId;
        }

        /// <summary>Checks whether a value is valid as one effective-identity segment.</summary>
        /// <param name="value">The value to check.</param>
        /// <returns><see langword="true"/> when the value satisfies the identity contract.</returns>
        internal static bool IsValidSegment(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value!.Length <= IdentitySegmentLength &&
                value.IndexOf('/') < 0 &&
                string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
                value.All(character => character >= ' ' && character <= '~');
        }

        private static string CreateSlug(string value)
        {
            StringBuilder result = new StringBuilder();
            bool pendingSeparator = false;
            foreach (char character in value.Normalize(NormalizationForm.FormKC))
            {
                char lower = char.ToLower(character, CultureInfo.InvariantCulture);
                bool accepted = (lower >= 'a' && lower <= 'z') || (lower >= '0' && lower <= '9');
                if (accepted)
                {
                    if (pendingSeparator && result.Length > 0 && result.Length < PackSlugLength)
                    {
                        result.Append('-');
                    }

                    pendingSeparator = false;
                    if (result.Length < PackSlugLength)
                    {
                        result.Append(lower);
                    }

                    continue;
                }

                pendingSeparator = result.Length > 0;
            }

            return result.Length == 0 ? "rule-pack" : result.ToString().TrimEnd('-');
        }

        private static void ValidateSegment(string value, string parameterName)
        {
            if (!IsValidSegment(value))
            {
                throw new ArgumentException(
                    $"Identity segments must be non-empty printable ASCII, at most {IdentitySegmentLength} characters, and cannot contain '/' or surrounding whitespace.",
                    parameterName);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }
    }
}
