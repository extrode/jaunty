#!/usr/bin/env bash
# Builds the docs site using the shared Extrode.DocsGen tool (sibling repo:
# ../docsgen.extrode.com). Output: dist/docs-site (gitignored).
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project ../docsgen.extrode.com/src/Extrode.DocsGen -- docs dist/docs-site --site-name jaunty
