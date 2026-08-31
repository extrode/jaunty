# RealWorld/Conduit → Jaunty port (torture test artifact)

Reference snapshot of the files written/edited to port
`gothinkster/aspnetcore-realworld-example-app`'s (Conduit) entire data layer from EF Core onto
Jaunty, per `docs/jaunty-torture-test-handoff.md` (Part 3). See
`docs/jaunty-torture-test-gaps-log.md` for what this surfaced about Jaunty itself.

This is **not a buildable sample** — it's a snapshot of files that live inside a full Conduit
clone (gitignored from Jaunty's own history, since it's someone else's repo). Paths mirror their
location in that clone. Kept here so the actual port code survives independently of the local,
disposable vendor clone.

Unlike Part 1 (eShopOnWeb), Conduit's MediatR CQRS handlers compose EF Core `IQueryable<T>`
directly in every handler body (`context.Articles.Include(...).Where(...)`) rather than going
through a repository/specification seam — so this port touched every handler (19 of them across
Articles/Comments/Favorites/Followers/Profiles/Tags/Users), not just an infrastructure layer.

**Design decisions:**
- `ConduitDb` (`src/Conduit/Infrastructure/ConduitDb.cs`) replaces `ConduitContext` — wraps a
  single `IDbConnection` plus the ambient transaction each MediatR request runs inside
  (`ConduitDbTransactionPipelineBehavior`), exposing `Connection` for direct Jaunty fluent calls
  and `Options` (a `CommandOptions` carrying the active transaction) for every write.
- `ArticleQueries.LoadArticleGraphAsync`/`HydrateAsync` (`src/Conduit/Features/Articles/
  ArticleQueries.cs`) replace the EF `Include` chain (`ArticleExtensions.GetAllData()`) — loads
  the root Article row via a real query, then hydrates Author/ArticleTags/ArticleFavorites with
  one `WHERE ... IN (...)` query per relation, same pattern as
  `samples/torture-test-eshoponweb-port`'s `JauntyRepository<T>`.
- `Article` gained an explicit `AuthorId` column. The original relied on an EF *shadow* foreign
  key (`Article.Author` navigation-only, no scalar property) — a micro-ORM has no equivalent to
  EF's convention-based shadow-property inference, so the FK needs a real, explicit column.
- Composite-key join entities (`ArticleTag`, `ArticleFavorite`, `FollowedPeople`) use Jaunty's
  native composite `[Key]` support (two `[Key]`-attributed properties per class).
- Schema creation: `ConduitSchema.CreateTablesSql` (hand-written idempotent
  `CREATE TABLE IF NOT EXISTS`) replaces `Database.EnsureCreated()` — Jaunty has no
  migrations/schema-from-entity tooling.
- No DB-level cascade delete is relied on; `Articles/Delete.cs` explicitly deletes child
  Comments/ArticleTags/ArticleFavorites before deleting the Article row.
- Entities aren't source-generated for this port (no `[Table]`/`partial` + Jaunty source
  generator wiring) — `Jaunty.Extensions.Reflection`'s `UseReflectionMapping()` is called once
  in `ServicesExtensions.AddConduit()` instead.
- The EF-InMemory-backed `SliceFixture` test fixture is replaced with a real, disposable
  per-test SQLite file (`Pooling=False` in the connection string, connection explicitly closed
  before the file is deleted) — same substitution Part 1 made for its own EF-InMemory fixture.
  Two actual `[Fact]` test files (`Articles/DeleteTests.cs`, `Users/CreateTests.cs`) queried
  `ConduitContext` directly as part of their assertions (not just fixture scaffolding) and needed
  mechanical translation to Jaunty fluent queries; the assertions themselves are unchanged.

At last verification: builds clean, all 7 `Conduit.IntegrationTests` pass against SQLite. Live
HTTP smoke test (registration, article creation with tags, article detail with full graph
hydration, tag listing, profile lookup) confirmed working end-to-end through the real ASP.NET
DI/MediatR pipeline. `Directory.Packages.props` in the vendor clone dropped the 3
`Microsoft.EntityFrameworkCore.*` package versions and added `Microsoft.Data.Sqlite` (10.0.2)
plus an explicit `SQLitePCLRaw.bundle_e_sqlite3` override (3.0.3) to clear a known-vulnerability
NuGet audit failure on the version Microsoft.Data.Sqlite 10.0.2 pulls in transitively
(GHSA-2m69-gcr7-jv3q) — not reproduced here since it's a repo-root config file, not port code.
