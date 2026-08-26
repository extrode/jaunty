# Security Policy

## Supported Versions

Security fixes are applied to the latest released version. Older releases do
not receive backported fixes.

## Reporting a Vulnerability

Please do **not** open a public GitHub issue for security vulnerabilities.

Report privately via GitHub Security Advisories:
<https://github.com/extrode/jaunty/security/advisories/new>

Include:

- A description of the vulnerability and its impact
- Steps to reproduce (a minimal repro is ideal)
- Affected package(s) and version(s)

You can expect an acknowledgement within 7 days. Once a fix is available, a
patched release is published and the advisory is disclosed.

## Scope

Jaunty executes SQL you write; it does not construct SQL from user input on
its own. Reports most relevant to this project include:

- Parameter binding flaws that could enable SQL injection through the
  documented APIs (e.g. collection expansion, positional binding)
- Sensitive data leaking through logging/interception despite the
  redaction configuration (`LoggingConfiguration` sensitive-parameter list)
- Bulk copy / import paths executing unintended statements

SQL injection through interpolating untrusted input into SQL strings passed
to Jaunty is by design out of scope — use parameters.
