using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Xenopairings.Services.Auth;
using Xenopairings.Services.Elo;

namespace Xenopairings.Pages;

public class LoginModel(
    IAuthService authService,
    IEloService eloService,
    IOptions<AdminSettings> adminSettings) : PageModel
{
    public string? Email { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void OnGet(string? returnUrl = null) => ViewData["ReturnUrl"] = returnUrl;

    public async Task<IActionResult> OnPostAsync(string email, string password, string? returnUrl = null)
    {
        Email = email;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Email and password are required.";
            return Page();
        }

        var user = await authService.LoginAsync(email, password);
        if (user is null)
        {
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            BuildPrincipal(user.Email, user.Id, adminSettings.Value, user.EmailVerified));

        // Ensure verified users always have a leaderboard entry, even if they
        // verified their email before EnsureRatingAsync-on-verify was deployed.
        // EnsureRatingAsync is idempotent — no-op if the row already exists.
        if (user.EmailVerified)
        {
            var displayName = user.Email.Contains('@')
                ? user.Email[..user.Email.IndexOf('@')]
                : user.Email;
            await eloService.EnsureRatingAsync(user.Email, displayName);
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return Redirect("/dashboard");
    }

    internal static ClaimsPrincipal BuildPrincipal(
        string email, Guid userId, AdminSettings settings, bool emailVerified = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, email),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("email_verified", emailVerified ? "true" : "false"),
        };
        if (settings.IsAdmin(email))
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
