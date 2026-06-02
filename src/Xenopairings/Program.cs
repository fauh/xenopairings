using Hangfire;
using Hangfire.Storage.SQLite;
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
using Xenopairings.Services.Backups;
using Xenopairings.Services.Email;
using Xenopairings.Services.Players;
using Xenopairings.Services.Reminders;
using Xenopairings.Services.Rounds;
using Xenopairings.Services.Standings;
using Xenopairings.Services.Tournaments;

var builder = WebApplication.CreateBuilder(args);

// ── Connection string — DATA_DIR env var for container deployments ────────────
// Development default: DATA_DIR not set → "Data Source=xenopairings.db" (relative to CWD).
// Production: DATA_DIR=/home/data is set so SQLite writes to the mounted persistent volume.
var dataDir = Environment.GetEnvironmentVariable("DATA_DIR");
if (!string.IsNullOrWhiteSpace(dataDir))
{
    builder.Configuration["ConnectionStrings:DefaultConnection"] =
        $"Data Source={Path.Combine(dataDir, "xenopairings.db")}";

    // ── Data Protection keys — persist to the mounted volume ──────────────────
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "dataprotection-keys")));
}

// ── Forwarded headers — must run early so IP-based rate limiting sees real IPs ──
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── Rate limiting ────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 5 tournament-creation POSTs per IP per day
    options.AddPolicy("tournament-creation", httpCtx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpCtx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromDays(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    // 30 player registrations per IP per day
    options.AddPolicy("player-registration", httpCtx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpCtx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromDays(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
});

// ── Background jobs / Hangfire — conditionally disabled ─────────────────────
builder.Services.Configure<RemindersSettings>(
    builder.Configuration.GetSection(RemindersSettings.SectionName));

var remindersSettings = builder.Configuration
    .GetSection(RemindersSettings.SectionName)
    .Get<RemindersSettings>() ?? new RemindersSettings();

// ── Core services ────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Email ────────────────────────────────────────────────────────────────────
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection(EmailSettings.SectionName));

var emailSettings = builder.Configuration
    .GetSection(EmailSettings.SectionName)
    .Get<EmailSettings>() ?? new EmailSettings();

if (emailSettings.UseRealProvider)
{
    if (string.IsNullOrWhiteSpace(emailSettings.ApiKey))
        throw new InvalidOperationException(
            "EmailSettings:ApiKey must be set when EmailSettings:UseRealProvider is true.");

    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, ConsoleEmailSender>();
}

// ── Hangfire (only when enabled) ─────────────────────────────────────────────
if (remindersSettings.Enabled)
{
    var hangfireConnStr = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=xenopairings.db";
    builder.Services.AddHangfire(config =>
        config.UseSimpleAssemblyNameTypeSerializer()
              .UseRecommendedSerializerSettings()
              .UseSQLiteStorage(hangfireConnStr));
    builder.Services.AddHangfireServer();
}

builder.Services.AddScoped<IReminderService, NullReminderService>();

// ── Backup settings ──────────────────────────────────────────────────────────
builder.Services.Configure<BackupSettings>(
    builder.Configuration.GetSection(BackupSettings.SectionName));
builder.Services.AddTransient<BackupJob>();

// ── App services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<TokenGenerator>();
builder.Services.AddSingleton<SlugGenerator>();
builder.Services.AddScoped<ITournamentService, TournamentService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<StandingsService>();
builder.Services.AddScoped<IRoundService, RoundService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

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

if (app.Environment.IsDevelopment() && remindersSettings.Enabled)
{
    app.UseHangfireDashboard();
}

if (remindersSettings.Enabled)
{
    // Nightly backup at 03:00 UTC
    RecurringJob.AddOrUpdate<BackupJob>(
        "nightly-backup",
        job => job.RunAsync(CancellationToken.None),
        "0 3 * * *",
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
}

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapRazorPages();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
