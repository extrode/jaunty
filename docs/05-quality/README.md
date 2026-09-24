# Quality & Testing

Documentation for Jaunty quality assurance, testing strategies, and code coverage.

---

## Reports

Dated, point-in-time reports (benchmarks, coverage gaps, audits) live in
[`../reports/`](../reports/), not here - this folder holds the living quality/testing guides,
`../reports/` holds the dated snapshots. See
[`../reports/production-readiness-2026-07-02.md`](../reports/production-readiness-2026-07-02.md)
for the production readiness assessment (July 2026).

## Code Coverage

- [`code-coverage.md`](code-coverage.md) - **How to run coverage, what `coverage.runsettings` does, and the 2026-08-27 baseline**
- [`../reports/coverage-gaps-2026-07-04.md`](../reports/coverage-gaps-2026-07-04.md) - Coverage gap inventory (July 2026; older dotCover analysis archived under `../archive/2026-02-code-coverage/`)

---

## Quality Standards

Jaunty maintains high quality through:

1. **High test coverage** - Target: 90%+ line coverage
2. **Performance benchmarks** - Regular performance testing
3. **Code review** - All changes reviewed against [CODE-REVIEW.md](../03-development/code-review-checklist.md)
4. **Documentation** - All public APIs documented

---

## For Contributors

### Before Submitting PRs

- [ ] All new code has test coverage
- [ ] Performance-sensitive code has benchmarks
- [ ] Documentation updated
- [ ] Code review checklist completed

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run specific test project
dotnet test tests/Extrode.Jaunty.Tests
```

---

## See Also

| Document | Purpose |
|----------|---------|
| [`../03-development/code-review-checklist.md`](../03-development/code-review-checklist.md) | Code review checklist |
| [`../06-releases/`](../06-releases/) | Release documentation |
