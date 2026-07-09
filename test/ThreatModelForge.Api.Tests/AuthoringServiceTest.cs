namespace ThreatModelForge.Api.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Unit tests for <see cref="AuthoringService"/>, the tmforge-json authoring facade that the CLI,
    /// the HTTP API, the WASM shim, and an MCP server share. The facade is stateless: every call takes
    /// a model in and returns the edited model out.
    /// </summary>
    [TestClass]
    public class AuthoringServiceTest
    {
        /// <summary>
        /// Verifies that <c>add</c> materializes an element into an empty (null) model and reports its id.
        /// </summary>
        [TestMethod]
        public void Add_AddsElementToEmptyModel()
        {
            AuthoringResultDto result = AuthoringService.Add(null, new AddRequest { Kind = StencilKind.Process, Name = "Web API" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Id);
            Assert.AreEqual(1, result.Model!.Elements!.Count);
            Assert.AreEqual("Web API", result.Model.Elements![0].Name);
        }

        /// <summary>
        /// Verifies that <c>add --alias</c> gives the element a deterministic id derived from the alias.
        /// </summary>
        [TestMethod]
        public void Add_WithAlias_ProducesDeterministicId()
        {
            AuthoringResultDto result = AuthoringService.Add(null, new AddRequest { Kind = StencilKind.Process, Name = "Reconciler", Alias = "P1" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(AuthoringSupport.DeterministicId("P1").ToString(), result.Id);
        }

        /// <summary>
        /// Verifies that <c>connect</c> adds a data flow between two elements resolved by alias.
        /// </summary>
        [TestMethod]
        public void Connect_AddsFlowBetweenElements()
        {
            TmForgeModelDto model = Add(null, StencilKind.Process, "Web", "web");
            model = Add(model, StencilKind.DataStore, "DB", "db");

            AuthoringResultDto result = AuthoringService.Connect(model, new ConnectRequest { Source = "web", Target = "db", Name = "query" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("web", ResolveAlias(result.Model!, result.Source!));
            Assert.AreEqual(1, result.Model!.Flows!.Count);
            Assert.AreEqual("query", result.Model.Flows![0].Name);
        }

        /// <summary>
        /// Verifies that <c>set</c> applies and canonicalizes a typed property that analysis rules read.
        /// </summary>
        [TestMethod]
        public void Set_AppliesTypedProperty()
        {
            TmForgeModelDto model = Add(null, StencilKind.Process, "API", "api");

            AuthoringResultDto result = AuthoringService.Set(model, new SetRequest { Id = "api", Properties = new[] { "AuthenticationScheme=oauth" } });

            Assert.IsTrue(result.Success, result.Error);
            TmForgeElementDto element = result.Model!.Elements!.Single();
            Assert.AreEqual("OAuth", element.Properties["AuthenticationScheme"]);
        }

        /// <summary>
        /// Verifies that <c>set</c> rejects a property that is not in the schema, reporting an error.
        /// </summary>
        [TestMethod]
        public void Set_RejectsUnknownProperty()
        {
            TmForgeModelDto model = Add(null, StencilKind.Process, "API", "api");

            AuthoringResultDto result = AuthoringService.Set(model, new SetRequest { Id = "api", Properties = new[] { "Teleport=Yes" } });

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Error);
            Assert.IsNull(result.Model);
        }

        /// <summary>
        /// Verifies that <c>rename</c> changes an element's display name.
        /// </summary>
        [TestMethod]
        public void Rename_ChangesName()
        {
            TmForgeModelDto model = Add(null, StencilKind.Process, "Old", "p");

            AuthoringResultDto result = AuthoringService.Rename(model, new RenameRequest { Id = "p", Name = "New" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("New", result.Model!.Elements!.Single().Name);
        }

        /// <summary>
        /// Verifies that <c>remove</c> deletes an element and cascades to its connected flows.
        /// </summary>
        [TestMethod]
        public void Remove_RemovesElementAndConnectedFlows()
        {
            TmForgeModelDto model = Add(null, StencilKind.Process, "Web", "web");
            model = Add(model, StencilKind.DataStore, "DB", "db");
            model = AuthoringService.Connect(model, new ConnectRequest { Source = "web", Target = "db" }).Model!;

            AuthoringResultDto result = AuthoringService.Remove(model, new RemoveRequest { Id = "db" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2, result.Removed!.Count);
            Assert.AreEqual(1, result.Model!.Elements!.Count);
            Assert.AreEqual(0, result.Model.Flows?.Count ?? 0);
        }

        /// <summary>
        /// Verifies that <c>apply</c> materializes a manifest into a model with the expected counts.
        /// </summary>
        [TestMethod]
        public void Apply_BuildsModelFromManifest()
        {
            Manifest manifest = new Manifest
            {
                Name = "T",
                Boundaries = new List<ManifestBoundary> { new ManifestBoundary { Alias = "TB", Name = "Edge" } },
                Elements = new List<ManifestElement>
                {
                    new ManifestElement { Alias = "P1", Kind = "process", Name = "Proc", Boundary = "TB" },
                    new ManifestElement { Alias = "EXT", Kind = "external", Name = "Client" },
                },
                Flows = new List<ManifestFlow> { new ManifestFlow { From = "EXT", To = "P1", Name = "call" } },
            };

            ApplyResultDto result = AuthoringService.Apply(manifest, force: false);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Boundaries);
            Assert.AreEqual(2, result.Elements);
            Assert.AreEqual(1, result.Flows);
            Assert.IsNotNull(result.Model);
        }

        /// <summary>
        /// Verifies that <c>export</c> extracts a manifest that round-trips the applied model's shape.
        /// </summary>
        [TestMethod]
        public void ExportManifest_RoundTripsThroughApply()
        {
            Manifest manifest = new Manifest
            {
                Elements = new List<ManifestElement>
                {
                    new ManifestElement { Alias = "P1", Kind = "process", Name = "Proc" },
                    new ManifestElement { Alias = "EXT", Kind = "external", Name = "Client" },
                },
                Flows = new List<ManifestFlow> { new ManifestFlow { From = "EXT", To = "P1", Name = "call" } },
            };
            ApplyResultDto applied = AuthoringService.Apply(manifest, force: false);

            Manifest exported = AuthoringService.ExportManifest(applied.Model);

            Assert.AreEqual(2, exported.Elements!.Count);
            Assert.AreEqual(1, exported.Flows!.Count);
        }

        private static TmForgeModelDto Add(TmForgeModelDto? model, StencilKind kind, string name, string alias)
        {
            AuthoringResultDto result = AuthoringService.Add(model, new AddRequest { Kind = kind, Name = name, Alias = alias });
            Assert.IsTrue(result.Success, result.Error);
            return result.Model!;
        }

        private static string? ResolveAlias(TmForgeModelDto model, string id)
        {
            TmForgeElementDto? element = model.Elements?.FirstOrDefault(candidate => candidate.Id == id);
            return element != null && element.Properties.TryGetValue("Alias", out string? alias) ? alias : null;
        }
    }
}
