# Change log

Newest entry first.

## 2026-06-02 — Phase 3: team events + scoring systems (service + data layer)

**Scoring systems**: `ScoringSystem` enum (GW / WTC). `WtcScoring` pure static converter (standard WTC 40K differential → 0–20 GP, always sums to 20). `StandingsService` updated to accept scoring system and count draws. `PlayerStanding` gains `Draws` field. `NewTournament` scoring picker.

**Team events**: `Tournament.IsTeamEvent` + `TeamSize`. `Team` + `TeamMatchup` models. `Player.TeamId` + `Match.TeamMatchupId` (both nullable). EF migration. `TeamStanding` record. `SwissTeamPairingService` (same algorithm as individual, operates on teams). `TeamStandingsService` — aggregates per-matchup BPs/GPs, applies WTC draw threshold `(TeamSize - 1)`. `ITeamService`/`TeamService` — create (with invite token) + join (with fullness guard). `RoundService` branched for individual vs team rounds. `IRoundService` extended with `GetTeamMatchupsAsync` + `AddMatchToTeamMatchupAsync`. `NewTournament` team event toggle + team size input. 68 tests, 0 failures. Team UI (tournament page registration, manage page team matchups, join team page) deferred to Phase 3b.

## 2026-06-02 — Phase 2: email + password login

User accounts added. Email + password login via ASP.NET Core cookie auth (`IPasswordHasher<User>`, PBKDF2-SHA256). Token-in-URL identity retired — session email is now the sole credential.

New: `User` model + migration, `AuthService` (register/login/verify), `Login.cshtml`, `Register.cshtml`, `Logout.cshtml` Razor Pages, `/dashboard` (organizing + playing-in lists). NavMenu shows conditional sign-in/register vs dashboard/sign-out. `ManageTournament` and `EditRegistration` now check session email against `OrganizerEmail`/`Player.Email`. Tournament registration requires login; email is pre-filled read-only. 41 tests pass.

## 2026-06-02 — Phase 1: tournament core loop

Full end-to-end tournament flow is now working. Organizers can create rounds, generate Swiss pairings, enter scores per match, and complete rounds. Standings and current round pairings are visible on the public tournament page. Players can edit their army list and faction from their edit-registration page.

New: `RoundService` (create round with pairings, enter scores, complete round), `StandingsService` (compute W/Pts from all scored matches), `ITournamentService.SetRegistrationOpenAsync`, `IPlayerService.UpdateRegistrationAsync`. 31 tests pass.

## 2026-06-02 — Phase 0: scaffold from consid_maltid
Xenopairings scaffolded from the consid_maltid codebase. Stack, infra, and identity patterns carried over. Domain models (Tournament, Player, Round, Match) created. Swiss-pairing service stubbed. Phase 1 (tournament creation + registration flow) is next.
