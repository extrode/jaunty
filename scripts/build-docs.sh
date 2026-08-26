#!/usr/bin/env bash
# Builds the docs site using the shared Docs tool (sibling repo: ../docs).
# Was ../docsgen/src/Beparey.DocsGen until 2026-08-26; the repo and the project
# were both renamed in the extrode rebrand. Output: dist/docs-site (gitignored).
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project ../docs/src/Docs -- docs dist/docs-site --site-name jaunty
