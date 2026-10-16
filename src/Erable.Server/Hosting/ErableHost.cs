using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Erable.Core;
using Erable.Server.Accounts;
using Erable.Server.Api;
using Erable.Server.Native;
using Erable.Server.Projects;
using Erable.Server.Realtime;
using Erable.Server.Security;
using Erable.Server.Spa;
using Erable.Server.Users;
using Erable.Storage;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;

namespace Erable.Server.Hosting;

/// <summary>Single entry point used by <c>erable serve</c> and by the desktop shell (in-process).</summary>
public static class ErableHost
{
    /// <summary>The only address the desktop backend may listen on (docs/architecture.md §9).</summary>
    public const string DesktopAddress = "127.0.0.1";

    /// <summary>Minimum session token length in characters (hex: 128 bits).</summary>
    public const int MinimumTokenLength = 32;

    /// <summary>OpenAPI <c>format</c> of element IDs, mapped to the branded <c>ElementId</c> type in TypeScript.</summary>
    public const string ElementIdSchemaFormat = "element-id";

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
        Validate(options);

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
        builder.Services.AddSingleton(ClockOf(options));
        builder.Services.TryAddSingleton<INativeBridge, UnavailableNativeBridge>();
        if (options.SelfTest)
        {
            builder.Services.AddSingleton<SelfTestChannel>();
        }
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<UserProfiles>();
        builder.Services.AddScoped(services =>
            services.GetRequiredService<UserProfiles>().For(services.GetRequiredService<IHttpContextAccessor>().HttpContext));
        if (options.Mode == HostMode.Server)
        {
            builder.Services.AddSingleton(_ => ServerStore.OpenOrCreate(options.DataDirectory
                ?? throw new InvalidOperationException("Server mode needs a data directory.")));
        }

