# Phase 1 — Tournament core loop

**Status**: complete

## Goal

Deliver the first end-to-end working tournament: organizer creates rounds and pairings, scores are entered, standings update live. Players can also edit their army list.

## What was already working (Phase 0)

- Create tournament (`NewTournament.razor` + `TournamentService`)
- Success screen with manage URL copy (`TournamentCreated.razor`)
- Player registration (`TournamentPage.razor` + `PlayerService`)
- Drop player (`ManageTournament.razor`)
- Swiss pairing algorithm (`SwissPairingService` — pure logic, no DB)

## What was stubbed / missing

- `ManageTournament.ToggleRegistration` mutated in-memory only (TODO comment)
- No `RoundService` or way to create rounds / generate pairings
- No standings computation
- `TournamentPage` showed only the player list
- `EditRegistration` could withdraw but not update army list / faction

## Task breakdown

| # | Task | Status |
|---|---|---|
| 1.1 | `ITournamentService.SetRegistrationOpenAsync` | ✅ |
| 1.2 | Fix ManageTournament ToggleRegistration persistence | ✅ |
| 1.3 | `IPlayerService.UpdateRegistrationAsync` | ✅ |
| 1.4 | `IRoundService` + `RoundService` | ✅ |
| 1.5 | `StandingsService` | ✅ |
| 1.6 | Register services in Program.cs | ✅ |
| 1.7 | ManageTournament — round management section | ✅ |
| 1.8 | TournamentPage — standings + current round pairings | ✅ |
| 1.9 | EditRegistration — army list / faction editing | ✅ |
| 1.10 | Tests: RoundServiceTests (9 tests) | ✅ |
| 1.11 | Tests: StandingsServiceTests (5 tests) | ✅ |
| 1.12 | Docs: phase-1-plan.md + change_log.md | ✅ |

## Key implementation decisions

- **Bye auto-scoring**: bye matches (Player2 = null) are marked `IsScored = true` at creation time, awarding the bye player 1 win and 0 battle points. No organizer action needed.
- **Round creation guard**: `RoundService.CreateWithPairingsAsync` throws if the previous round is not complete, enforcing the Swiss flow.
- **Standings sort**: Wins desc, then TotalPoints desc. Draws (equal scores) count as 0 wins for both.
- **`@{var...}` in Razor**: Razor compiler RZ1010 — replaced with nested `@if` checks to avoid variable scope issues.
- **No migration needed**: all tables existed from Phase 0.

## What actually happened

All 12 tasks completed in one session. 31 tests pass (9 new RoundService, 5 new StandingsService + 17 existing). No schema changes required.
