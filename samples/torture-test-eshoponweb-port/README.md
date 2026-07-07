# eShopOnWeb → Jaunty port (torture test artifact)

Reference snapshot of the files written/edited to port `dotnet-architecture/eShopOnWeb`'s
Catalog/Basket/Order data layer from EF Core onto Jaunty, per
`docs/jaunty-torture-test-handoff.md` (Part 1). See
`docs/jaunty-torture-test-gaps-log.md` for what this surfaced about Jaunty itself.

This is **not a buildable sample** — it's a snapshot of files that live inside a full
eShopOnWeb clone (gitignored from Jaunty's own history, since it's someone else's repo).
Paths mirror their location in that clone. Kept here so the actual port code survives
independently of the local, disposable vendor clone.

At last verification: builds clean, 12/12 `FunctionalTests` pass against a Jaunty-backed
repository (SQLite in tests, real DB connection string in prod-shaped config). EF Core's
own `EfRepository`/`CatalogContext`/`Ardalis.Specification.EntityFrameworkCore` remain
present alongside this in the actual clone — `IntegrationTests` (out of scope for the
port) depends on them directly, so they weren't removed.
