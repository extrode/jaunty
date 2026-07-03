using System.Text;
using System.Text.RegularExpressions;

using Markdig;

string repoRoot = FindRepoRoot(AppContext.BaseDirectory);
string sourceDir = Path.Combine(repoRoot, "docs", "01-api-reference");
string outputDir = Path.Combine(repoRoot, "dist", "html-docs");

// (fileName without extension, nav label)
(string File, string Label)[] pages =
[
    ("overview", "Overview"),
    ("query-methods", "Query Methods"),
    ("scalar-methods", "Scalar Methods"),
    ("single-result-methods", "Single Result Methods"),
    ("streaming-methods", "Streaming Methods"),
    ("multiple-result-sets", "Multiple Result Sets"),
    ("multi-entity-mapping", "Multi-Entity Mapping"),
    ("crud-operations", "CRUD Operations"),
    ("get-and-execute-operations", "Get and Execute Operations"),
    ("stored-procedures", "Stored Procedures"),
    ("fluent-api", "Fluent API"),
    ("configuration", "Configuration"),
    ("attributes", "Attributes"),
    ("api-summary", "API Summary"),
];

var pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .Build();

Directory.CreateDirectory(outputDir);

var navLookup = pages.ToDictionary(p => p.File, p => p.Label);

foreach (var page in pages)
{
    string mdPath = Path.Combine(sourceDir, page.File + ".md");
    if (!File.Exists(mdPath))
    {
        Console.WriteLine($"WARNING: missing {mdPath}, skipping.");
        continue;
    }

    string markdown = File.ReadAllText(mdPath);
    string title = ExtractTitle(markdown) ?? page.Label;
    string bodyHtml = Markdown.ToHtml(RewriteMarkdownLinks(markdown), pipeline);

    string html = RenderPage(title, page.File, bodyHtml, pages);
    File.WriteAllText(Path.Combine(outputDir, page.File + ".html"), html, new UTF8Encoding(false));
}

// index.html mirrors overview.html as the site landing page
string overviewHtmlPath = Path.Combine(outputDir, "overview.html");
if (File.Exists(overviewHtmlPath))
    File.Copy(overviewHtmlPath, Path.Combine(outputDir, "index.html"), overwrite: true);

File.WriteAllText(Path.Combine(outputDir, "site.css"), SiteCss(), new UTF8Encoding(false));

Console.WriteLine($"Generated {pages.Length} pages to {outputDir}");

static string? ExtractTitle(string markdown)
{
    var match = Regex.Match(markdown, @"^#\s+(.+)$", RegexOptions.Multiline);
    return match.Success ? match.Groups[1].Value.Trim() : null;
}

// Markdown links like (configuration.md) are rewritten to (configuration.html) so
// cross-page navigation works in the generated static site.
static string RewriteMarkdownLinks(string markdown) =>
    Regex.Replace(markdown, @"\]\(([a-zA-Z0-9\-]+)\.md\)", "]($1.html)");

static string RenderPage(string title, string currentFile, string bodyHtml, (string File, string Label)[] pages)
{
    var nav = new StringBuilder();
    nav.Append("<nav aria-label=\"API reference sections\">\n<ul>\n");
    foreach (var p in pages)
    {
        string aria = p.File == currentFile ? " aria-current=\"page\"" : "";
        string cssClass = p.File == currentFile ? " class=\"active\"" : "";
        nav.Append($"<li><a href=\"{p.File}.html\"{cssClass}{aria}>{System.Net.WebUtility.HtmlEncode(p.Label)}</a></li>\n");
    }
    nav.Append("</ul>\n</nav>");

    return $$"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{{System.Net.WebUtility.HtmlEncode(title)}} - Jaunty API Documentation</title>
    <link rel="stylesheet" href="site.css">
    </head>
    <body>
    <header>
    <h1><a href="index.html">Jaunty</a></h1>
    <p>API Documentation</p>
    </header>
    <div class="layout">
    {{nav}}
    <main>
    <article>
    {{bodyHtml}}
    </article>
    </main>
    </div>
    <footer>
    <p>Jaunty &mdash; a high-performance .NET micro-ORM.</p>
    </footer>
    </body>
    </html>
    """;
}

static string FindRepoRoot(string startDir)
{
    var dir = new DirectoryInfo(startDir);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Jaunty.slnx")))
        dir = dir.Parent;

    return dir?.FullName ?? throw new InvalidOperationException("Could not locate repository root (Jaunty.slnx not found).");
}

static string SiteCss() => """
:root {
    --color-bg: #ffffff;
    --color-text: #1a1a1a;
    --color-muted: #55606c;
    --color-accent: #2f5f8f;
    --color-border: #d8dee4;
    --color-code-bg: #f4f6f8;
    font-size: 16px;
}

* { box-sizing: border-box; }

body {
    margin: 0;
    font-family: -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
    color: var(--color-text);
    background: var(--color-bg);
    line-height: 1.6;
}

header {
    padding: 1rem 2rem;
    border-bottom: 1px solid var(--color-border);
}

header h1 {
    margin: 0;
    font-size: 1.5rem;
}

header h1 a {
    color: var(--color-text);
    text-decoration: none;
}

header p {
    margin: 0.25rem 0 0;
    color: var(--color-muted);
    font-size: 0.9rem;
}

.layout {
    display: flex;
    align-items: flex-start;
    max-width: 1200px;
    margin: 0 auto;
}

nav {
    flex: 0 0 220px;
    padding: 1.5rem 1rem;
    position: sticky;
    top: 0;
}

nav ul {
    list-style: none;
    margin: 0;
    padding: 0;
}

nav a {
    display: block;
    padding: 0.35rem 0.5rem;
    color: var(--color-text);
    text-decoration: none;
    border-radius: 4px;
}

nav a:hover {
    background: var(--color-code-bg);
}

nav a.active {
    color: var(--color-accent);
    font-weight: 600;
    background: var(--color-code-bg);
}

main {
    flex: 1 1 auto;
    padding: 1.5rem 2rem 3rem;
    min-width: 0;
}

article h1:first-child {
    margin-top: 0;
}

article h2 {
    border-bottom: 1px solid var(--color-border);
    padding-bottom: 0.3rem;
    margin-top: 2.5rem;
}

article code {
    background: var(--color-code-bg);
    padding: 0.15em 0.4em;
    border-radius: 3px;
    font-family: "Cascadia Code", Consolas, "SFMono-Regular", monospace;
    font-size: 0.9em;
}

article pre {
    background: var(--color-code-bg);
    border: 1px solid var(--color-border);
    border-radius: 6px;
    padding: 1rem;
    overflow-x: auto;
}

article pre code {
    background: none;
    padding: 0;
}

article table {
    border-collapse: collapse;
    width: 100%;
}

article th, article td {
    border: 1px solid var(--color-border);
    padding: 0.5rem 0.75rem;
    text-align: left;
}

article a {
    color: var(--color-accent);
}

footer {
    border-top: 1px solid var(--color-border);
    padding: 1.5rem 2rem;
    color: var(--color-muted);
    font-size: 0.85rem;
    text-align: center;
}

@media (max-width: 800px) {
    .layout { flex-direction: column; }
    nav { position: static; flex: 1 1 auto; width: 100%; }
}
""";
