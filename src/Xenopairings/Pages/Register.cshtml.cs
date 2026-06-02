using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Xenopairings.Services.Auth;

namespace Xenopairings.Pages;

public class RegisterModel(IAuthService authService) : PageModel
{
    public string? Email { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(
        string email, string password, string confirmPassword)
    {
        Email = email;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Email and password are required.";
            return Page();
        }

        if (password.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters.";
            return Page();
        }

        if (password != confirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return Page();
        }

        try
        {
            var user = await authService.RegisterAsync(email, password);

            var claims = new List<Claim>
            {
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return Redirect("/dashboard");
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }
}
