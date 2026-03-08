# Archived Documentation

Historical documents from Jaunty development, organized by time period.

---

## How to Use Archives

**Archives contain**:
- Superseded documents (replaced by current docs)
- Historical context (decisions, discussions, assessments)
- Temporary artifacts (benchmarks, screenshots from specific dates)

**DO NOT use archives for**:
- Current guidance (see docs outside `99-archive/`)
- Active development reference
- Production decisions

---

## Archive Periods

| Period | Contents | Purpose |
|--------|----------|---------|
| [`2026-01-early-development/`](2026-01-early-development/) | Assessments, brainstorming, API analysis | Initial project analysis and design |
| [`2026-02-code-quality/`](2026-02-code-quality/) | Code quality initiatives, inconsistencies | Quality improvement efforts |
| [`2026-02-nativeaot-migration/`](2026-02-nativeaot-migration/) | NativeAOT migration planning | AOT compilation support |
| [`2026-03-misc-artifacts/`](2026-03-misc-artifacts/) | Benchmark results, generated docs | Ongoing artifacts |

---

## Archive Naming Convention

Archives are organized by **content period** (when the content was created/relevant), not archive date:

```
99-archive/
├── 2026-01-early-development/     # Content from January 2026
├── 2026-02-code-quality/          # Content from February 2026
└── 2026-03-misc-artifacts/        # Ongoing/miscellaneous
```

**Why content period?**
- Easier to find by historical context ("that January assessment")
- Groups related items together
- Matches mental model of project phases

---

## File Naming in Archives

**Preserved as-is** - Archive files keep their original names even if they don't match current conventions.

**Why?**
- Historical accuracy
- Avoids breaking external references
- Preserves context of when they were created

---

## Moving Documents to Archive

When superseding a document:

1. **Create archive folder** (if new period): `YYYY-MM-description/`
2. **Move file**: Use `git mv` for proper tracking
3. **Update links**: Fix all references to new location
4. **Add manifest entry**: Update this README with the new archive

---

## Current Documentation

For current information, see:

- **[00-quick-start/](../00-quick-start/)** - Getting started
- **[01-api-reference/](../01-api-reference/)** - API documentation
- **[02-architecture/](../02-architecture/)** - Architecture docs
- **[03-development/](../03-development/)** - Development guides
- **[04-extensions/](../04-extensions/)** - Extension docs
- **[05-quality/](../05-quality/)** - Quality & testing
- **[06-releases/](../06-releases/)** - Release docs

---

**Last Updated**: March 2026  
**Maintained By**: Jaunty Contributors
