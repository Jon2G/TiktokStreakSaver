#!/usr/bin/env bash
# Wrapper — prefer ./src/ios/build.sh (see src/ios/README.md).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
exec "$ROOT/src/ios/build.sh" "$@"
