# Quality & Testing

Documentation for Jaunty quality assurance, testing strategies, and code coverage.

---

## Reports

- [`reports/production-readiness-2026-03-03.md`](reports/production-readiness-2026-03-03.md) - Production readiness assessment (March 2026)

## Code Coverage

- [`code-coverage/`](code-coverage/) - Code coverage analysis and targets

## Test Organization

- [`../../06-releases/tasklists/jaunty-reorganize-tests.md`](../../06-releases/tasklists/jaunty-reorganize-tests.md) - Test reorganization plan

---

## Quality Standards

Jaunty maintains high quality through:

1. **High test coverage** - Target: 90%+ line coverage
2. **Performance benchmarks** - Regular performance testing
3. **Code review** - All changes reviewed against [CODE-REVIEW.md](../../03-development/code-review-checklist.md)
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
dotnet test tests/Jaunty.Tests
```

---

## See Also

| Document | Purpose |
|----------|---------|
| [`../03-development/code-review-checklist.md`](../03-development/code-review-checklist.md) | Code review checklist |
| [`../06-releases/`](../06-releases/) | Release documentation |
