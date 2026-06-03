using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Xenopairings.Services.Auth;

namespace Xenopairings.Pages;

public class ResetPasswordModel(IAuthService authService) : PageModel
{
    public string? Token { get; private set; }
    public bool Success { get; private set; }
    public bool InvalidToken { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void OnGet(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) InvalidToken = true;
        else Token = token;
    }

    public async Task<IActionResult> OnPostAsync(string token, string password, string confirm)
    {
        Token = token;

        if (string.IsNullOrWhiteSpace(token)) { InvalidToken = true; return Page(); }
        if (password.Length < 8) { ErrorMessage = "Password must be at least 8 characters."; return Page(); }
        if (password != confirm) { ErrorMessage = "Passwords do not match."; return Page(); }

        var ok = await authService.ResetPasswordAsync(token, password);
        if (!ok) { InvalidToken = true; return Page(); }

        Success = true;
        return Page();
    }
}
