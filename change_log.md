# Change log

Newest entry first.

## 2026-06-02 — Phase 1: tournament core loop

Full end-to-end tournament flow is now working. Organizers can create rounds, generate Swiss pairings, enter scores per match, and complete rounds. Standings and current round pairings are visible on the public tournament page. Players can edit their army list and faction from their edit-registration page.

New: `RoundService` (create round with pairings, enter scores, complete round), `StandingsService` (compute W/Pts from all scored matches), `ITournamentService.SetRegistrationOpenAsync`, `IPlayerService.UpdateRegistrationAsync`. 31 tests pass.

## 2026-06-02 — Phase 0: scaffold from consid_maltid
Xenopairings scaffolded from the consid_maltid codebase. Stack, infra, and identity patterns carried over. Domain models (Tournament, Player, Round, Match) created. Swiss-pairing service stubbed. Phase 1 (tournament creation + registration flow) is next.
