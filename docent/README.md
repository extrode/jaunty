# Docent

**A lightweight, themeable documentation framework for technical projects.**

Docent generates beautiful, accessible, static documentation sites from XML comments and Markdown. Built with simplicity and flexibility in mind.

> **Standalone Project**: Docent was originally part of the Jaunty project and is now a standalone documentation framework.

## Features

- **Multiple Themes** - Sage, Sepia, and more soothing color schemes
- **Dark Mode Ready** - Themeable dark mode via CSS variables
- **XML Comment Support** - Generate docs from C# XML documentation
- **Static Output** - Pure HTML/CSS, minimal JavaScript
- **Accessible** - WCAG 2.1 AA compliant
- **Responsive** - Mobile-first design
- **Themeable** - Easy to customize colors and layout
- **Fast** - No build process required for simple setups
- **CLI Tool** - Install as global .NET tool

## Quick Start

```bash
# Install the Docent CLI tool
dotnet tool install -g Docent.Generator

# Generate documentation
docent --input ./MyProject/bin/Debug/net8.0/ \
       --output ./docs \
       --theme sage \
       --site-name "My Project"
```

## Project Structure

```
docent/
├── src/
│   ├── core/              # Core CSS and base styles
│   └── themes/            # Theme definitions
│       ├── base/          # Base theme (shared variables)
│       ├── sage/          # Light green theme (default)
│       └── sepia/         # Warm sepia theme
├── templates/
│   ├── api/               # API reference templates
│   └── guide/             # Guide/tutorial templates
├── docs/                  # Generated documentation output
└── tools/
    └── Docent.Generator/  # Documentation generator CLI
```

## Themes

### Sage (Default)
A calming light green theme perfect for technical documentation.

### Sepia  
A warm, paper-like theme that's easy on the eyes for long reading sessions.

### Ocean
A cool blue theme inspired by the sea. Great for a modern, tech feel.

### Forest
A natural green theme that's easy on the eyes. Perfect for eco-friendly projects.

### Lavender
A soft purple theme inspired by flowers. Elegant and distinctive.

### VitePress (NEW)
A clean, modern theme inspired by VitePress. Features vibrant green accents and excellent readability. Perfect for developer documentation.

### Dark Mode
All themes support dark mode with **dual activation methods**:

**Manual Toggle** (for theme switchers):
```html
<html data-theme="dark">
```

**Auto-Detect** (respects system preference):
```css
@media (prefers-color-scheme: dark) { ... }
```

Use both for the best user experience - manual toggle overrides system preference.

## Customization

All colors are CSS custom properties. Override in your theme:

```css
:root {
  --docent-primary: #your-color;
  --docent-bg: #your-background;
  /* ... more variables */
}
```

## License

MIT License - See [LICENSE](LICENSE) for details.

---

**Docent** - Guiding users through your codebase.
