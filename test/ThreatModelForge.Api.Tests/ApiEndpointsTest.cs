namespace ThreatModelForge.Api.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests the hosted <c>/v1</c> surface over real HTTP. The rest of this project drives
    /// <c>EngineService</c> directly, which leaves everything the host itself owns unexercised:
    /// routing, status codes, model binding, query-string handling, content types, and download file
    /// names. Those are the parts a client actually depends on, and none of them are visible from a
    /// facade-level test.
    /// </summary>
    [TestClass]
    public class ApiEndpointsTest
    {
        /// <summary>A minimal but real model: one process, no flows.</summary>
        private const string Model =
            "{\"schema\":\"tmforge-json\",\"version\":\"0.1\"," +
            "\"elements\":[{\"id\":\"a\",\"kind\":\"process\",\"name\":\"Alpha\",\"x\":10,\"y\":10,\"width\":120,\"height\":60}]," +
            "\"flows\":[]}";

        /// <summary>
        /// The in-memory host. <c>Program</c> is a static class and cannot be a type argument, so the
        /// factory is anchored on a public type from the same assembly — it only uses the type to
        /// locate that assembly's entry point.
        /// </summary>
        private static WebApplicationFactory<HealthStatusDto>? factory;

        private static HttpClient? client;

        /// <summary>Gets the shared client.</summary>
        private static HttpClient Client => client ?? throw new InvalidOperationException("Host not started.");

        /// <summary>Starts one host for the whole class; booting it per test would dominate the run.</summary>
        /// <param name="context">The MSTest context.</param>
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            factory = new WebApplicationFactory<HealthStatusDto>();
            client = factory.CreateClient();
        }

        /// <summary>Shuts the host down.</summary>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            client?.Dispose();
            factory?.Dispose();
        }

        /// <summary>Verifies the health probe the container smoke test and orchestrators depend on.</summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task Health_ReportsOk()
        {
            using HttpResponseMessage response = await Client.GetAsync("/v1/health");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.AreEqual("ok", body.RootElement.GetProperty("status").GetString());
        }

        /// <summary>
        /// Verifies every catalog route serves a non-empty collection. A non-empty body is what
        /// separates "the route is wired" from "the engine is actually behind it".
        /// </summary>
        /// <param name="route">The catalog route.</param>
        /// <returns>A task.</returns>
        [TestMethod]
        [DataRow("/v1/formats")]
        [DataRow("/v1/stencils")]
        [DataRow("/v1/stencil-packs")]
        [DataRow("/v1/rules")]
        [DataRow("/v1/rule-packs")]
        [DataRow("/v1/property-schema")]
        public async Task Catalogs_ServeNonEmptyCollections(string route)
        {
            using HttpResponseMessage response = await Client.GetAsync(route);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.AreEqual(JsonValueKind.Array, body.RootElement.ValueKind);
            Assert.IsTrue(body.RootElement.GetArrayLength() > 0, route + " served an empty catalog.");
        }

        /// <summary>
        /// Verifies the rule bundle is served with the shape the Studio reads. A default host loads no
        /// custom packs, so the pack list is legitimately empty here — <see cref="ApiCustomRulesTest"/>
        /// covers a host that has been given one.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task RuleBundle_IsServedWithNoCustomPacksByDefault()
        {
            using HttpResponseMessage response = await Client.GetAsync("/v1/rule-bundle");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.AreEqual(JsonValueKind.Array, body.RootElement.GetProperty("rulePacks").ValueKind);
            Assert.AreEqual(0, body.RootElement.GetProperty("rulePacks").GetArrayLength());
            Assert.AreEqual(0, body.RootElement.GetProperty("diagnostics").GetArrayLength());
        }

        /// <summary>Verifies the analysis routes accept a model and answer with JSON.</summary>
        /// <param name="route">The analysis route.</param>
        /// <returns>A task.</returns>
        [TestMethod]
        [DataRow("/v1/model/analyze")]
        [DataRow("/v1/model/analysis")]
        [DataRow("/v1/model/analysis-document")]
        [DataRow("/v1/model/threats")]
        [DataRow("/v1/model/threat-register")]
        public async Task ModelRoutes_AcceptAModelAndAnswerJson(string route)
        {
            using HttpResponseMessage response = await PostJson(route, Model);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.AreNotEqual(JsonValueKind.Null, body.RootElement.ValueKind);
        }

        /// <summary>Verifies a three-way merge is accepted in the shape the Studio posts it.</summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task Merge_AcceptsBaseOursAndTheirs()
        {
            string request = "{\"base\":" + Model + ",\"ours\":" + Model + ",\"theirs\":" + Model + "}";

            using HttpResponseMessage response = await PostJson("/v1/model/merge", request);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.AreEqual(JsonValueKind.Object, body.RootElement.ValueKind);
        }

        /// <summary>Verifies the .tm7 export is delivered as a downloadable XML document.</summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task ExportTm7_DownloadsXml()
        {
            using HttpResponseMessage response = await PostJson("/v1/model/export/tm7", Model);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("application/xml", response.Content.Headers.ContentType?.MediaType);
            Assert.AreEqual("model.tm7", response.Content.Headers.ContentDisposition?.FileName);

            // The MTMT root element, not merely well-formed XML: this is what makes the download a .tm7.
            StringAssert.StartsWith(await response.Content.ReadAsStringAsync(), "<ThreatModel");
        }

        /// <summary>
        /// Verifies each conversion target carries the content type and download name a browser needs.
        /// These pairings live only in the host, so nothing below it can catch them being swapped.
        /// </summary>
        /// <param name="format">The target format id.</param>
        /// <param name="contentType">The expected content type.</param>
        /// <param name="fileName">The expected download file name.</param>
        /// <returns>A task.</returns>
        [TestMethod]
        [DataRow("tm7", "application/xml", "model.tm7")]
        [DataRow("drawio", "application/xml", "model.drawio")]
        [DataRow("vsdx", "application/vnd.ms-visio.drawing", "model.vsdx")]
        [DataRow("tmforge-json", "application/json", "model.tmforge.json")]
        public async Task Convert_LabelsEachTargetFormat(string format, string contentType, string fileName)
        {
            using HttpResponseMessage response = await PostJson("/v1/model/convert?to=" + format, Model);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(contentType, response.Content.Headers.ContentType?.MediaType);
            Assert.AreEqual(fileName, response.Content.Headers.ContentDisposition?.FileName);
        }

        /// <summary>Verifies the threat-model report is served as HTML or SVG on request.</summary>
        /// <param name="format">The report format.</param>
        /// <param name="contentType">The expected content type.</param>
        /// <param name="fileName">The expected download file name.</param>
        /// <returns>A task.</returns>
        [TestMethod]
        [DataRow("html", "text/html", "report.html")]
        [DataRow("svg", "image/svg+xml", "report.svg")]
        public async Task Report_ServesTheRequestedRendering(string format, string contentType, string fileName)
        {
            using HttpResponseMessage response = await PostJson("/v1/model/report?format=" + format, Model);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(contentType, response.Content.Headers.ContentType?.MediaType);
            Assert.AreEqual(fileName, response.Content.Headers.ContentDisposition?.FileName);
        }

        /// <summary>
        /// Verifies the analysis evidence is named the way <c>tmforge analyze --reportFolder</c> names
        /// it, so a downloaded artifact drops straight into a review folder or a CI upload.
        /// </summary>
        /// <param name="format">The evidence format.</param>
        /// <param name="contentType">The expected content type.</param>
        /// <param name="fileName">The expected download file name.</param>
        /// <returns>A task.</returns>
        [TestMethod]
        [DataRow("sarif", "application/sarif+json", "findings.sarif")]
        [DataRow("json", "application/json", "findings.json")]
        [DataRow("html", "text/html", "findings.html")]
        public async Task AnalysisReport_ServesTheRequestedEvidence(string format, string contentType, string fileName)
        {
            using HttpResponseMessage response = await PostJson("/v1/model/analysis-report?format=" + format, Model);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(contentType, response.Content.Headers.ContentType?.MediaType);
            Assert.AreEqual(fileName, response.Content.Headers.ContentDisposition?.FileName);
        }

        /// <summary>Verifies SARIF served over HTTP is valid SARIF, not just bytes with a SARIF name.</summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task AnalysisReport_ServesRealSarif()
        {
            using HttpResponseMessage response = await PostJson("/v1/model/analysis-report?format=sarif", Model);

            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.AreEqual("2.1.0", body.RootElement.GetProperty("version").GetString());
            Assert.IsTrue(body.RootElement.GetProperty("runs").GetArrayLength() > 0);
        }

        /// <summary>Verifies an uploaded model is decoded from base64 and read back as a model.</summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task Read_DecodesAnUploadedModel()
        {
            string request = "{\"contentBase64\":\"" + Base64(Model) + "\",\"formatId\":\"tmforge-json\"}";

            using HttpResponseMessage response = await PostJson("/v1/model/read", request);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.AreEqual("Alpha", body.RootElement.GetProperty("elements")[0].GetProperty("name").GetString());
        }

        /// <summary>Verifies format detection answers with the format it recognized.</summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task Detect_IdentifiesAKnownFormat()
        {
            using HttpResponseMessage response = await PostJson("/v1/detect", "{\"contentBase64\":\"" + Base64(Model) + "\"}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.AreEqual("tmforge-json", body.RootElement.GetProperty("id").GetString());
        }

        /// <summary>
        /// Verifies unrecognized content is a 404 rather than a 200 carrying null. This is the only
        /// route with a two-result union, so it is the only one where that distinction can regress.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task Detect_ReportsNotFoundForUnrecognizedContent()
        {
            using HttpResponseMessage response = await PostJson("/v1/detect", "{\"contentBase64\":\"" + Base64("not a model") + "\"}");

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }

        /// <summary>
        /// Verifies malformed input is refused as a client error. Without this the host could start
        /// answering 500 for a bad request body and nothing would notice.
        /// </summary>
        /// <param name="body">The request body.</param>
        /// <returns>A task.</returns>
        [TestMethod]
        [DataRow("{ this is not json", DisplayName = "malformed JSON")]
        [DataRow("null", DisplayName = "null body")]
        public async Task Analyze_RejectsAnUnusableBody(string body)
        {
            using HttpResponseMessage response = await PostJson("/v1/model/analyze", body);

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        /// <summary>Verifies a required query parameter is enforced by the host, not by the engine.</summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task Convert_RequiresATargetFormat()
        {
            using HttpResponseMessage response = await PostJson("/v1/model/convert", Model);

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        /// <summary>
        /// Verifies the OpenAPI document is served, since the Studio's client types are generated from
        /// it and a host that stops publishing it breaks that generation silently.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task OpenApi_DocumentIsServed()
        {
            using HttpResponseMessage response = await Client.GetAsync("/openapi/v1.json");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.IsTrue(body.RootElement.GetProperty("paths").TryGetProperty("/v1/health", out _));
        }

        /// <summary>
        /// Documents that an unknown conversion target is answered with 500 rather than 400.
        /// <para>
        /// The target format is caller input, so a wrong one is a client error; the engine throws and
        /// nothing in the host translates it, so the caller is told the server failed. This test pins
        /// the current behaviour so it cannot drift further — <b>update it when the host starts
        /// classifying this as a 400</b>, which is what it should return.
        /// </para>
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task Convert_AnswersServerErrorForAnUnknownTarget()
        {
            using HttpResponseMessage response = await PostJson("/v1/model/convert?to=nonsense", Model);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        /// <summary>
        /// Documents that an unmatched <c>/v1</c> path is not answered as an API 404.
        /// <para>
        /// The SPA fallback is registered for every unmatched path, so in a shipped image (which always
        /// carries the built Studio) a mistyped API route returns the application shell with 200 and
        /// <c>text/html</c>. A client that parses the body as JSON fails confusingly instead of seeing
        /// the status it deserves. <b>Update this when unmatched <c>/v1</c> paths start returning a JSON
        /// 404</b>, which is what an API surface should do.
        /// </para>
        /// <para>
        /// The assertion is written against the invariant rather than the status code, because an
        /// API-only build (<c>-p:BuildStudio=false</c>) has no <c>wwwroot</c> and answers 404 instead.
        /// Both outcomes share the defect being pinned: the caller never gets a JSON error.
        /// </para>
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task UnknownV1Route_IsNotAnsweredAsAnApiError()
        {
            using HttpResponseMessage response = await Client.GetAsync("/v1/no-such-endpoint");

            Assert.AreNotEqual(
                "application/json",
                response.Content.Headers.ContentType?.MediaType,
                "an unmatched /v1 path should eventually answer with a JSON 404.");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                // The SPA is present: its shell is served in place of an API error.
                Assert.AreEqual("text/html", response.Content.Headers.ContentType?.MediaType);
            }
            else
            {
                Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            }
        }

        /// <summary>Base64-encodes UTF-8 text.</summary>
        /// <param name="text">The text.</param>
        /// <returns>The encoded text.</returns>
        private static string Base64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

        /// <summary>Posts a JSON body to a route.</summary>
        /// <param name="route">The route.</param>
        /// <param name="body">The JSON body.</param>
        /// <returns>The response.</returns>
        private static async Task<HttpResponseMessage> PostJson(string route, string body)
        {
            using StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
            return await Client.PostAsync(route, content);
        }
    }
}
