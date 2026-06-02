# Phase 3 — Team events + scoring systems

**Status**: complete (service + data layer; team UI deferred to Phase 3b)

## Goal

Add team-event support and configurable scoring systems (GW / WTC).

## What shipped

### Scoring systems
- `ScoringSystem` enum (`Gw` / `Wtc`)
- `WtcScoring` — pure static converter using the standard WTC 40K differential table (0–20 GP, always sums to 20)
- `StandingsService` updated to accept `ScoringSystem`; for WTC converts raw BPs → GPs, counts draws (GP == 10)
- `PlayerStanding` record gains `Draws` field
- `NewTournament.razor` — scoring system picker (GW / WTC radio group)

### Team events
- `Tournament.IsTeamEvent` + `Tournament.TeamSize` fields
- `Team` model — name, invite token, captain player
- `TeamMatchup` model — team-level pairing per round, `TableGroupStart`
- `Player.TeamId` (nullable FK), `Match.TeamMatchupId` (nullable FK)
- EF migration `AddTeamAndScoringSystem`
- `TeamStanding` record, `SwissTeamPairingService` (same algorithm as player Swiss)
- `TeamStandingsService` — aggregates BPs/GPs per team, applies WTC draw threshold `(TeamSize - 1)`
- `ITeamService` / `TeamService` — create team (with invite token), join team (with fullness guard), list teams
- `RoundService` branched: `CreateIndividualRoundAsync` (unchanged) + `CreateTeamRoundAsync` (creates `TeamMatchup` rows using `SwissTeamPairingService`); `CompleteRoundAsync` checks all team matchup individual matches are fully scored
- `IRoundService` extended: `GetTeamMatchupsAsync`, `AddMatchToTeamMatchupAsync`
- `NewTournament.razor` — team event toggle + team size input

### Tests
- `WtcScoringTests` — 17 tests covering all bands, direction, boundary values
- `SwissTeamPairingServiceTests` — 5 tests
- 68 total, 0 failures

## Deferred to Phase 3b
- `TournamentPage.razor` — team registration UI (create team / join team section)
- `JoinTeam.razor` — `/t/{slug}/join-team?code={token}`
- `ManageTournament.razor` — team matchup display + individual match management + GP display for WTC
- `Dashboard.razor` — team name in "Playing in" section
- `TeamStandingsServiceTests` (integration tests against DB)

## Key decisions

- **WTC draw threshold**: `diff ≤ (TeamSize - 1)` means a 3-player team needs a game-point difference of >2 to win a round. A perfect split (each team wins one player's match 11-9) = diff 2 → draw.
- **Byes in team events**: `TeamMatchup` with `Team2Id = null`; no individual matches; team gets a win in standings at round completion.
- **Raw scores stored**: `Match.Player1Score`/`Player2Score` always hold raw BPs. GP conversion happens at query time in `StandingsService` / `TeamStandingsService` — no extra DB columns.
- **`PlayerStanding.Draws`**: added to record; `SwissPairingService` ignores it (only uses `Wins` + `TotalPoints`).
