using System.Security.Claims;
using EndpointMonitoring.Core;
using EndpointMonitoring.Core.Data;
using EndpointMonitoring.Core.Models;
using EndpointMonitoring.Core.Providers.FritzBox;
using EndpointMonitoring.Core.Providers.Http;
using EndpointMonitoring.Core.Providers.Ping;
using EndpointMonitoring.Web.Auth;
using EndpointMonitoring.Web.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var dbPath = builder.Configuration["DatabasePath"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "EndpointMonitoring", "monitoring.db");

Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddEndpointMonitoringCore(dbPath);
builder.Services.AddMonitoringProvider<HttpMonitoringProvider>();
builder.Services.AddMonitoringProvider<PingMonitoringProvider>();
builder.Services.AddMonitoringProvider<FritzBoxMonitoringProvider>();

builder.Services.AddHttpClient("monitoring")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

builder.Services.AddMudServices();

// ---- Authentication & authorization ----------------------------------------------------
var oidc = builder.Configuration.GetSection(OidcOptions.SectionName).Get<OidcOptions>() ?? new OidcOptions();
builder.Services.AddSingleton(oidc);
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<UserAuthService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticationStateProvider, CookieRevalidatingAuthStateProvider>();
builder.Services.AddCascadingAuthenticationState();

var authBuilder = builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = AuthSchemes.Cookie;          // identity comes from the cookie
    o.DefaultSignInScheme = AuthSchemes.Cookie;    // OIDC result is persisted into the cookie
    o.DefaultChallengeScheme = AuthSchemes.Cookie; // unauthenticated -> local login (not OIDC)
})
.AddCookie(AuthSchemes.Cookie, c =>
{
    c.LoginPath = "/Account/Login";
    c.AccessDeniedPath = "/Account/AccessDenied";
    c.ExpireTimeSpan = TimeSpan.FromHours(8);
    c.SlidingExpiration = true;
    c.Cookie.Name = "EndpointMonitoring.Auth";
    c.Cookie.HttpOnly = true;
    c.Cookie.SameSite = SameSiteMode.Lax; // Lax required so the cookie survives the OIDC callback
});

if (oidc.Enabled)
{
    authBuilder.AddOpenIdConnect(AuthSchemes.Oidc, o =>
    {
        o.Authority = oidc.Authority;
        o.ClientId = oidc.ClientId;
        o.ClientSecret = oidc.ClientSecret;
        o.ResponseType = "code";
        o.UsePkce = true;
        o.SaveTokens = true;
        o.SignInScheme = AuthSchemes.Cookie;
        o.CallbackPath = "/signin-oidc";
        o.SignedOutCallbackPath = "/signout-callback-oidc";
        o.GetClaimsFromUserInfoEndpoint = true;
        o.MapInboundClaims = false;
        o.Scope.Clear();
        foreach (var scope in oidc.Scopes)
            o.Scope.Add(scope);
        o.TokenValidationParameters.NameClaimType = "name";
        o.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;

        o.Events.OnTokenValidated = async ctx =>
        {
            var principal = ctx.Principal!;
            var subject = principal.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(subject))
                return;

            var role = OidcRoleMapper.ResolveRole(principal, oidc);
            var email = principal.FindFirst("email")?.Value;
            var displayName = principal.FindFirst("name")?.Value
                ?? principal.FindFirst("preferred_username")?.Value;

            var users = ctx.HttpContext.RequestServices.GetRequiredService<UserAuthService>();
            var user = await users.EnsureExternalUserAsync(subject, email, displayName, role);

            // Normalize the identity so OIDC and local users carry the same claim shape:
            // NameIdentifier = DB id (consumed by the revalidating provider) and a single app role.
            var identity = (ClaimsIdentity)principal.Identity!;
            foreach (var existing in identity.FindAll(ClaimTypes.Role).ToList())
                identity.RemoveClaim(existing);
            foreach (var existing in identity.FindAll(ClaimTypes.NameIdentifier).ToList())
                identity.RemoveClaim(existing);
            identity.AddClaim(new Claim(ClaimTypes.Role, user.Role));
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        };
    });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole(AppRoles.Admin));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(options =>
    {
        // Keep disconnected circuits alive for 5 minutes so brief debugger
        // pauses or network hiccups don't lose the Blazor circuit.
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);
    });

builder.Services.AddSignalR(options =>
{
    // Give the client more time to respond before the server drops the connection.
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var factory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using (var ctx = factory.CreateDbContext())
    {
        ctx.Database.EnsureCreated();
        ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }

    // EnsureCreated does not add the Users table to a pre-existing DB; bootstrap it + seed admin.
    await AuthBootstrapper.EnsureUsersTableAndSeedAsync(
        factory,
        sp.GetRequiredService<IPasswordHasher<User>>(),
        app.Configuration,
        sp.GetRequiredService<ILoggerFactory>().CreateLogger("AuthBootstrap"));
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

// ---- Sign-in / sign-out endpoints ------------------------------------------------------
// SignInAsync cannot run inside an interactive circuit, so local sign-in happens in the
// static-SSR Login page and these endpoints handle sign-out and the OIDC challenge.
app.MapPost("/Account/Logout", async (HttpContext http) =>
{
    await http.SignOutAsync(AuthSchemes.Cookie);
    return Results.LocalRedirect("/Account/Login");
}).AllowAnonymous();

if (oidc.Enabled)
{
    app.MapGet("/Account/ExternalLogin", (string? returnUrl) =>
        Results.Challenge(
            new AuthenticationProperties { RedirectUri = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl },
            [AuthSchemes.Oidc])).AllowAnonymous();

    app.MapPost("/Account/ExternalLogout", async (HttpContext http) =>
    {
        await http.SignOutAsync(AuthSchemes.Cookie);
        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [AuthSchemes.Oidc]);
    });
}

app.Run();
