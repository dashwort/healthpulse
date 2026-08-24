using ApexCharts;

using HealthTracker.Application.Abstractions;
using HealthTracker.Application.Services;
using HealthTracker.Infrastructure;
using HealthTracker.Infrastructure.Persistence;
using HealthTracker.Web.Authentication;
using HealthTracker.Web.Components;
using HealthTracker.Web.Configuration;
using HealthTracker.Web.Services;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;

using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder
    .Services.AddDataProtection()
    .PersistKeysToFileSystem(
        new DirectoryInfo(
            Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys")
        )
    );
builder
    .Configuration.AddJsonFile("settings.json", optional: false, reloadOnChange: true)
    .AddJsonFile(
        $"settings.{builder.Environment.EnvironmentName}.json",
        optional: true,
        reloadOnChange: true
    )
    .AddEnvironmentVariables();
var oidc =
    builder.Configuration.GetSection(ExternalOidcSettings.SectionName).Get<ExternalOidcSettings>()
    ?? new ExternalOidcSettings();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<HealthTracker.Web.Mcp.HealthPulseMcpTools>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<MobileReleaseService>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<AccessControlAuthorizationHandler>();
builder.Services.AddScoped<HealthTrackerService>();
builder.Services.AddScoped<PersonalAccessTokenService>();
builder.Services.AddScoped<MobileAuthenticationService>();
builder.Services.AddMudServices();
builder.Services.AddHostedService<SoftDeletionPurgeService>();
builder.Services.AddApexCharts();
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("HealthTracker")
        ?? throw new InvalidOperationException("ConnectionStrings:HealthTracker is required.")
);
var usesDevelopmentAuthentication =
    builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(oidc.Authority);
if (usesDevelopmentAuthentication)
{
    builder
        .Services.AddAuthentication("Development")
        .AddScheme<
            Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
            DevelopmentAuthenticationHandler
        >("Development", _ => { });
}
else
{
    if (
        string.IsNullOrWhiteSpace(oidc.Authority)
        || string.IsNullOrWhiteSpace(oidc.ClientId)
        || string.IsNullOrWhiteSpace(oidc.ClientSecret)
    )
    {
        throw new InvalidOperationException(
            "OpenID Connect Authority, ClientId, and ClientSecret must be configured outside development."
        );
    }

    builder
        .Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(options =>
        {
            options.Authority = oidc.Authority;
            options.ClientId = oidc.ClientId;
            options.ClientSecret = oidc.ClientSecret;
            options.CallbackPath = oidc.CallbackPath;
            options.ResponseType = "code";
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = "name",
            };
            options.Events = new OpenIdConnectEvents
            {
                OnTokenValidated = async context =>
                {
                    var email =
                        context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                        ?? context.Principal?.FindFirst("email")?.Value;
                    var emailVerified = context.Principal?.FindFirst("email_verified")?.Value;
                    if (
                        string.IsNullOrWhiteSpace(email)
                        || !string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        context.Fail("Sign-in failed.");
                        return;
                    }

                    var dataStore = context.HttpContext.RequestServices.GetRequiredService<IHealthDataStore>();
                    if (
                        await dataStore.FindAllowedUserByEmailAsync(
                            email.Trim().ToUpperInvariant(),
                            false,
                            context.HttpContext.RequestAborted
                        ) is null
                    )
                    {
                        context.Fail("Sign-in failed.");
                    }
                },
                OnRemoteFailure = context =>
                {
                    context.Response.Redirect("/Error");
                    context.HandleResponse();
                    return Task.CompletedTask;
                },
            };
            foreach (var scope in oidc.Scopes)
            {
                options.Scope.Add(scope);
            }
        });
}
builder.Services.AddAuthorization(options =>
{
    var interactiveAuthenticationScheme = usesDevelopmentAuthentication
        ? "Development"
        : CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultPolicy = new AuthorizationPolicyBuilder(interactiveAuthenticationScheme)
        .RequireAuthenticatedUser()
        .AddRequirements(new ActiveAllowedUserRequirement())
        .Build();
    options.AddPolicy(
        "MobileApiUser",
        policy =>
            policy
                .AddAuthenticationSchemes(interactiveAuthenticationScheme, "MobileBearer")
                .RequireAuthenticatedUser()
                .AddRequirements(new ActiveAllowedUserRequirement())
    );
    options.AddPolicy(
        "InteractiveUser",
        policy =>
            policy
                .AddAuthenticationSchemes(interactiveAuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new ActiveAllowedUserRequirement())
    );
    options.AddPolicy(
        "Administrator",
        policy =>
            policy
                .AddAuthenticationSchemes(interactiveAuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new ActiveAllowedUserRequirement(), new AdministratorRequirement())
    );
});
builder.Services.AddAuthentication()
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, PersonalAccessTokenAuthenticationHandler>("PersonalAccessToken", _ => { })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, MobileBearerAuthenticationHandler>("MobileBearer", _ => { });
builder.Services.AddScoped<IAuthorizationHandler>(
    serviceProvider => serviceProvider.GetRequiredService<AccessControlAuthorizationHandler>()
);
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<HealthTracker.Web.Mcp.McpAuditAndDailyLimitMiddleware>();
app.UseAntiforgery();
if (usesDevelopmentAuthentication)
{
    app.MapGet("/login", () => Results.Redirect("/"));
    app.MapPost("/logout", () => Results.Redirect("/"))
        .RequireAuthorization()
        .WithMetadata(new RequireAntiforgeryTokenAttribute(true));
}
else
{
    app.MapGet(
        "/login",
        () =>
            Results.Challenge(
                new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                {
                    RedirectUri = "/",
                },
                [OpenIdConnectDefaults.AuthenticationScheme]
            )
    );
    app.MapPost(
        "/logout",
        () =>
            Results.SignOut(
                new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                {
                    RedirectUri = "/signed-out",
                },
                [CookieAuthenticationDefaults.AuthenticationScheme]
            )
    )
        .RequireAuthorization()
        .WithMetadata(new RequireAntiforgeryTokenAttribute(true));
}
app.MapStaticAssets();
app.MapGet(
    "/.well-known/healthpulse-mobile",
    (HttpRequest request) =>
        Results.Ok(
            new
            {
                product = "HealthPulse",
                apiVersion = 1,
                apiBaseUrl = $"{request.Scheme}://{request.Host}",
                authorizationEndpoint = "/api/mobile/auth/authorize",
                tokenEndpoint = "/api/mobile/auth/token",
                updateEndpoint = "/.well-known/healthpulse-android-update",
            }
        )
).AllowAnonymous();
app.MapGet(
    "/.well-known/healthpulse-android-update",
    async (MobileReleaseService mobileReleaseService, CancellationToken cancellationToken) =>
        Results.Ok(await mobileReleaseService.GetLatestAsync(cancellationToken))
).AllowAnonymous();
app.MapControllers();
app.MapMcp("/mcp").RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "PersonalAccessToken" });
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
