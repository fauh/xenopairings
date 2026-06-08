using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Xenopairings.Services.Auth;
using Xenopairings.Services.Elo;

namespace Xenopairings.Pages;

public class VerifyEmailModel(
    IAuthService authService,
    IEloService eloService,
    IOptions<AdminSettings> adminSettings) : PageModel
{
    public bool Success { get; private set; }

    public async Task OnGetAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;

        Success = await authService.VerifyEmailAsync(token);

        if (Success)
        {
            // If the user is currently signed in, refresh the auth cookie so the
            // email_verified claim updates immediately and the nav banner disappears
            // without requiring a logout/login cycle.
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (email is not null)
            {
                var user = await authService.GetByEmailAsync(email);
                if (user is not null)
                {
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        LoginModel.BuildPrincipal(user.Email, user.Id, adminSettings.Value, emailVerified: true));
                }

                // Ensure the player has a leaderboard entry from day one.
                // Uses the local-part of their email as the initial display name;
                // the user can update it later on the Account page.
                var displayName = email.Contains('@') ? email[..email.IndexOf('@')] : email;
                await eloService.EnsureRatingAsync(email, displayName);
            }
        }
    }
}
