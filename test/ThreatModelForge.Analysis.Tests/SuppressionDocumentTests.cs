namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <see cref="SuppressionDocument"/> class.
    /// </summary>
    [TestClass]
    public class SuppressionDocumentTests
    {
        /// <summary>
        /// Gets a value indicating whether the current operating system uses a case-insensitive file
        /// system by default. Windows and macOS do; other platforms are treated as case-sensitive.
        /// </summary>
        private static bool CaseInsensitiveFileSystem =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        /// <summary>
        /// A suppression is returned when the queried path matches a declared path exactly.
        /// </summary>
        [TestMethod]
        public void GetSuppressionsMatchesExactPath()
        {
            string path = Path.Combine(Path.GetTempPath(), "Model.tm7");
            SuppressionDocument document = DocumentFor(path);

            SuppressMessage[] resolved = document.GetSuppressions(path).ToArray();

            Assert.AreEqual(1, resolved.Length);
            Assert.AreEqual("TM1000", resolved[0].RuleID);
        }

        /// <summary>
        /// Unit test for the <see cref="SuppressionDocument.GetSuppressions(string)"/> method covering
        /// the case-sensitivity of path matching. A suppression declared for <c>Model.tm7</c> must only
        /// be applied to a differently cased path such as <c>model.tm7</c> on file systems that are
        /// themselves case-insensitive. On a case-sensitive file system (Linux and other Unix hosts,
        /// including the Linux runner the packaged GitHub Action executes on) the two are distinct
        /// files and the suppression must not leak between them.
        /// </summary>
        [TestMethod]
        public void GetSuppressionsRespectsFileSystemCaseSensitivity()
        {
            string directory = Path.GetTempPath();
            string declared = Path.Combine(directory, "Model.tm7");
            string differentCase = Path.Combine(directory, "model.tm7");
            SuppressionDocument document = DocumentFor(declared);

            int resolved = document.GetSuppressions(differentCase).Count();

            int expected = CaseInsensitiveFileSystem ? 1 : 0;
            Assert.AreEqual(expected, resolved);
        }

        /// <summary>
        /// A path that is not declared in the document resolves to no suppressions.
        /// </summary>
        [TestMethod]
        public void GetSuppressionsReturnsEmptyForUnknownPath()
        {
            string declared = Path.Combine(Path.GetTempPath(), "Model.tm7");
            SuppressionDocument document = DocumentFor(declared);

            string other = Path.Combine(Path.GetTempPath(), "Other.tm7");
            Assert.AreEqual(0, document.GetSuppressions(other).Count());
        }

        /// <summary>
        /// A document with no file elements resolves to no suppressions rather than throwing.
        /// </summary>
        [TestMethod]
        public void GetSuppressionsReturnsEmptyWhenNoFilesDeclared()
        {
            SuppressionDocument document = new SuppressionDocument();

            string path = Path.Combine(Path.GetTempPath(), "Model.tm7");
            Assert.AreEqual(0, document.GetSuppressions(path).Count());
        }

        /// <summary>
        /// Unit test for the <see cref="SuppressionDocument.Load(string)"/> method. A relative
        /// <c>file</c> reference in the suppression JSON is resolved against the folder that contains
        /// the suppression document, so the absolute model path resolves to the declared suppressions.
        /// </summary>
        [TestMethod]
        public void LoadResolvesRelativeFilePathsAgainstDocumentFolder()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string documentPath = Path.Combine(directory, "suppressions.json");
                string json =
                    "{\"files\":[{\"file\":\"webshop.tm7\",\"suppressions\":" +
                    "[{\"rule\":\"TM1000\",\"justification\":\"accepted\"}]}]}";
                File.WriteAllText(documentPath, json);

                SuppressionDocument document = SuppressionDocument.Load(documentPath);

                string modelPath = Path.Combine(directory, "webshop.tm7");
                SuppressMessage[] resolved = document.GetSuppressions(modelPath).ToArray();

                Assert.AreEqual(1, resolved.Length);
                Assert.AreEqual("TM1000", resolved[0].RuleID);
                Assert.AreEqual("accepted", resolved[0].Justification);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>
        /// Builds a document that declares a single <c>TM1000</c> suppression for the given rooted path.
        /// </summary>
        /// <param name="filePath">The rooted threat-model path the suppression applies to.</param>
        /// <returns>The constructed <see cref="SuppressionDocument"/>.</returns>
        private static SuppressionDocument DocumentFor(string filePath)
        {
            return new SuppressionDocument
            {
                Files = new FileSuppressionElement[]
                {
                    new FileSuppressionElement
                    {
                        File = filePath,
                        Suppressions = new SuppressMessage[]
                        {
                            new SuppressMessage { RuleID = "TM1000", Justification = "accepted" },
                        },
                    },
                },
            };
        }
    }
}
