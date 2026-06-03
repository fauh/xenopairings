using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Xenopairings.Services.Auth;

namespace Xenopairings.Pages;

public class ForgotPasswordModel(IAuthService authService) : PageModel
{
    public bool Sent { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ErrorMessage = "Email is required.";
            return Page();
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        await authService.SendPasswordResetAsync(email, baseUrl);
        Sent = true;
        return Page();
    }
}
