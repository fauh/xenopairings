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
using Xenopairings.Services.Notifications;
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
// Railway injects DATABASE_URL as postgresql://user:pass@host:port/db.
// .NET's Uri class doesn't handle the postgresql:// scheme, so we swap it
// for https:// purely for parsing, then build a clean Npgsql key=value string.
// Falls back to individual PG* vars, then appsettings.json (local dev).
var rawDbUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("POSTGRES_URL");

if (!string.IsNullOrWhiteSpace(rawDbUrl))
{
    try
    {
        var parseableUrl = rawDbUrl
            .Replace("postgresql://", "https://", StringComparison.OrdinalIgnoreCase)
            .Replace("postgres://", "https://", StringComparison.OrdinalIgnoreCase);
        // Strip any existing query string — we set SSL options explicitly below
        var qIndex = parseableUrl.IndexOf('?');
        if (qIndex >= 0) parseableUrl = parseableUrl[..qIndex];

        var uri = new Uri(parseableUrl);
        var parts = uri.UserInfo.Split(':', 2);

        var connStr = $"Host={uri.Host};" +
                      $"Port={(uri.Port > 0 ? uri.Port : 5432)};" +
                      $"Database={uri.AbsolutePath.TrimStart('/')};" +
                      $"Username={Uri.UnescapeDataString(parts[0])};" +
                      $"Password={Uri.UnescapeDataString(parts.Length > 1 ? parts[1] : "")};" +
                      "SSL Mode=Require;Trust Server Certificate=true";

        builder.Configuration["ConnectionStrings:DefaultConnection"] = connStr;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[DB] Failed to parse DATABASE_URL: {ex.Message}");
        // Fall through to appsettings default
    }
}
else
{
    // Individual PG* vars (Railway also sets these if linked explicitly)
    var pgHost = Environment.GetEnvironmentVariable("PGHOST");
    if (!string.IsNullOrWhiteSpace(pgHost))
    {
        var connStr = $"Host={pgHost};" +
                      $"Port={Environment.GetEnvironmentVariable("PGPORT") ?? "5432"};" +
                      $"Database={Environment.GetEnvironmentVariable("PGDATABASE")};" +
                      $"Username={Environment.GetEnvironmentVariable("PGUSER")};" +
                      $"Password={Environment.GetEnvironmentVariable("PGPASSWORD")};" +
                      "SSL Mode=Require;Trust Server Certificate=true";
        builder.Configuration["ConnectionStrings:DefaultConnection"] = connStr;
    }
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

// Provider selection: "smtp" → MailKit SMTP, "resend" → Resend HTTP API, else console.
// Set EmailSettings__Provider in Railway to activate real sending.
switch (emailSettings.Provider.ToLowerInvariant())
{
    case "brevo":
        if (string.IsNullOrWhiteSpace(emailSettings.ApiKey))
            throw new InvalidOperationException(
                "EmailSettings:ApiKey must be set when EmailSettings:Provider is 'brevo'.");
        builder.Services.AddHttpClient<IEmailSender, BrevoEmailSender>();
        break;
    case "smtp":
        if (string.IsNullOrWhiteSpace(emailSettings.SmtpHost))
            throw new InvalidOperationException(
                "EmailSettings:SmtpHost must be set when EmailSettings:Provider is 'smtp'.");
        builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
        break;
    case "resend":
        if (string.IsNullOrWhiteSpace(emailSettings.ApiKey))
            throw new InvalidOperationException(
                "EmailSettings:ApiKey must be set when EmailSettings:Provider is 'resend'.");
        builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>();
        break;
    default:
        builder.Services.AddScoped<IEmailSender, ConsoleEmailSender>();
        break;
}

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
builder.Services.AddSingleton<TournamentNotificationService>();
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
