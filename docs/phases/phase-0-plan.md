# Phase 0 — Scaffold

**Status**: complete

## Goal
Copy the consid_maltid stack and patterns into a new repo, strip the food-order domain, add Xenopairings domain models and service stubs. Exit: `dotnet build` green, `dotnet test` green on the infrastructure tests.

## What actually happened
Scaffolded from consid_maltid. All four domain models created (Tournament, Player, Round, Match). AppDbContext wired with EF Core relationships. TournamentService and PlayerService implemented. SwissPairingService (pure logic) created with tests. EF migrations added. All tests pass.
