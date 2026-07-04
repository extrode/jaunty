# Releases & Planning

Release documentation, task lists, and planning documents for Jaunty.

---

## Release Runbook

- [`RELEASE-RUNBOOK.md`](RELEASE-RUNBOOK.md) - Step-by-step process for cutting and shipping a release

## Task Lists

Prioritized task lists for Jaunty development:

- [`tasklists/production-readiness-tasklist.md`](tasklists/production-readiness-tasklist.md) - Production readiness checklist
- [`tasklists/commercial-tasklist.md`](tasklists/commercial-tasklist.md) - Commercial analysis action items
- [`tasklists/jaunty-reorganize-tests.md`](tasklists/jaunty-reorganize-tests.md) - Test reorganization plan

## Reports

- [`../05-quality/reports/PRODUCTION-READINESS-2026-07-02.md`](../05-quality/reports/PRODUCTION-READINESS-2026-07-02.md) - Production readiness assessment

---

## Release Process

### Pre-Release Checklist

1. [ ] All tests passing
2. [ ] Code coverage targets met
3. [ ] Documentation updated
4. [ ] Performance benchmarks reviewed
5. [ ] Known limitations documented

### Version Numbering

Jaunty uses date-based versioning: `YYYY.MM.PATCH`

Example: `2026.03.01` = March 2026, patch 1

---

## For Contributors

### Planning Documents

Task lists in `tasklists/` contain prioritized work items. When contributing:

1. Review open task lists
2. Choose a task matching your skills
3. Create a branch for the task
4. Submit a PR referencing the task

### Release Notes

Release notes are generated from:
- Git commit messages
- Task list completion status
- Performance benchmark comparisons

---

## See Also

| Document | Purpose |
|----------|---------|
| [`../05-quality/README.md`](../05-quality/README.md) | Quality standards |
| [`../CONTRIBUTING.md`](../CONTRIBUTING.md) | Contribution guide |
