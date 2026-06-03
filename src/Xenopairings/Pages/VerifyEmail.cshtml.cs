using Microsoft.AspNetCore.Mvc.RazorPages;
using Xenopairings.Services.Auth;

namespace Xenopairings.Pages;

public class VerifyEmailModel(IAuthService authService) : PageModel
{
    public bool Success { get; private set; }

    public async Task OnGetAsync(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
            Success = await authService.VerifyEmailAsync(token);
    }
}
