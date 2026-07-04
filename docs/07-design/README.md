# 07-design — Visual Design Source

This directory contains the authoritative visual design for the Jaunty documentation site.

## Files

| File | Description |
|------|-------------|
| `Jaunty Docs App.dc.html` | **Primary design** — full docs app layout: header bar, collapsible sidebar, content pane, code blocks, prev/next nav, status bar. This is the authoritative target. |
| `Jaunty Docs.dc.html` | Secondary / alternate page design for additional reference. |
| `support.js` | Design-tool runtime (custom `<x-dc>` / `<sc-*>` component renderer). Required by the browser to display the `.dc.html` files. Not used by the generated site. |

## Viewing the designs

Open either `.dc.html` file in a browser from this directory (both files and `support.js` must be co-located). The design tool renders `<x-dc>` markup client-side; it does not require a build step.

## Design tokens (dark theme)

Extracted from `Jaunty Docs App.dc.html` and implemented in `tools/Jaunty.DocsGenerator`:

| Token | Value | Role |
|-------|-------|------|
| `--bg` | `#0e0e10` | Page background |
| `--bg2` | `#0a0a0c` | Header / status bar background |
| `--panel` | `#141419` | Sidebar panels, callout backgrounds |
| `--border` | `#1e1e23` | Primary border |
| `--border2` | `#26262c` | Secondary border |
| `--tx` | `#e6e6e2` | Primary text |
| `--tx2` | `#b9b9bd` | Secondary text / body prose |
| `--mut` | `#8d8d95` | Muted text, nav group labels |
| `--dim` | `#63636b` | Dimmed text, breadcrumbs, code lang labels |
| `--faint` | `#4a4a52` | Placeholder text, filter icons |
| `--hi` | `#ffffff` | High-emphasis text, page title |
| `--hov` | `#15151a` | Hover background |
| `--code-bg` | `#101013` | Code block background |
| `--code-hd` | `#121215` | Code block header background |
| `--code-bd` | `#232329` | Code block border |
| `--code-tx` | `#d6d6d2` | Code block text |

**Font:** `JetBrains Mono` (400/500/600 weight, italic variants) via Google Fonts — used for everything: prose, nav, code.

**Layout:** CSS grid `grid-template-rows: 46px 1fr 26px; height: 100vh` — header / body / status bar. Body uses a 240 px sidebar + fluid content column.

## Implementation

`tools/Jaunty.DocsGenerator` is the console app that implements this design as a static site generator. Run it with:

```bash
dotnet run --project tools/Jaunty.DocsGenerator -- <input-dir> <output-dir>
# Example:
dotnet run --project tools/Jaunty.DocsGenerator -- docs/ dist/docs-site/
```

See `tools/Jaunty.DocsGenerator/Program.cs` for the CSS and JS implementation of these tokens.
