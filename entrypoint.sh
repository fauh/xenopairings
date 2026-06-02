#!/bin/sh
set -e

# The persistent volume is mounted at /home/data after image layers are applied.
# Its ownership defaults to root on first mount. Fix it so appuser can write the
# SQLite database (and Hangfire storage, when enabled).
chown -R appuser:appuser /home/data 2>/dev/null || true

# Drop to non-root user and exec the app (replaces this shell process).
exec su-exec appuser dotnet Xenopairings.dll
