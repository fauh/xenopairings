#!/bin/sh
set -e

# Drop to non-root user and run the app.
# Database is PostgreSQL (Railway) — no local data directory needed.
exec su-exec appuser dotnet Xenopairings.dll
