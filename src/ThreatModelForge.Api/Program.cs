namespace ThreatModelForge.Api
{
    using System;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.HttpResults;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// The Threat Model Forge engine API host. Exposes a small, versioned <c>/v1</c> surface over
    /// the real .NET engine (validate, convert, read, report, detect) and serves the Studio SPA
    /// from <c>wwwroot</c> so the API and UI ship as one hosted artifact.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The application entry point.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // Allow the local React (Vite) dev server to call the API during development.
            builder.Services.AddCors(options =>
                options.AddDefaultPolicy(policy =>
                    policy.WithOrigins("http://localhost:5199")
                        .AllowAnyHeader()
                        .AllowAnyMethod()));

            // Publish the OpenAPI document (served at /openapi/v1.json). It is the single source of
            // truth for the API contract; the React client's types are generated from it.
            builder.Services.AddOpenApi();

            WebApplication app = builder.Build();
            app.UseCors();

            // Serve the built Studio SPA's static assets from wwwroot (populated by the build).
            app.UseStaticFiles();

            app.MapOpenApi();

            app.MapGet("/v1/health", () => TypedResults.Ok(new HealthStatusDto { Status = "ok" }))
                .WithName("GetHealth")
                .WithTags("System");
            app.MapGet("/v1/formats", () => TypedResults.Ok(EngineService.GetFormats()))
                .WithName("GetFormats")
                .WithTags("Formats");
            app.MapGet("/v1/stencils", () => TypedResults.Ok(EngineService.GetStencils()))
                .WithName("GetStencils")
                .WithTags("Catalog");
            app.MapGet("/v1/stencil-packs", () => TypedResults.Ok(EngineService.GetStencilPacks()))
                .WithName("GetStencilPacks")
                .WithTags("Catalog");
            app.MapGet("/v1/rules", () => TypedResults.Ok(EngineService.GetRules()))
                .WithName("GetRules")
                .WithTags("Catalog");
            app.MapGet("/v1/rule-packs", () => TypedResults.Ok(EngineService.GetRulePacks()))
                .WithName("GetRulePacks")
                .WithTags("Catalog");
            app.MapGet("/v1/property-schema", () => TypedResults.Ok(EngineService.GetPropertySchema()))
                .WithName("GetPropertySchema")
                .WithTags("Catalog");
            app.MapPost("/v1/model/validate", (TmForgeModelDto model) => TypedResults.Ok(EngineService.Validate(model)))
                .WithName("ValidateModel")
                .WithTags("Model");
            app.MapPost(
                "/v1/model/export/tm7",
                (TmForgeModelDto model) => TypedResults.File(EngineService.ExportTm7(model), "application/xml", "model.tm7"))
                .WithName("ExportModelTm7")
                .WithTags("Model");
            app.MapPost("/v1/model/convert", (TmForgeModelDto model, string to) =>
                {
                    byte[] bytes = EngineService.Convert(model, to);
                    (string contentType, string fileName) = DescribeConversion(to);
                    return TypedResults.File(bytes, contentType, fileName);
                })
                .WithName("ConvertModel")
                .WithTags("Model");
            app.MapPost(
                "/v1/model/read",
                (FileContentDto file) => TypedResults.Ok(
                    EngineService.ReadModel(Convert.FromBase64String(file.ContentBase64), file.FormatId)))
                .WithName("ReadModel")
                .WithTags("Model");
            app.MapPost("/v1/model/report", (TmForgeModelDto model, string format) =>
                {
                    bool svg = string.Equals(format, "svg", StringComparison.OrdinalIgnoreCase);
                    byte[] bytes = EngineService.Report(model, format);
                    return TypedResults.File(bytes, svg ? "image/svg+xml" : "text/html", svg ? "report.svg" : "report.html");
                })
                .WithName("ReportModel")
                .WithTags("Report");
            app.MapPost("/v1/detect", DetectFormat)
                .WithName("DetectFormat")
                .WithTags("Formats");

            // Serve the Studio SPA's entry document for any non-API path so client-side routes
            // resolve. The /v1 and /openapi endpoints are matched first, so this only catches the rest.
            app.MapFallbackToFile("index.html");

            app.Run();
        }

        private static Results<Ok<FormatDto>, NotFound> DetectFormat(FileContentDto file)
        {
            FormatDto? detected = EngineService.Detect(Convert.FromBase64String(file.ContentBase64));
            return detected is null ? TypedResults.NotFound() : TypedResults.Ok(detected);
        }

        private static (string ContentType, string FileName) DescribeConversion(string formatId)
        {
            switch (formatId)
            {
                case "drawio":
                    return ("application/xml", "model.drawio");
                case "vsdx":
                    return ("application/vnd.ms-visio.drawing", "model.vsdx");
                case "tmforge-json":
                    return ("application/json", "model.tmforge.json");
                default:
                    return ("application/xml", "model.tm7");
            }
        }
    }
}
