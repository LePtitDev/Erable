using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Erable.Server.Api;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Net.Http.Headers;

namespace Erable.Server.Hosting;

/// <summary>Single entry point used by <c>erable serve</c> and by the desktop shell (in-process).</summary>
public static class ErableHost
{
    /// <summary>True when the <c>/_test</c> endpoints are compiled in (never in Release builds).</summary>
    public static bool TestEndpointsCompiled =>
#if ERABLE_TEST_ENDPOINTS
        true;
#else
        false;
#endif

    /// <summary>Builds a configured, not yet started, backend.</summary>
    /// <param name="options">Host options.</param>
    /// <param name="configure">Last-chance builder customization (tests use it to plug a test server).</param>
    public static WebApplication Build(ErableHostOptions options, Action<WebApplicationBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ErableHost).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = options.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            EnvironmentName = options.EnvironmentName,
        });

        builder.WebHost.UseKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Listen(IPAddress.Parse(options.Address), options.Port);
        });

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(options.TimeProvider);
        builder.Services.AddProblemDetails();
        builder.Services.Configure<JsonOptions>(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });

        configure?.Invoke(builder);

        var app = builder.Build();
        ConfigurePipeline(app, options);
        return app;
    }

    /// <summary>Address actually bound once the host is started (resolves port 0).</summary>
    public static Uri GetListeningUri(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
        var first = addresses?.FirstOrDefault()
            ?? throw new InvalidOperationException("The host is not listening; call StartAsync first.");
        return new Uri(first);
    }

    private static void ConfigurePipeline(WebApplication app, ErableHostOptions options)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                // Vite emits content-hashed file names under /assets; everything else must be revalidated.
                var immutable = context.Context.Request.Path.StartsWithSegments("/assets", StringComparison.Ordinal);
                context.Context.Response.Headers[HeaderNames.CacheControl] =
                    immutable ? "public, max-age=31536000, immutable" : "no-cache";
            },
        });

        app.MapInfoEndpoints();
        app.MapGroup(ApiRoutes.V1);

        if (TestEndpointsCompiled && options.TestMode)
        {
            MapTestEndpoints(app);
        }

        // Unknown API routes are real 404s, never the SPA shell.
        foreach (var prefix in ApiRoutes.NonSpaPrefixes)
        {
            app.MapFallback(prefix + "/{**path}", () => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound));
        }

        app.MapFallbackToFile("index.html", new StaticFileOptions
        {
            OnPrepareResponse = context => context.Context.Response.Headers[HeaderNames.CacheControl] = "no-cache",
        });
    }

    private static void MapTestEndpoints(WebApplication app)
    {
#if ERABLE_TEST_ENDPOINTS
        Testing.TestEndpoints.Map(app);
#else
        _ = app;
#endif
    }
}
