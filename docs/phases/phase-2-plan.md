# Phase 2 — Email + password login

**Status**: complete

## Goal

Add user accounts so organizers and players can find their tournaments without managing token URLs. Email + password login replaces the token-in-URL identity model.

## Key decisions

- **Auth**: ASP.NET Core cookie authentication. `IPasswordHasher<User>` (PBKDF2-SHA256) — no extra NuGet packages.
- **Token URLs retired**: `ManageToken` and `EditToken` columns stay in DB but stop being used for access control.
- **Razor Pages for login/register/logout**: cookie writes require an HTTP response, not a Blazor SignalR circuit. Consistent with `Error.cshtml` already in the project.
- **`CascadingAuthenticationState`** in `Routes.razor`: Blazor pages read session email via `[CascadingParameter] Task<AuthenticationState>`.

## Access rules

| Action | Requirement |
|---|---|
| Create tournament | Logged in — OrganizerEmail set from session |
| Manage tournament | Logged in AND session email == OrganizerEmail |
| Register for tournament | Logged in — email pre-filled from session |
| Edit registration | Logged in AND session email == Player.Email |
| View tournament / standings | Public — no auth required |

## Task breakdown

| # | Task | Status |
|---|---|---|
| 2.1 | `User` model + EF migration | ✅ |
| 2.2 | `IAuthService` + `AuthService` | ✅ |
| 2.3 | Wire auth in `Program.cs` + `Routes.razor` | ✅ |
| 2.4 | `Login.cshtml` + `.cshtml.cs` | ✅ |
| 2.5 | `Register.cshtml` + `.cshtml.cs` | ✅ |
| 2.6 | `Logout.cshtml` + `.cshtml.cs` | ✅ |
| 2.7 | `Dashboard.razor` + `ListByOrganizerEmailAsync` + `ListWithTournamentByEmailAsync` | ✅ |
| 2.8 | `NavMenu.razor` — conditional auth links via `<AuthorizeView>` | ✅ |
| 2.9 | `NewTournament.razor` — require auth, email from session | ✅ |
| 2.10 | `TournamentCreated.razor` — simplify (no manage link display) | ✅ |
| 2.11 | `ManageTournament.razor` — session email replaces token | ✅ |
| 2.12 | `TournamentPage.razor` — require login to register | ✅ |
| 2.13 | `EditRegistration.razor` — session email replaces edit token | ✅ |
| 2.14 | `AuthServiceTests` (9 tests) | ✅ |
| 2.15 | Docs | ✅ |

## What actually happened

All 15 tasks completed in one session. 41 tests pass (9 new AuthServiceTests + 32 existing).

Key implementation note: `RegModel.Email` field removed from `TournamentPage` form — email is now taken from session and passed directly to `RegisterAsync`, not bound through the form model.
