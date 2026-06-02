using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Xenopairings.Services.Auth;

namespace Xenopairings.Pages;

public class LoginModel(IAuthService authService) : PageModel
{
    public string? Email { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void OnGet(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
    }

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

        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return Redirect("/dashboard");
    }
}
