#!/usr/bin/env bash
# Shared check used by pre-commit and pre-push: build + run ConstExpr.Tests, abort on failure.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)" || exit 1

LOG=$(mktemp)
if dotnet build ConstExpr.Tests/ConstExpr.Tests.csproj -c Debug -v quiet >"$LOG" 2>&1 \
  && TUNIT_DISABLE_HTML_REPORTER=true ./ConstExpr.Tests/bin/Debug/net11.0/ConstExpr.Tests --disable-logo --no-progress >>"$LOG" 2>&1; then
  rm -f "$LOG"
  exit 0
fi

echo "ConstExpr.Tests failed to build or has failing tests -- aborted." >&2
tail -n 40 "$LOG" >&2
rm -f "$LOG"
exit 1
