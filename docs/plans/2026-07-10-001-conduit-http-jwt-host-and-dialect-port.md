# Conduit HTTP + JWT host (Phase A), then 5-dialect port (Phase B)

## Context

Part 3 of the torture-test effort (`docs/jaunty-torture-test-handoff.md`) already built a
SQLite-only, data-layer-only Conduit/RealWorld sample (`samples/JauntyQ.Conduit.Sqlite.Tests/`,
37/37 tests passing, merged to `dev` as `a7061c6`) — repositories called directly in xUnit,
no HTTP, auth reduced to a SHA-256 hash-compare stand-in. The user asked to go further: stand
up a **real ASP.NET Core host implementing the full RealWorld HTTP+JWT spec** on top of this
data layer (Phase A), then **port the whole thing across all 5 dialects** (SQL Server/Postgres/
MySQL/MariaDB/SQLite) matching the rigor of Parts 1 (eShopOnWeb) and 2 (Sakila) (Phase B).

This plan covers **Phase A in full detail**; Phase B is outlined at the end and will be
re-confirmed once Phase A is green, following the exact "fully duplicated project, no shared
library" pattern already established by the eShopOnWeb sample family.

Confirmed decisions (via user Q&A):
- Extend `samples/JauntyQ.Conduit.Sqlite.Tests` **in-place** — do not create a separate project.
- `GET /articles/:slug/comments` returns the official spec's bare `{"comments": [...]}` (no count).
- Duplicate article slug → reject with **422** `{"errors": {"title": ["must be unique"]}}`.

## Existing surface being built on (already implemented, verified via code read)

- Domain records: `Profile(Username, Bio, Image, Following)`, `ArticleView(Slug, Title,
  Description, Body, TagList, CreatedAt, UpdatedAt, Favorited, FavoritesCount, Author)`,
  `CommentView(Id, CreatedAt, UpdatedAt, Body, Author)`.
- Repositories (instance-based, ctor takes `JauntyDb db`): `UserRepository` (`Register`,
  `Login`, `GetById`, `GetByUsername`, `UpdateProfile`, static `HashPassword`),
  `ProfileRepository` (`GetProfile`, `Follow`, `Unfollow`), `ArticleRepository` (`Create`,
  `GetBySlug`, `Update`, `Delete`, `List`, `Feed`), `CommentRepository` (`Add`,
  `GetByArticleId`, `Delete`), `FavoriteRepository` (`Favorite`, `Unfavorite`, `GetCount`,
  `IsFavorited`), plus `db.Tags.GetAll()` (generated).
- `ConduitSqliteFixture.cs`: uniquely-named in-memory SQLite (`Mode=Memory;Cache=Shared`),
  applies `schema.sqlite.sql`, exposes `Db`/`Connection`/`Available`/`SkipReason`.
- `.csproj` today: plain `Microsoft.NET.Sdk`, net8.0, Microsoft.Data.Sqlite, xunit.

## 1. Project/host scaffolding

- Switch `.csproj` to `Microsoft.NET.Sdk.Web` (implicit `Microsoft.AspNetCore.App` shared
  framework reference). Add `Microsoft.AspNetCore.Mvc.Testing` (brings `WebApplicationFactory`
  + TestHost) and `System.IdentityModel.Tokens.Jwt` for JWT creation/validation. Keep the
  existing `AdditionalFiles` globs (`db/**/*.sql`, `db/schema/*.schema.json`) and
  `schema.sqlite.sql` copy-to-output unchanged.
- New `Program.cs`: top-level statements + `public partial class Program { }` marker at the
  bottom (required for `WebApplicationFactory<Program>` to resolve a public entry-point type).
  Register `JauntyDb`/`SqliteConnection` construction through DI via a small factory reading
  the connection string from configuration (not a hardcoded inline `new SqliteConnection(...)`),
  so `ConfigureWebHost` in the test fixture can override it per test run.
- `app.MapGroup("/api")` for all routes; Minimal API throughout (not Controllers) — 17 thin
  endpoints, avoids the MVC/model-binding/ProblemDetails pipeline whose default error shape
  would need overriding anyway.

## 2. JWT auth

- Symmetric HMAC-SHA256 signing, key = a `const string` in `Auth/JwtOptions.cs`, clearly
  commented as sample-only (same spirit as the existing `HashPassword` "stand-in" caveat).
- Claims: `sub`/`uid` = user id (the only one actually consumed server-side), `email` for
  convenience, standard `iat`/`exp`. Lifetime: 24h fixed, no refresh flow.
- `Auth/TokenService.cs`: `string CreateToken(User user)` — called only from register/login
  handlers, not from `UserRepository` (token issuance is an HTTP concern, not data-layer).
