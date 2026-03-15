using Orion.Core.Models;
using Orion.Core.Services;
using Orion.Core.Jobs;
using Orion.Api.Proxy;
using Yarp.ReverseProxy.Configuration;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.DataProtection;
using Orion.Api.Security;
using Orion.Api.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// Phase 11: Configure Kestrel for mTLS
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
    });
});

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Register Orion Services
builder.Services.AddSingleton<IMetadataService>(new SqliteMetadataService("orion_metadata.db"));
builder.Services.AddSingleton<ITelemetryService>(new DuckDbTelemetryService("orion_telemetry.db"));
builder.Services.AddSingleton<IStorageService, SeaweedStorageService>();
builder.Services.AddSingleton<ILogService, LogService>();

// Phase 16: WorkOS & Auth
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Orion.Auth";
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api") || 
                context.Request.Path.StartsWithSegments("/dashboard") ||
                context.Request.Path.StartsWithSegments("/auth/user"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<WorkOS.SSOService>(sp => 
{
    var apiKey = builder.Configuration["WorkOS:ApiKey"] ?? "sk_test_placeholder";
    return new WorkOS.SSOService(new WorkOS.WorkOSClient(new WorkOS.WorkOSOptions { ApiKey = apiKey }));
});


// Phase 11: HSM & Data Protection
builder.Services.AddDataProtection()
    .SetApplicationName("Orion")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), "keys")));
builder.Services.AddSingleton<IHsmProvider, DefaultHsmProvider>();

builder.Services.AddSingleton<ISecurityService>(sp => 
    new SecurityService(sp.GetRequiredService<IHsmProvider>()));

builder.Services.AddSingleton<ISecretService, SecretService>();
builder.Services.AddSingleton<IMetricsService, MetricsService>();
builder.Services.AddSingleton<IDracoTelemetryExporter, DracoTelemetryExporter>();
builder.Services.AddSingleton<IScaleService, ScaleService>();
builder.Services.AddSingleton<IContainerService, WasmtimeService>(); // Next-Gen WASM Edge Runtime
builder.Services.AddSingleton<IJobDispatcher>(new JobDispatcher());
builder.Services.AddTransient<IBuildService, BuildService>();
builder.Services.AddSingleton<IManifestService, ManifestService>();
builder.Services.AddSingleton<IMeshService, MeshService>();
builder.Services.AddSingleton<IPilotService, OrionPilotService>();
builder.Services.AddHostedService<JobWorker>();
builder.Services.AddHostedService<AutoscaleWorker>();
builder.Services.AddHostedService<TelemetryExporterWorker>();
builder.Services.AddHostedService<ManifestWorker>();
builder.Services.AddHostedService<PilotWorker>();

