# Releases & Planning

Release documentation, task lists, and planning documents for Jaunty.

---

## Release Runbook

- [`RELEASE-RUNBOOK.md`](RELEASE-RUNBOOK.md) - Step-by-step process for cutting and shipping a release

## Task Lists

Prioritized task lists for Jaunty development:

- [`tasklists/PRODUCTION-READINESS-TASKLIST.md`](tasklists/PRODUCTION-READINESS-TASKLIST.md) - Production readiness checklist
- [`tasklists/COMMERCIAL-TASKLIST.md`](tasklists/COMMERCIAL-TASKLIST.md) - Commercial analysis action items
- [`tasklists/ENTERPRISE-READINESS-2026-07-04.md`](tasklists/ENTERPRISE-READINESS-2026-07-04.md) - Enterprise readiness assessment
- [`tasklists/jaunty-reorganize-tests.md`](tasklists/jaunty-reorganize-tests.md) - Test reorganization plan

## Commercial

- [`pricing.md`](pricing.md) - The commercial model. **Jaunty is free; support is sold.** Decided 2026-08-29
- [`order-form-template.md`](order-form-template.md) - Order form template
- [`continuity-rider-template.md`](continuity-rider-template.md) - Escrow and continuity rider
- [`feature-gap-analysis.md`](feature-gap-analysis.md) - Feature gaps against competing ORMs
- [`COMMERCIAL-ANALYSIS-REPORT.md`](COMMERCIAL-ANALYSIS-REPORT.md) - Market analysis. Pricing sections superseded by the owner's decision of 2026-08-29; see [`pricing.md`](pricing.md)
- [`COMMERCIAL-DISTRIBUTION-FLOW.md`](COMMERCIAL-DISTRIBUTION-FLOW.md) - **Stale.** Its premise is a private release feed and a paid Jaunty; both are retired. Awaiting the owner's call on rewrite or removal

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

Jaunty uses **semantic versioning**. The current version is `1.0.0-rc.2`, set in
`src/Directory.Build.props` and tagged `v1.0.0-rc.2`.

An earlier date-based scheme (`YYYY.MM.PATCH`) was documented here and never shipped.

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
| [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md) | Contribution guide |