- **Custom auth scheme required**: RealWorld uses `Authorization: Token <jwt>`, not `Bearer`.
  Built-in `JwtBearerHandler` can't be reconfigured for a different scheme keyword, so write
  `Auth/TokenAuthenticationHandler.cs` (`AuthenticationHandler<TokenAuthenticationSchemeOptions>`):
  parse the header, require the literal `Token ` prefix, validate the JWT, return
  `AuthenticateResult.Success(ticket)` on success. **Return `AuthenticateResult.NoResult()`**
  (never `.Fail()`) when the header is missing/malformed/invalid — this lets auth-optional
  endpoints proceed unauthenticated instead of 401ing. Register as the default scheme so
  bare `.RequireAuthorization()` picks it up.
- Auth-required endpoints: `GET/PUT /user`, follow/unfollow, `GET /articles/feed`, article
  create/update/delete, comment create/delete, favorite/unfavorite.
- Auth-optional endpoints (read viewer id via `HttpContext.User` if authenticated, else null —
  feeds straight into the existing `int? viewerId` params already on `GetProfile`/`GetBySlug`/
  `List`/`GetByArticleId`, no repository changes needed): `GET /profiles/:username`,
  `GET /articles`, `GET /articles/:slug`, `GET /articles/:slug/comments`.
- No-auth endpoints: `POST /users`, `POST /users/login`, `GET /tags`.
- "Must be author" checks (article/comment update-delete): after `.RequireAuthorization()`
  confirms *someone* is logged in, handler compares author identity itself and returns
  `Results.Forbid()` (403) on mismatch — not worth a full `IAuthorizationHandler` for 2 cases.

## 3. Contracts (request/response DTOs)

New `Contracts/` folder (sibling to `Domain/`/`Repositories/`): `UserDtos.cs`,
`ProfileDtos.cs`, `ArticleDtos.cs`, `CommentDtos.cs`, `TagDtos.cs`, `ErrorResponse.cs`.

- `Profile`, `ArticleView`, `CommentView` map 1:1 onto their wrapped JSON shapes as-is
  (`{"profile": ...}`, `{"article": ...}`, `{"comment": ...}`) — System.Text.Json's default
  camelCase policy handles the field casing, no per-field attributes needed.
- `{"articles": [...], "articlesCount": N}`: new `record ArticlesResponse(IReadOnlyList<ArticleView> Articles, int ArticlesCount)`
  (renames `Total` → `articlesCount`).
- `{"comments": [...]}`: bare wrap, no count (per confirmed decision above).
- `{"tags": [...]}`: `record TagsResponse(IReadOnlyList<string> Tags)`.
- `{"user": {email, token, username, bio, image}}`: **not** a direct wrap of the generated
  `User` POCO (must drop `PasswordHash`/`Id`, add `Token`) — new
  `record UserResponse(string Email, string Token, string Username, string Bio, string? Image)`
  with a `static From(User user, string token)` helper, used by register/login/get-current/update.
- Request records: `RegisterRequest(Username, Email, Password)`, `LoginRequest(Email, Password)`,
  `UpdateUserRequest(Email?, Username?, Password?, Bio?, Image?)` (all-nullable — spec allows
  partial update; merge non-null fields onto the fetched current `User` before calling
  `UpdateProfile`), `UpsertArticleRequest(Title, Description, Body, TagList?)`,
  `AddCommentRequest(Body)`. Each gets its own non-generic envelope record (`RegisterRequestEnvelope { User }` etc.)
  since the wrapper key differs per resource — simpler than fighting a generic `Envelope<T>` against
  System.Text.Json's naming policy.

## 4. Repository additions (minimal, additive only)

- `ArticleRepository.GetIdBySlug(string slug)` → `int?` — needed by comments/favorites
  endpoints to resolve the internal article id without reaching into `JauntyDb` directly from
  the HTTP layer (keeps `JauntyDb` access confined to `Repositories/`).
- `UserRepository.UpdateProfile` gains one optional trailing `string? passwordHash = null`
  param (plus the corresponding conditional `SET` in `db/tables/Users/Update.sql`) so
  `PUT /user` can rehash a changed password without a second method.
- `POST /articles` duplicate-slug handling: pre-check via a slug lookup (or catch the unique
  constraint violation) and return 422 — no repository signature change, just handler-level
  catch/pre-check logic.
- Optional cosmetic: a thin `TagRepository` façade wrapping `db.Tags.GetAll()`, for layering
  consistency with every other resource (not required, low priority).

## 5. Error mapping

- **Do not use `Results.ValidationProblem`** — its default RFC7807 shape
  (`{"errors":..., "title":..., "status":..., "traceId":...}`) doesn't match the spec's bare
  `{"errors": {"field": ["msg"]}}`. Add a small `Results422.ValidationError(...)` helper in
  `Contracts/ErrorResponse.cs` returning `Results.Json(new ErrorResponse(errors), statusCode: 422)`.
