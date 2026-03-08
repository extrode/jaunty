# Documentation Assets

Media assets supporting Jaunty documentation.

---

## Contents

### Screenshots

UI screenshots and visual examples:

- [`screenshots/`](screenshots/) - Application screenshots

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
