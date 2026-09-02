#!/usr/bin/env bash
# Builds the docs site using the shared Docs tool (sibling repo: ../docs).
#
# Output: dist/docs-site, which is COMMITTED, not gitignored. .gitignore has
# /dist/* followed by !/dist/docs-site/, so the site is tracked and the rest of
# dist/ is not. Regenerating without committing the result leaves a published
# site that disagrees with the markdown it was built from.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project ../docs/src/Docs -- docs dist/docs-site --site-name jaunty