// Architecture-aware initialization log
var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
Console.WriteLine($@"
   ____       _               _____ _                 _ 
  / __ \     (_)             / ____| |               | |
 | |  | |_ __ _  ___  _ __  | |    | | ___  _   _  __| |
 | |  | | '__| |/ _ \| '_ \ | |    | |/ _ \| | | |/ _` |
 | |__| | |  | | (_) | | | || |____| | (_) | |_| | (_| |
  \____/|_|  |_|\___/|_| |_| \_____|_|\___/ \__,_|\__,_|
                                                        
 [HYBRID ORION] {arch} Control Plane booting...
 [SECURITY] Zero-Trust Mesh Enabled (Headscale Topology)
 [COMPUTE] WASM Edge Runtime Activated (Wasmtime)
");

// YARP Setup
var proxyConfigProvider = new DynamicProxyConfigProvider();
builder.Services.AddSingleton<IProxyConfigProvider>(proxyConfigProvider);
builder.Services.AddReverseProxy();

// Configure JSON serialization for Native AOT compatibility
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, Orion.Core.Serialization.OrionJsonContext.Default);
});

var app = builder.Build();

// Phase 11: Edge WAF
app.UseMiddleware<WafMiddleware>();

// Initialize the databases
using (var scope = app.Services.CreateScope())
{
    var metadata = scope.ServiceProvider.GetRequiredService<IMetadataService>();
    var telemetry = scope.ServiceProvider.GetRequiredService<ITelemetryService>();
    var meshService = scope.ServiceProvider.GetRequiredService<IMeshService>();
    await metadata.InitializeAsync();
    await telemetry.InitializeAsync();
    await meshService.InitializeAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

// Background Proxy Refresher
_ = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IMetadataService>();
            var apps = await db.GetAppsAsync();
            var instances = await db.GetActiveInstancesAsync();
            proxyConfigProvider.Update(apps, instances);
        }
        catch { /* Log error */ }
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
});

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

// Auth API
app.MapGet("/auth/login", (IConfiguration config) =>
{
    var clientId = config["WorkOS:ClientId"] ?? "client_placeholder";
    var redirectUri = "http://localhost:3000/auth/callback";
    
    // AuthKit Universal Login: Manually constructing URL to bypass SDK version limits
    var url = $"https://api.workos.com/sso/authorize?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&provider=authkit";
    
    return Results.Redirect(url);
});

app.MapGet("/auth/callback", async (string code, IHttpClientFactory httpClientFactory, IConfiguration config, HttpContext context) =>
{
    var clientId = config["WorkOS:ClientId"] ?? "client_placeholder";
    var apiKey = config["WorkOS:ApiKey"] ?? "sk_test_placeholder";
    var redirectUri = "http://localhost:3000/auth/callback";

    Console.WriteLine($"[AUTH] Exchanging code for token... Code: {code.Substring(0, Math.Min(5, code.Length))}...");

    using var client = httpClientFactory.CreateClient();
    var response = await client.PostAsJsonAsync("https://api.workos.com/user_management/authenticate", new
    {
        client_id = clientId,
        client_secret = apiKey,
        code = code,
        grant_type = "authorization_code"
    });

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[AUTH] Error: User Management authentication failed. Status: {response.StatusCode}, Error: {error}");
        return Results.Unauthorized();
    }

    var result = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
    var userNode = result?["user"];

    if (userNode == null)
    {
        Console.WriteLine("[AUTH] Error: User data not found in response.");
        return Results.Unauthorized();
    }

    var userId = userNode["id"]?.ToString() ?? "";
    var email = userNode["email"]?.ToString() ?? "";
    var firstName = userNode["first_name"]?.ToString() ?? "";
    var lastName = userNode["last_name"]?.ToString() ?? "";
    var picture = userNode["profile_picture_url"]?.ToString() ?? "";

    Console.WriteLine($"[AUTH] Successfully authenticated user: {email} (ID: {userId})");

    var claims = new List<System.Security.Claims.Claim>
    {
        new(System.Security.Claims.ClaimTypes.NameIdentifier, userId),
        new(System.Security.Claims.ClaimTypes.Email, email),
        new("workos_id", userId),
        new("display_name", $"{firstName} {lastName}".Trim()),
        new("picture", picture)
    };

    var identity = new System.Security.Claims.ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new System.Security.Claims.ClaimsPrincipal(identity);

    await context.SignInAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, principal);

    return Results.Redirect("http://localhost:3000/");
});

app.MapGet("/auth/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("http://localhost:3000/");
});

app.MapGet("/auth/user", (System.Security.Claims.ClaimsPrincipal user) =>
{
    if (user.Identity?.IsAuthenticated == true)
    {
        return Results.Ok(new
        {
            IsAuthenticated = true,
            UserId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            Email = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            Name = user.FindFirst("display_name")?.Value,
            Picture = user.FindFirst("picture")?.Value
        });
    }
    return Results.Ok(new { IsAuthenticated = false });
});

// Helper to get UserId
string? GetUserId(System.Security.Claims.ClaimsPrincipal user) => user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

// API Endpoints
app.MapGet("/apps", async (System.Security.Claims.ClaimsPrincipal user, IMetadataService db) =>
{
    var userId = GetUserId(user);
    var apps = await db.GetAppsAsync(userId);
    return Results.Ok(apps);
})
.WithName("GetApps")
.WithOpenApi()
.RequireAuthorization();

app.MapGet("/apps/{id}", async (Guid id, System.Security.Claims.ClaimsPrincipal user, IMetadataService db) =>
{
    var userId = GetUserId(user);
    var app = await db.GetAppByNameAsync("", userId); // Hacky if we only have ID, let's use GetApps and filter
    var apps = await db.GetAppsAsync(userId);
    var appModel = apps.FirstOrDefault(a => a.Id == id);
    return appModel != null ? Results.Ok(appModel) : Results.NotFound();
})
.WithName("GetApp")
.WithOpenApi()
.RequireAuthorization();

app.MapPut("/apps/{id}", async (Guid id, App updatedApp, System.Security.Claims.ClaimsPrincipal user, IMetadataService db) =>
{
    var userId = GetUserId(user);
    var existingApp = (await db.GetAppsAsync(userId)).FirstOrDefault(a => a.Id == id);
    if (existingApp == null) return Results.NotFound();

    existingApp.Name = updatedApp.Name;
    existingApp.RepoUrl = updatedApp.RepoUrl;
    existingApp.BuildCommand = updatedApp.BuildCommand;
    existingApp.RunCommand = updatedApp.RunCommand;
    existingApp.BuildFolder = updatedApp.BuildFolder;

    await db.UpdateAppAsync(existingApp);
    return Results.Ok(existingApp);
})
.WithName("UpdateApp")
.WithOpenApi()
.RequireAuthorization();

app.MapPost("/apps/explore", async (ExploreRequest request, IBuildService builder) =>
{
    if (string.IsNullOrEmpty(request.RepoUrl)) return Results.BadRequest("RepoUrl is required.");
    var dirs = await builder.GetRepoDirectoriesAsync(request.RepoUrl);
    return Results.Ok(dirs);
})
.WithName("ExploreRepo")
.WithOpenApi()
.RequireAuthorization();

app.MapPost("/apps", async (App app, System.Security.Claims.ClaimsPrincipal user, IMetadataService db) =>
{
    app.OwnerId = GetUserId(user) ?? "";
    await db.CreateAppAsync(app);
    return Results.Created($"/apps/{app.Id}", app);
})
.WithName("CreateApp")
.WithOpenApi()
.RequireAuthorization();

app.MapGet("/apps/{id}/deployments", async (Guid id, System.Security.Claims.ClaimsPrincipal user, IMetadataService db) =>
{
    var userId = GetUserId(user);
    var deployments = await db.GetDeploymentsAsync(id, userId);
    return Results.Ok(deployments);
})
.WithName("GetDeployments")
.WithOpenApi()
.RequireAuthorization();

app.MapPost("/deployments", async (Deployment deployment, System.Security.Claims.ClaimsPrincipal user, IMetadataService db) =>
{
    deployment.OwnerId = GetUserId(user) ?? "";
    await db.CreateDeploymentAsync(deployment);
    return Results.Created($"/deployments/{deployment.Id}", deployment);
})
.WithName("CreateDeployment")
.WithOpenApi()
.RequireAuthorization();

app.MapPost("/apps/{id}/build", async (Guid id, System.Security.Claims.ClaimsPrincipal user, IMetadataService db, IJobDispatcher dispatcher) =>
{
    var userId = GetUserId(user);
    var apps = await db.GetAppsAsync(userId);
    var app = apps.FirstOrDefault(a => a.Id == id);
    if (app == null) return Results.NotFound();

    var deployment = new Deployment
    {
        AppId = app.Id,
        OwnerId = userId ?? "",
        Status = DeploymentStatus.Pending
    };

    await db.CreateDeploymentAsync(deployment);

    var buildJob = new BuildJob
    {
        Id = deployment.Id,
        AppId = app.Id,
        AppName = app.Name,
        RepoUrl = app.RepoUrl,
        BuildCommand = app.BuildCommand,
        RunCommand = app.RunCommand,
        BuildFolder = app.BuildFolder,
        OwnerId = userId ?? ""
    };

    await dispatcher.EnqueueAsync(buildJob);

    return Results.Accepted($"/apps/{id}/deployments", deployment);
})
.WithName("TriggerBuild")
.WithOpenApi()
.RequireAuthorization();

app.MapGet("/apps/{id}/logs", async (HttpContext context, Guid id, System.Security.Claims.ClaimsPrincipal user, ILogService logService, CancellationToken ct) =>
{
    var userId = GetUserId(user);
    context.Response.ContentType = "text/event-stream";
    
    // Ownership check for logs is complex in stream, usually checked at start
    // For now, only stream if authenticated. 
    // TODO: In production, verify app ownership before starting stream.

    // 1. Send recent logs first
    var recentLogs = logService.GetRecentLogs(id); 
    foreach (var log in recentLogs)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(log, Orion.Core.Serialization.OrionJsonContext.Default.LogEntry);
        await context.Response.WriteAsync($"data: {json}\n\n", ct);
    }

    // 2. Subscribe to new logs
    EventHandler<LogEntry> handler = async (sender, log) =>
    {
        if (log.AppId == id || log.DeploymentId == id)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(log, Orion.Core.Serialization.OrionJsonContext.Default.LogEntry);
                await context.Response.WriteAsync($"data: {json}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
            catch { /* Client disconnected */ }
        }
    };

    logService.OnLogReceived += handler;
    
    try 
    {
        await Task.Delay(-1, ct); 
    }
    catch (OperationCanceledException) { }
    finally
    {
        logService.OnLogReceived -= handler;
    }
})
.WithName("StreamLogs")
.RequireAuthorization();

app.MapGet("/apps/{id}/secrets", async (Guid id, System.Security.Claims.ClaimsPrincipal user, ISecretService secretService) =>
{
    var userId = GetUserId(user);
    var secrets = await secretService.GetSecretsAsync(id, decrypt: false);
    // Ideally ISecretService should take userId too, but for now we filter in metadata inside it if possible
    // or we check ownership here.
    
    var masked = secrets.ToDictionary(k => k.Key, v => "**********");
    return Results.Ok(masked);
})
.WithName("GetSecrets")
.WithOpenApi()
.RequireAuthorization();

app.MapPost("/apps/{id}/secrets", async (Guid id, Dictionary<string, string> secrets, System.Security.Claims.ClaimsPrincipal user, ISecretService secretService) =>
{
    // Need ownership check
    foreach (var kvp in secrets)
    {
        await secretService.SetSecretAsync(id, kvp.Key, kvp.Value);
    }
    return Results.Accepted();
})
.WithName("SetSecrets")
.WithOpenApi()
.RequireAuthorization();

app.MapDelete("/apps/{id}/secrets/{key}", async (Guid id, string key, System.Security.Claims.ClaimsPrincipal user, ISecretService secretService) =>
{
    await secretService.DeleteSecretAsync(id, key);
    return Results.NoContent();
})
.WithName("DeleteSecret")
.WithOpenApi()
.RequireAuthorization();

app.MapPost("/apps/{id}/scale", async (Guid id, int replicas, System.Security.Claims.ClaimsPrincipal user, IScaleService scaleService) =>
{
    await scaleService.ScaleAsync(id, replicas);
    return Results.Accepted();
})
.WithName("ScaleApp")
.WithOpenApi()
.RequireAuthorization();

app.MapGet("/apps/{id}/metrics", async (Guid id, System.Security.Claims.ClaimsPrincipal user, IMetricsService metricsService) =>
{
    var metrics = await metricsService.GetMetricsAsync(id);
    return Results.Ok(metrics);
})
.WithName("GetMetrics")
.WithOpenApi()
.RequireAuthorization();

app.MapGet("/apps/{id}/telemetry", async (Guid id, System.Security.Claims.ClaimsPrincipal user, ITelemetryService telemetry) =>
{
    var userId = GetUserId(user);
    var metrics = await telemetry.GetMetricsAsync(id, userId);
    return Results.Ok(metrics);
})
.WithName("GetTelemetry")
.WithOpenApi()
.RequireAuthorization();

app.MapGet("/dashboard/summary", async (System.Security.Claims.ClaimsPrincipal user, IMetadataService db, IMetricsService metricsService, IServiceProvider sp) =>
{
    var userId = GetUserId(user);
    var apps = (await db.GetAppsAsync(userId)).Where(a => !a.Name.Equals("ScaleApp", StringComparison.OrdinalIgnoreCase));
    var allInstances = await db.GetActiveInstancesAsync(userId);
    var peers = await db.GetPeersAsync(); // Peers are usually global infrastructure
    
    var summary = new DashboardSummary
    {
        TotalApps = apps.Count(),
        TotalInstances = allInstances.Count(),
        ConnectedPeers = peers.Count(p => p.Status == "Online"),
        ControlPlaneArch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
        PilotStatus = (await sp.GetRequiredService<IPilotService>().AnalyzeHealthAsync()).PilotStatus
    };

    foreach (var app in apps)
    {
        var metrics = await metricsService.GetMetricsAsync(app.Id);
        var appInstances = allInstances.Where(i => i.AppId == app.Id);
        
        summary.Apps.Add(new AppSummary
        {
            Id = app.Id,
            Name = app.Name,
            Status = appInstances.Any() ? "Running" : "Idle",
            ActiveReplicas = appInstances.Count(),
            CpuUsage = metrics.CpuUsage,
            MemoryUsageMb = metrics.MemoryUsageMb
        });
    }

    return Results.Ok(summary);
})
.WithName("GetDashboardSummary")
.WithOpenApi()
.RequireAuthorization();

app.MapGet("/pilot/report", async (IPilotService pilot) =>
{
    var report = await pilot.AnalyzeHealthAsync();
    return Results.Ok(report);
})
.WithName("GetPilotReport")
.WithOpenApi();

app.Run();