        builder.Services.AddSingleton<AccountService>();
        builder.Services.AddSingleton<LoginThrottle>();
        builder.Services.AddSingleton<UserDirectory>();
        builder.Services.AddSingleton<ProjectWorkspace>();
        builder.Services.AddSingleton<ProjectCatalog>();
        builder.Services.AddHostedService<ServerStartup>();
        builder.Services.AddSingleton<AttachmentSettings>();
        builder.Services.AddSingleton<WindowCoordinator>();
        builder.Services.AddSingleton<InstanceRegistry>();
#if ERABLE_TEST_ENDPOINTS
        if (options.TestMode)
        {
            builder.Services.AddSingleton<Testing.TestSlots>();
        }
#endif
        builder.Services.AddProblemDetails();
        if (options.Mode == HostMode.Server)
        {
            builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
                .SetIsOriginAllowed(origin => IsAllowedOrigin(origin, options))
                .AllowAnyHeader()
                .AllowAnyMethod()
                // SignalR negotiates with credentials; the session cookie is SameSite=Strict, so it never travels cross-site.
                .AllowCredentials()
                .WithExposedHeaders(HeaderNames.ETag, HeaderNames.RetryAfter, HeaderNames.ContentRange, HeaderNames.AcceptRanges)));
        }

        if (options.TrustProxy)
        {
            builder.Services.Configure<ForwardedHeadersOptions>(forwarded =>
            {
                forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
                // The proxy is whatever is in front of the server (a container network): it is trusted by configuration.
                forwarded.KnownIPNetworks.Clear();
                forwarded.KnownProxies.Clear();
            });
        }

        builder.Services.AddSignalR(hub =>
        {
            // A browser closed without a goodbye (power cut, network lost) leaves its presence for at most this long.
            hub.KeepAliveInterval = options.HubKeepAlive;
            hub.ClientTimeoutInterval = options.HubClientTimeout;
        }).AddJsonProtocol(json =>
        {
            json.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            json.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });
        builder.Services.AddSingleton<ProjectConnections>();
        builder.Services.AddSingleton<ProjectEvents>();
        builder.Services.AddOpenApi(ApiRoutes.OpenApiDocumentName, openApi =>
        {
            openApi.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Erable API";
                document.Info.Version = InfoEndpoints.ApiVersion;
                // The server URL depends on the random port: keep the document stable for the snapshot and the client.
                document.Servers?.Clear();
                return Task.CompletedTask;
            });
            openApi.AddSchemaTransformer((schema, context, _) =>
            {
                // ElementId is carried by a custom JsonConverter, invisible to the generator: always a string
                // (docs/architecture.md §4). The format lets the TypeScript generator map it to the branded type.
                var type = context.JsonTypeInfo.Type;
                if (type == typeof(ElementId) || type == typeof(ElementId?))
                {
                    // A nullable ElementId? keeps (or gets) the null type.
                    var nullable = type == typeof(ElementId?) || ((schema.Type ?? 0) & JsonSchemaType.Null) != 0;
                    schema.Type = nullable ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
                    schema.Pattern = ElementId.Pattern;
                    schema.Format = ElementIdSchemaFormat;
                    schema.Properties?.Clear();
                    return Task.CompletedTask;
                }

                // Values and rules are free JSON whose shape depends on the property type (docs/architecture.md §5.4):
                // any JSON in the schema, read and checked by the SPA (src/shared/properties).
                if (type == typeof(System.Text.Json.Nodes.JsonObject))
                {
                    schema.Type = JsonSchemaType.Object;
                    schema.Properties?.Clear();
                    schema.AdditionalProperties = new OpenApiSchema();
                    return Task.CompletedTask;
                }

                // Enums are serialized as strings (JsonStringEnumConverter) but the generator omits their type.
                if (context.JsonTypeInfo.Type.IsEnum && schema.Enum is { Count: > 0 })
                {
                    schema.Type = JsonSchemaType.String;
                }

                return Task.CompletedTask;
            });
        });
        builder.Services.Configure<JsonOptions>(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            // Numbers are numbers: the web default also accepts strings, which leaks `integer | string` into the OpenAPI schema.
            json.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
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

    /// <summary>
    /// Per-user documents (recent projects, preferences…): the desktop data folder is the application profile itself;
    /// a server keeps them in <c>&lt;data&gt;/profile</c>.
    /// </summary>
    internal static string ProfileDirectory(ErableHostOptions options)
    {
        var data = options.DataDirectory ?? Path.Combine(Path.GetTempPath(), "erable-no-data");
        return options.Mode == HostMode.Desktop ? data : Path.Combine(data, "profile");
    }

    /// <summary>
    /// The backend clock. In test mode, unless a test supplies its own, a <see cref="Testing.TestClock"/> starting at the
    /// reference date of the e2e tests (docs/features.md §1.4), moved by <c>POST /_test/clock</c>.
    /// </summary>
    private static TimeProvider ClockOf(ErableHostOptions options)
    {
#if ERABLE_TEST_ENDPOINTS
        if (options.TestMode && ReferenceEquals(options.TimeProvider, TimeProvider.System))
        {
            return new Testing.TestClock(Testing.TestClock.ReferenceDate);
        }
#endif
        return options.TimeProvider;
    }

    /// <summary>An origin the API answers to from a browser: the public URL, the configured ones, and loopback pages if allowed.</summary>
    internal static bool IsAllowedOrigin(string origin, ErableHostOptions options)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        var normalized = uri.GetLeftPart(UriPartial.Authority);
        if (options.PublicUrl is { Length: > 0 } publicUrl
            && Uri.TryCreate(publicUrl, UriKind.Absolute, out var publicUri)
            && string.Equals(publicUri.GetLeftPart(UriPartial.Authority), normalized, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (options.CorsOrigins.Any(o => Uri.TryCreate(o, UriKind.Absolute, out var configured)
            && string.Equals(configured.GetLeftPart(UriPartial.Authority), normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return options.AllowLoopbackOrigins && uri.IsLoopback;
    }

    private static void Validate(ErableHostOptions options)
    {
        if (options.Mode != HostMode.Desktop)
        {
            return;
        }

        // Guard rail: a desktop backend reachable from the network would expose the user's projects.
        if (options.Address != DesktopAddress)
        {
            throw new InvalidOperationException(
                $"Desktop mode only listens on {DesktopAddress}; '{options.Address}' is refused.");
        }

        if (options.SessionToken is not { Length: >= MinimumTokenLength })
        {
            throw new InvalidOperationException(
                $"Desktop mode requires a session token of at least {MinimumTokenLength} characters.");
        }
    }

    private static void ConfigurePipeline(WebApplication app, ErableHostOptions options)
    {
        if (options.Mode == HostMode.Desktop)
        {
            app.UseMiddleware<DesktopSecurityMiddleware>();

            // F16.R2: advertise this instance while it runs, so that another one can bring it to the front.
            var registry = app.Services.GetRequiredService<InstanceRegistry>();
            app.Lifetime.ApplicationStarted.Register(() => registry.Register(GetListeningUri(app).Port));
            app.Lifetime.ApplicationStopping.Register(registry.Unregister);
        }

        if (options.TrustProxy)
        {
            app.UseForwardedHeaders();
        }

        app.UseExceptionHandler();
        if (options.Mode == HostMode.Server)
        {
            // Before the access rules: a preflight request carries no credentials.
            app.UseCors();
        }

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

        // After the static files, which need no identity: the session lookups are for API and shell requests only.
        app.UseMiddleware<AccessControlMiddleware>();

        // Wraps the endpoints below: once a write of a project is answered, its events go out to the other clients.
        app.UseMiddleware<ProjectEventsMiddleware>();

        app.MapOpenApi(ApiRoutes.OpenApiPattern).PublicAccess();
        app.MapInfoEndpoints();
        app.MapSessionEndpoints();
        app.MapSelfTestEndpoints();
        app.MapGroup(ApiRoutes.V1).MapAuthEndpoints().MapProjectEndpoints();
        // The hub itself needs a signed-in user; joining a project checks the membership.
        app.MapHub<ProjectHub>(ApiRoutes.ProjectHub).RequireSignIn();
        app.MapNativeEndpoints(options);

        if (TestEndpointsCompiled && options.TestMode)
        {
            MapTestEndpoints(app);
        }

        // Unknown API routes are real 404s, never the SPA shell.
        foreach (var prefix in ApiRoutes.NonSpaPrefixes)
        {
            app.MapFallback(prefix + "/{**path}", () => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)).PublicAccess();
        }

        // The SPA shell is rendered per request (theme attribute); an explicit endpoint also takes precedence
        // over the static file middleware for /index.html.
        app.MapGet("/index.html", SpaIndexEndpoint.HandleAsync).ExcludeFromDescription().PublicAccess();
        app.MapFallback(SpaIndexEndpoint.HandleAsync).PublicAccess();
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
