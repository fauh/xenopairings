using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Xenopairings;
using Xenopairings.Data;
using Xenopairings.Models;
using Xenopairings.Services;
using Xenopairings.Services.Auth;
using Xenopairings.Services.Elo;
using Xenopairings.Services.Email;
using Xenopairings.Services.GitHub;
using Xenopairings.Services.Organizations;
using Xenopairings.Services.Players;
using Xenopairings.Services.Reminders;
using Xenopairings.Services.Reports;
using Xenopairings.Services.Rounds;
using Xenopairings.Services.Standings;
using Xenopairings.Services.Teams;
using Xenopairings.Services.TopCut;
using Xenopairings.Services.Tournaments;

var builder = WebApplication.CreateBuilder(args);

// ── Database — PostgreSQL ─────────────────────────────────────────────────────
// Railway sets individual PG* variables when a PostgreSQL service is linked.
// Build a plain Npgsql key=value connection string from them — avoids all URI
// parsing issues. Falls back to appsettings.json for local development.
var pgHost = Environment.GetEnvironmentVariable("PGHOST");
if (!string.IsNullOrWhiteSpace(pgHost))
{
    var connStr = new System.Text.StringBuilder()
        .Append("Host=").Append(pgHost).Append(';')
        .Append("Port=").Append(Environment.GetEnvironmentVariable("PGPORT") ?? "5432").Append(';')
        .Append("Database=").Append(Environment.GetEnvironmentVariable("PGDATABASE")).Append(';')
        .Append("Username=").Append(Environment.GetEnvironmentVariable("PGUSER")).Append(';')
        .Append("Password=").Append(Environment.GetEnvironmentVariable("PGPASSWORD")).Append(';')
        .Append("SSL Mode=Require;Trust Server Certificate=true")
        .ToString();
    builder.Configuration["ConnectionStrings:DefaultConnection"] = connStr;
}

// ── Forwarded headers ─────────────────────────────────────────────────────────
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── Rate limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("tournament-creation", httpCtx =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpCtx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromDays(1) }));
    options.AddPolicy("player-registration", httpCtx =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpCtx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromDays(1) }));
});

// ── Core services ─────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// DataProtection keys stored in PostgreSQL so they survive redeploys
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>();

// ── Reminders (disabled — no Hangfire in this deployment) ────────────────────
builder.Services.Configure<RemindersSettings>(
    builder.Configuration.GetSection(RemindersSettings.SectionName));
builder.Services.AddScoped<IReminderService, NullReminderService>();

// ── Email ─────────────────────────────────────────────────────────────────────
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));
var emailSettings = builder.Configuration.GetSection(EmailSettings.SectionName).Get<EmailSettings>()
    ?? new EmailSettings();

if (emailSettings.UseRealProvider)
    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>();
else
    builder.Services.AddScoped<IEmailSender, ConsoleEmailSender>();

// ── App services ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<TokenGenerator>();
builder.Services.AddSingleton<SlugGenerator>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.Configure<AdminSettings>(builder.Configuration.GetSection(AdminSettings.SectionName));
builder.Services.Configure<GitHubSettings>(builder.Configuration.GetSection(GitHubSettings.SectionName));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITournamentService, TournamentService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<StandingsService>();
builder.Services.AddScoped<TeamStandingsService>();
builder.Services.AddScoped<IRoundService, RoundService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IEloService, EloService>();
builder.Services.AddScoped<IPlayerReportService, PlayerReportService>();
builder.Services.AddScoped<ITopCutService, TopCutService>();
builder.Services.AddHttpClient<GitHubIssueService>();

// ── Cookie authentication ─────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.Name = "xp_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseRateLimiter();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapRazorPages();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
