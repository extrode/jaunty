# File Naming Convention

Standard naming conventions for documentation files in Jaunty.

---

## Standard: `lowercase-with-hyphens.md`

All documentation files should use lowercase letters with hyphens as word separators.

### Examples

 **Correct**:
- `api-design-guidelines.md`
- `code-review-checklist.md`
- `architecture-specification.md`
- `production-readiness-2026-03-03.md`

 **Incorrect**:
- `API-DESIGN-GUIDELINES.md` (uppercase)
- `ApiDesignGuidelines.md` (PascalCase)
- `api_design_guidelines.md` (underscores)
- `apidesignguidelines.md` (no separators)

---

## Rationale

1. **GitHub Standard** - Matches GitHub wikis, Pages, and most open-source projects
2. **URL-Friendly** - Clean URLs when rendered (`/docs/api-design` not `/docs/APIDesign`)
3. **Cross-Platform** - Works consistently on:
   - Windows (case-insensitive filesystem)
   - Linux (case-sensitive filesystem)
   - macOS (case-insensitive by default)
4. **Sortable** - Alphabetical sorting works predictably
5. **Web Convention** - Matches web URL standards (all lowercase)
6. **Readable** - Hyphens provide clear word separation

---

## Exceptions (Keep As-Is)

### Universal Standards

These filenames are standardized across GitHub and should remain unchanged:

| Filename | Location | Reason |
|----------|----------|--------|
| `README.md` | Any folder | Universal documentation standard |
| `CONTRIBUTING.md` | Repo root | GitHub contribution guide |
| `LICENSE.md` | Repo root | GitHub license display |
| `CHANGELOG.md` | Repo root | GitHub changelog display |

### Dates in Filenames

When dates are part of the filename for sorting/versioning:

 **Correct**:
- `production-readiness-2026-03-03.md`
- `coverage-plan-2026-02-24.md`
- `benchmark-results-2026-01-15.md`

 **Incorrect**:
- `PRODUCTION-READINESS-2026-03-03.md` (uppercase)
- `2026-03-03-production-readiness.md` (date first - harder to scan)

### Archive Files

Historical documents in `archive/` should **keep their original names** to preserve historical accuracy and avoid breaking external references.

---

## Migration Guide

### For Existing Files

When renaming existing files:

1. **Use `git mv`** for proper rename tracking:
   ```bash
   git mv docs/API-DESIGN.md docs/api-design-guidelines.md
   ```

2. **Update all internal links** using search/replace:
   - Search: `API-DESIGN.md`
   - Replace: `api-design-guidelines.md`

3. **Test all links** before committing

4. **Commit with clear message**:
   ```
   docs: rename API-DESIGN.md to api-design-guidelines.md
   
   Following new file naming convention (lowercase-with-hyphens).
   Updated 15 internal links.
   ```

### For New Files

Always use the standard convention:

```bash
# Good
docs/03-development/my-new-guide.md

# Bad
docs/03-development/MyNewGuide.md
docs/03-development/MY-NEW-GUIDE.md
```

---

## Enforcement

### Pre-commit Checks

Consider adding a pre-commit hook to check file naming:

```bash
#!/bin/bash
# .git/hooks/pre-commit

# Check for uppercase .md files in docs/ (excluding exceptions)
if git diff --cached --name-only | grep -E '^docs/.*[A-Z].*\.md$' | grep -v 'README.md'; then
    echo "Error: Documentation files must use lowercase-with-hyphens naming"
    echo "See docs/03-development/file-naming-convention.md"
    exit 1
fi
```

### CI/CD Checks

Add a workflow check for new documentation:

```yaml
# .github/workflows/docs-lint.yml
name: Documentation Lint

on:
  pull_request:
    paths:
      - 'docs/**'

jobs:
  lint-docs:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Check file naming
        run: |
          # Check for uppercase in new/modified docs files
          # (implementation depends on your CI setup)
```

---

## Quick Reference

### Do's

- `api-design-guidelines.md`
- `code-review-checklist.md`
- `production-readiness-2026-03-03.md`
- `README.md` (exception)

### Don'ts

- `API-DESIGN-GUIDELINES.md`
- `ApiDesignGuidelines.md`
- `api_design_guidelines.md`
- `apidesignguidelines.md`

---

## Related Documents

- [`../README.md`](../README.md) - Documentation index
- [`../03-development/code-review-checklist.md`](code-review-checklist.md) - Code review standards
- [`../03-development/xml-documentation-style.md`](xml-documentation-style.md) - XML documentation guide

---

**Adopted**: March 2026  
**Applies To**: All documentation in `docs/` folder  
**Enforcement**: Manual review + CI/CD (planned)
