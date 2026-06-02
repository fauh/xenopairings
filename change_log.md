# Change log

Newest entry first.

## 2026-06-02 — Phase 2: email + password login

User accounts added. Email + password login via ASP.NET Core cookie auth (`IPasswordHasher<User>`, PBKDF2-SHA256). Token-in-URL identity retired — session email is now the sole credential.

New: `User` model + migration, `AuthService` (register/login/verify), `Login.cshtml`, `Register.cshtml`, `Logout.cshtml` Razor Pages, `/dashboard` (organizing + playing-in lists). NavMenu shows conditional sign-in/register vs dashboard/sign-out. `ManageTournament` and `EditRegistration` now check session email against `OrganizerEmail`/`Player.Email`. Tournament registration requires login; email is pre-filled read-only. 41 tests pass.

## 2026-06-02 — Phase 1: tournament core loop

Full end-to-end tournament flow is now working. Organizers can create rounds, generate Swiss pairings, enter scores per match, and complete rounds. Standings and current round pairings are visible on the public tournament page. Players can edit their army list and faction from their edit-registration page.

New: `RoundService` (create round with pairings, enter scores, complete round), `StandingsService` (compute W/Pts from all scored matches), `ITournamentService.SetRegistrationOpenAsync`, `IPlayerService.UpdateRegistrationAsync`. 31 tests pass.

## 2026-06-02 — Phase 0: scaffold from consid_maltid
Xenopairings scaffolded from the consid_maltid codebase. Stack, infra, and identity patterns carried over. Domain models (Tournament, Player, Round, Match) created. Swiss-pairing service stubbed. Phase 1 (tournament creation + registration flow) is next.
