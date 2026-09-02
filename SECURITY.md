# Security Policy

## Supported versions

Security fixes are applied to the latest released version. Older releases do not receive
backported fixes. The current release is `v1.0.0-rc.2`; while the 1.0 line is in release
candidate, "latest" means the newest tag, not the newest stable.

## Reporting a vulnerability

Please do **not** open a public GitHub issue for a security vulnerability.

Report privately via GitHub Security Advisories:
<https://github.com/extrode/jaunty/security/advisories/new>

Include:

- A description of the vulnerability and its impact
- Steps to reproduce (a minimal repro is ideal)
- Affected package(s) and version(s)

If that page is unavailable to you, open an ordinary issue saying only that you have a security
report and want a private channel — **no detail, no repro** — and one will be opened for you.

You can expect an acknowledgement within 7 days. Once a fix is available, a patched release is
published and the advisory is disclosed.

## Scope

Jaunty executes SQL you write; it does not construct SQL from user input on its own. What is in
scope is Jaunty putting something into a command that you did not put there, or failing to
neutralise something it undertook to neutralise:

- **Parameter binding** — flaws that could enable SQL injection through the documented APIs,
  such as collection expansion or the single-scalar shorthand
- **Identifier handling** — a table, column or CTE name reaching SQL without passing
  `SqlIdentifierValidator`, in any dialect, builder or bulk-copy provider
- **Bulk copy and CSV import** — paths that execute a statement other than the one intended
- **Redaction inside `LoggingInterceptor`** — a value whose parameter name matches
  `LoggingConfiguration.SensitiveParameterNames` appearing unmasked in its output
  (the type lives in the optional `Extrode.Jaunty.Extensions.Logging` package)
- **Connection strings** — credentials surviving `SanitizeConnectionString` into a log or an
  exception message

## Not in scope

These are documented behaviours rather than defects. A report about one of them will be closed
with a pointer back to this section:

- **Interpolating untrusted input into a SQL string you then pass to Jaunty.** Use parameters.
- **A custom interceptor reading `CommandContext.Parameters`.** That property is the caller's own
  parameters object, stored verbatim and unredacted by design; the XML documentation on it says
  so. Redaction is the built-in `LoggingInterceptor`'s behaviour, not the pipeline's, and an
  interceptor you write is responsible for its own.

## Prior findings

Eight families of security-relevant findings were raised by the internal audit and all eight are
closed in `src/`. [`docs/05-quality/audit-record.md`](docs/05-quality/audit-record.md) lists them
and says where each is closed, along with why the audit's open carry-forward list is not
published.
