# Xenopairings

A Warhammer 40K tournament management app. Create events, pair rounds using Swiss pairings, track scores and standings, and follow player ELO ratings across seasons.

**Live at:** [xenopairings-production.up.railway.app](https://xenopairings-production.up.railway.app)

---

## Features

- Swiss-style pairings (random R1, then by wins/points, no repeat matchups)
- Individual and team event support
- GW standard and WTC differential scoring systems
- Live standings after each round
- Top-cut bracket generation
- ELO rating system with global leaderboard and seasonal resets
- Player profiles with full match history
- Organization management
- Organizer dashboard for army list tracking and player check-in
- Admin panel for user management and ELO corrections

## Tech stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core Blazor Server (.NET 10) |
| Database | PostgreSQL via EF Core 10 + Npgsql |
| Background jobs | Hangfire |
| Email | SMTP / Brevo HTTP API |
| Auth | Cookie auth (PBKDF2-SHA256) |
| Frontend | Bootstrap 5 + custom CSS (dark mode) |
| Tests | xUnit + Shouldly |
| Deployment | Railway (Docker) |

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- PostgreSQL (local install or Docker)

### Local setup

1. **Clone the repo**
   ```sh
   git clone https://github.com/fauh/xenopairings.git
   cd xenopairings
   ```

2. **Set up the database**

   The default connection string expects a local PostgreSQL instance:
   ```
   Host=localhost;Database=xenopairings;Username=postgres;Password=postgres
   ```
   Override it in `src/Xenopairings/appsettings.Development.json` or via environment variable:
   ```sh
   export ConnectionStrings__DefaultConnection="Host=localhost;..."
   ```

3. **Run the app**
   ```sh
   dotnet run --project src/Xenopairings
   ```
   The app will be at `https://localhost:7073`. Migrations are applied automatically on startup.

4. **Make yourself an admin**

   Add your email to `AdminSettings.AdminEmails` in `appsettings.Development.json`:
   ```json
   "AdminSettings": {
     "AdminEmails": ["you@example.com"]
   }
   ```

Email defaults to `console` provider in development — emails are printed to stdout, no SMTP needed.

## Running tests

```sh
dotnet test
```

Tests use an in-memory SQLite fixture and do not require a running database or email provider.

## Deployment

The app ships as a Docker image. Railway is the primary host — pushing to `main` triggers an automatic build and deploy.

**Required environment variables on the host:**

| Variable | Description |
|---|---|
| `DATABASE_URL` | PostgreSQL URL (injected automatically by Railway) |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `EmailSettings__Provider` | `brevo` \| `smtp` \| `console` |
| `EmailSettings__ApiKey` | Brevo API key (if using Brevo) |
| `EmailSettings__FromAddress` | Verified sender address |
| `EmailSettings__BaseUrl` | Public URL of the app (for email links) |

To build and run locally with Docker:
```sh
docker build -t xenopairings .
docker run -p 8080:8080 -e DATABASE_URL="..." xenopairings
```

## Contributing

1. Fork the repo and create a feature branch off `master`
2. Make your changes — keep PRs focused on one thing
3. Run `dotnet test` and ensure the build passes
4. Open a PR against `master`

The `docs/` folder has deeper design context if you want to understand the data model or architecture before making larger changes.

## License

[GNU Affero General Public License v3.0](LICENSE) — free to use, modify, and distribute, but any modified version deployed as a network service must also be open source under the same license.
