# Documentation Assets

Media assets supporting Jaunty documentation.

---

## Contents

### Screenshots

UI screenshots and visual examples:

- [`screenshots/`](screenshots/) - Application screenshots

### Logo

Traced from the source artwork; `#FDCF32` on `#212121` for dark surfaces, on white for light ones,
and the bare mark for anything else. All three share one path.

- [`logo/jaunty-mark.svg`](logo/jaunty-mark.svg), transparent; the one the README uses, so the
  reader's theme does not matter
- [`logo/jaunty-mark-dark.svg`](logo/jaunty-mark-dark.svg) and
  [`logo/jaunty-mark-light.svg`](logo/jaunty-mark-light.svg), rounded corners
- [`logo/jaunty-mark-dark-square.svg`](logo/jaunty-mark-dark-square.svg) and
  [`logo/jaunty-mark-light-square.svg`](logo/jaunty-mark-light-square.svg), square corners

JauntyQ candidates, each as transparent, `-dark` and `-light`:

- `logo/jauntyq-lockup*.svg`: the mark followed by a Q glyph of the same height
- `logo/jauntyq-badge*.svg`: the mark with a small Q in a disc at the lower right
- `logo/jauntyq-ring*.svg`: the mark inside a Q ring whose tail breaks out at the lower right

### Benchmarks

`benchmarks/` holds the README's comparison table and the 2026-07-29 read-path and allocation
charts as SVGs with colored cells, generated from the numbers in
`docs/05-quality/reports/benchmarks-2026-07-29.md`. GitHub strips cell colors from Markdown
tables, which is why they are images; the README keeps a text copy under each one.

### Diagrams

Architecture and flow diagrams:

- `diagrams/` - Architecture diagrams (coming soon)

---

## Usage

### Referencing Images in Documentation

```markdown
![Description](../assets/screenshots/example.png)
```

### Best Practices

1. **Use descriptive filenames**: `fluent-api-query-example.png` not `image1.png`
2. **Optimize for web**: Compress images before adding
3. **Use appropriate formats**:
   - PNG for screenshots (sharp text, UI)
   - SVG for diagrams (scalable, editable)
   - WebP for photos (smaller file size)

---

## Adding New Assets

1. Place images in the appropriate subfolder
2. Use lowercase-with-hyphens naming
3. Reference from documentation using relative paths
4. Commit with clear message describing the image

---

**Location**: `docs/assets/`  
**Purpose**: Support documentation with visual assets