- Login failure → **401** (bad credentials, not a field error). Not-found (`GetBySlug`/
  `GetProfile` null, unknown comment id) → plain **404**, no envelope. Forbidden (non-author
  write) → plain **403**. Unauthenticated on a required endpoint → framework default 401 via
  `.RequireAuthorization()`.

## 6. Testing strategy

- Existing direct-repository test classes (`UserTests.cs`, `ArticleTests.cs`, etc.) stay
  **completely untouched** — still valid lower-level regression coverage.
- New `Http/` subfolder: `ConduitWebAppFixture.cs` (a `WebApplicationFactory<Program>` that
  creates its own uniquely-named in-memory SQLite DB, applies the schema, and overrides the
  DI-registered connection string via `ConfigureWebHost`/`ConfigureServices` — separate from
  `ConduitSqliteFixture` so existing repository tests don't pay for host startup), plus one
  test class per resource: `UserHttpTests.cs`, `ProfileHttpTests.cs`, `ArticleHttpTests.cs`,
  `CommentHttpTests.cs`, `FavoriteHttpTests.cs`, `TagHttpTests.cs` — real `HttpClient` calls
  against real routes, auth-required tests obtain a token via `POST /api/users/login` first
  and attach `Authorization: Token <jwt>` manually.

## 7. Milestones (branch `torture/part4-conduit-http-jwt`, one commit each)

1. Host skeleton: `Sdk.Web` switch, packages, minimal `Program.cs` + `Program` marker, DI-registered
   connection factory, empty `/api` group. Build clean, existing tests unaffected.
2. JWT wiring: `TokenService`, `TokenAuthenticationHandler`, `AddAuthentication`/`AddAuthorization`
   registered — no endpoints yet, just confirm the app boots with the scheme active.
3. `Contracts/` DTOs + `ErrorResponse`/`Results422` helper.
4. Users/auth endpoints (register/login/get-current/update) + `ConduitWebAppFixture` +
   `UserHttpTests.cs`.
5. Profile endpoints (get/follow/unfollow) + `ProfileHttpTests.cs`.
6. Tags endpoint + `TagHttpTests.cs`.
7. Article read endpoints (list/feed/get-by-slug) + `ArticleHttpTests.cs` (read cases).
8. Article write endpoints (create/update/delete) + `GetIdBySlug` + slug-collision 422 +
   extend `ArticleHttpTests.cs`.
9. Comment endpoints (add/list/delete) + `CommentHttpTests.cs`.
10. Favorite endpoints (favorite/unfavorite) + `FavoriteHttpTests.cs`.
11. Full-suite stabilization: run 3x consecutively, fix any shared in-memory DB fixture-isolation
    flakiness between HTTP test classes.
12. Docs: append Phase A findings to `docs/torture-test-log.md`, process lessons (if any new)
    to `docs/torture-test-lessons-learned.md`.

Merge to `dev` with `--no-ff` after explicit confirmation, same as every prior part.

## Verification

- `dotnet build` clean from milestone 1 onward.
- `dotnet test` for `JauntyQ.Conduit.Sqlite.Tests` green after each milestone; full solution
  suite green by milestone 11.
- Manual smoke: run the host locally (`dotnet run`), issue a `POST /api/users` +
  `POST /api/users/login` + authenticated `GET /api/user` via curl/httpie to confirm the
  `Authorization: Token` scheme actually works end-to-end outside the test harness once.

## Phase B outline (planned in detail after Phase A is confirmed green)

Port the whole project (data layer + HTTP/JWT host) to 4 more fully self-contained sibling
projects (`samples/JauntyQ.Conduit.{SqlServer,Postgres,MySql,MariaDb}.Tests/`), matching the
eShopOnWeb pattern exactly: per-dialect Testcontainers fixture (`IAsyncLifetime`,
`Available`/`SkipReason` set on Docker-start failure) instead of in-memory SQLite, per-dialect
`schema.<dialect>.sql` (identity syntax, FK style — MySQL/MariaDB need out-of-line FKs), a
per-dialect committed `db/schema/jaunty.schema.json` snapshot, per-dialect NuGet provider +
Testcontainers packages (`Microsoft.Data.SqlClient`+`Testcontainers.MsSql`, `Npgsql`+
`Testcontainers.PostgreSql`, `MySqlConnector`+`Testcontainers.MySql`/`Testcontainers.MariaDb`),
and per-dialect `.sql` adjustments (`LIMIT/OFFSET` vs `OFFSET/FETCH`, `-- @identity` handling,
SQL Server `-- @type total bigint` + explicit cast on any COUNT(*) query). The `Program.cs`
endpoint/JWT/Contracts code stays identical across all 5 — only the fixture, schema, `.sql`
files, and `.csproj` package refs differ per dialect. Branch: `torture/part5-conduit-5dialect`.
