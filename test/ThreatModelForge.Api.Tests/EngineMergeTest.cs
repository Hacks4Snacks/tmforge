namespace ThreatModelForge.Api.Tests
{
    using System;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Unit tests for <see cref="EngineService.Merge"/>, the three-way merge that backs the Studio
    /// resolve view and the <c>/v1/model/merge</c> endpoint.
    /// </summary>
    [TestClass]
    public class EngineMergeTest
    {
        /// <summary>
        /// Verifies that non-overlapping edits from both sides combine without conflict. Element
        /// identity survives the tmforge-json round-trip because ids are GUIDs.
        /// </summary>
        [TestMethod]
        public void Merge_CombinesNonOverlappingRenames()
        {
            string process = Guid.NewGuid().ToString();
            string store = Guid.NewGuid().ToString();

            TmForgeModelDto baseModel = Model(Element(process, "process", "Web App"), Element(store, "datastore", "Database"));
            TmForgeModelDto ours = Model(Element(process, "process", "API Gateway"), Element(store, "datastore", "Database"));
            TmForgeModelDto theirs = Model(Element(process, "process", "Web App"), Element(store, "datastore", "Warehouse"));

            MergeResultDto result = EngineService.Merge(baseModel, ours, theirs);

            Assert.AreEqual(0, result.Conflicts!.Count);
            Assert.AreEqual("API Gateway", result.Merged!.Elements!.Single(element => element.Id == process).Name);
            Assert.AreEqual("Warehouse", result.Merged!.Elements!.Single(element => element.Id == store).Name);
        }

        /// <summary>
        /// Verifies that when both sides set the same attribute differently, the conflict is reported
        /// and the merged model keeps the <c>ours</c> value.
        /// </summary>
        [TestMethod]
        public void Merge_ReportsSamePropertyConflictAndKeepsOurs()
        {
            string process = Guid.NewGuid().ToString();

            MergeResultDto result = EngineService.Merge(
                Model(Element(process, "process", "Base name")),
                Model(Element(process, "process", "Ours name")),
                Model(Element(process, "process", "Theirs name")));

            MergeConflictDto conflict = result.Conflicts!.Single();
            Assert.AreEqual("Property", conflict.Kind);
            Assert.AreEqual("name", conflict.Property);
            Assert.AreEqual("Ours name", conflict.Ours);
            Assert.AreEqual("Theirs name", conflict.Theirs);
            Assert.AreEqual("Ours name", result.Merged!.Elements!.Single(element => element.Id == process).Name);
        }

        /// <summary>
        /// Verifies that when no base is supplied the merge falls back to two-way: elements unique to
        /// either side are unioned, and a shared element whose attribute diverges is reported as a
        /// conflict (kept as <c>ours</c>), since without an ancestor neither side can be presumed right.
        /// </summary>
        [TestMethod]
        public void Merge_WithoutBase_IsTwoWay()
        {
            string process = Guid.NewGuid().ToString();
            string oursOnly = Guid.NewGuid().ToString();
            string theirsOnly = Guid.NewGuid().ToString();

            TmForgeModelDto ours = Model(Element(process, "process", "Auth"), Element(oursOnly, "external", "Auditor"));
            TmForgeModelDto theirs = Model(Element(process, "process", "Frontend"), Element(theirsOnly, "external", "User"));

            MergeResultDto result = EngineService.Merge(null, ours, theirs);

            MergeConflictDto conflict = result.Conflicts!.Single();
            Assert.AreEqual("Property", conflict.Kind);
            Assert.AreEqual("name", conflict.Property);
            Assert.IsNull(conflict.Base);
            Assert.AreEqual("Auth", conflict.Ours);
            Assert.AreEqual("Frontend", conflict.Theirs);
            Assert.AreEqual("Auth", result.Merged!.Elements!.Single(element => element.Id == process).Name);
            Assert.IsTrue(result.Merged!.Elements!.Any(element => element.Id == oursOnly), "ours-only element kept");
            Assert.IsTrue(result.Merged!.Elements!.Any(element => element.Id == theirsOnly), "theirs-only element added");
        }

        private static TmForgeElementDto Element(string id, string kind, string name)
        {
            return new TmForgeElementDto { Id = id, Kind = kind, Name = name, X = 0, Y = 0 };
        }

        private static TmForgeModelDto Model(params TmForgeElementDto[] elements)
        {
            return new TmForgeModelDto { Schema = "tmforge-json", Version = "0.1", Elements = elements };
        }
    }
}
