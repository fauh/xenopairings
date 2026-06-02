# CLAUDE.md

Quick orientation for any Claude instance starting work in this repo.

## What this is
**Xenopairings** — Warhammer 40K tournament manager. Organizers create tournaments, players sign up, the app generates Swiss-style pairings each round, players enter scores, and standings are displayed live. No user accounts; identity via tokens in URLs.

Stack: ASP.NET Core Blazor Server (.NET 10) · EF Core 10 · SQLite · Hangfire · Docker

## Read in this order
1. `docs/design.md` — source of truth
2. `docs/roadmap.md` — phased delivery
3. `docs/phases/` — per-phase plans
4. `change_log.md` — decision diary

## Current status
Phase 0 scaffolded. Phase 1 (tournament creation + player registration) next.

## Production environment
TBD — same Azure App Service pattern as consid_maltid. See docs/design.md §7 when set up.
