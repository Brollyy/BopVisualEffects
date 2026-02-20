#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

chmod +x .githooks/pre-commit

git config core.hooksPath .githooks

echo "Git hooks enabled: .githooks/pre-commit"
