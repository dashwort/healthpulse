using ApexCharts;

using HealthTracker.Application.Abstractions;
using HealthTracker.Application.Services;
using HealthTracker.Infrastructure;
using HealthTracker.Infrastructure.Persistence;
using HealthTracker.Web.Authentication;
using HealthTracker.Web.Components;
using HealthTracker.Web.Configuration;
using HealthTracker.Web.Services;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
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
    );
var oidc =
    builder.Configuration.GetSection(ExternalOidcSettings.SectionName).Get<ExternalOidcSettings>()
    ?? new ExternalOidcSettings();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<HealthTrackerService>();
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
            foreach (var scope in oidc.Scopes)
            {
                options.Scope.Add(scope);
            }
        });
}
builder.Services.AddAuthorization();
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
app.UseAntiforgery();
if (usesDevelopmentAuthentication)
{
    app.MapGet("/login", () => Results.Redirect("/"));
    app.MapGet("/logout", () => Results.Redirect("/"));
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
    app.MapGet(
        "/logout",
        () =>
            Results.SignOut(
                new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                {
                    RedirectUri = "/",
                },
                [
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    OpenIdConnectDefaults.AuthenticationScheme,
                ]
            )
    );
}
app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
