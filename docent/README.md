# Docent

**A lightweight, themeable documentation framework for technical projects.**

Docent generates beautiful, accessible, static documentation sites from XML comments and Markdown. Built with simplicity and flexibility in mind.

## Features

- **Multiple Themes** - Sage, Sepia, and more soothing color schemes
- **XML Comment Support** - Generate docs from C# XML documentation
- **Static Output** - Pure HTML/CSS, minimal JavaScript
- **Accessible** - WCAG 2.1 AA compliant
- **Responsive** - Mobile-first design
- **Themeable** - Easy to customize colors and layout
- **Fast** - No build process required for simple setups

## Quick Start

```bash
# Clone or copy Docent to your project
git clone https://github.com/yourorg/docent.git

# Generate documentation
dotnet run --project docent/tools/Docent.Generator.csproj \
  --input ../YourProject/bin/Debug/net8.0/YourProject.dll \
  --output ./docs \
  --theme sage
```

## Project Structure

```
docent/
├── src/
│   ├── core/              # Core CSS and base styles
│   └── themes/            # Theme definitions
│       ├── base/          # Base theme (shared variables)
│       ├── sage/          # Light green theme
│       └── sepia/         # Warm sepia theme
├── templates/
│   ├── api/               # API reference templates
│   └── guide/             # Guide/tutorial templates
├── docs/                  # Generated documentation output
└── tools/
    └── Docent.Generator/  # Documentation generator tool
```

## Themes

### Sage
A calming light green theme perfect for technical documentation.

### Sepia  
A warm, paper-like theme that's easy on the eyes for long reading sessions.

## License

MIT License - See [LICENSE](LICENSE) for details.

---

**Docent** - Guiding users through your codebase.
