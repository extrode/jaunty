# Releases & Planning

Release documentation and planning documents for Jaunty. The task lists, the pricing page and
the market-analysis papers are held in the private work repository and are not published here.

---

## Release Runbook

- [`RELEASE-RUNBOOK.md`](RELEASE-RUNBOOK.md) - Step-by-step process for cutting and shipping a release

## Commercial

**Jaunty is free to use, including in commercial production. What is sold is support**, and
its terms are available on request rather than published here. To ask, open a
[support enquiry](https://github.com/extrode/jaunty/issues/new?template=support-enquiry.yml).

- [`order-form-template.md`](order-form-template.md) - Order form template
- [`continuity-rider-template.md`](continuity-rider-template.md) - Escrow and continuity rider
- [`feature-gap-analysis.md`](feature-gap-analysis.md) - Feature gaps against competing ORMs

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

The prioritized work items live in the private work repository. When contributing, open an
issue describing the change first, then branch and submit a PR referencing it.

### Release Notes

Release notes are generated from:
- Git commit messages
- Performance benchmark comparisons

---

## See Also

| Document | Purpose |
|----------|---------|
| [`../05-quality/README.md`](../05-quality/README.md) | Quality standards |
| [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md) | Contribution guide |
