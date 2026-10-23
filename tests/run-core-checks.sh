#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export ATR_TEST_ROOT="$(mktemp -d /tmp/atr-core-checks.XXXXXX)"
export XDG_DATA_HOME="$ATR_TEST_ROOT/data"
trap 'rm -rf "$ATR_TEST_ROOT"' EXIT
dotnet run --project tests/CoreChecks -c Release
