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

string[] csharpKeywords =
[
    "public", "private", "protected", "internal", "static", "readonly", "const", "class", "struct",
    "interface", "enum", "void", "new", "return", "using", "namespace", "var", "this", "base", "null",
    "true", "false", "if", "else", "for", "foreach", "while", "do", "switch", "case", "break", "continue",
    "try", "catch", "finally", "throw", "async", "await", "where", "get", "set", "override", "virtual",
    "abstract", "sealed", "partial", "in", "out", "ref", "params", "typeof", "is", "as", "default", "yield",
];

string[] sqlKeywords =
[
    "SELECT", "FROM", "WHERE", "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE", "JOIN", "INNER",
    "LEFT", "RIGHT", "ON", "GROUP", "BY", "ORDER", "AS", "AND", "OR", "NOT", "NULL", "COUNT", "SUM", "AVG",
    "MAX", "MIN", "DISTINCT", "LIMIT", "OFFSET", "CREATE", "TABLE", "PRIMARY", "KEY",
];

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
    bodyHtml = PostProcessCodeBlocks(bodyHtml);
    bodyHtml = WrapCallouts(bodyHtml);

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

string PostProcessCodeBlocks(string html) =>
    Regex.Replace(html, @"<pre><code class=""language-(\w+)"">([\s\S]*?)</code></pre>", match =>
    {
        string lang = match.Groups[1].Value;
        string code = match.Groups[2].Value;
        string highlighted = lang switch
        {
            "csharp" => HighlightTokens(code, csharpKeywords),
            "sql" => HighlightTokens(code, sqlKeywords),
            _ => code,
        };
        return $"<pre data-lang=\"{lang}\"><code class=\"language-{lang}\">{highlighted}</code></pre>";
    });

// Single alternation-based scan so comments/strings/keywords/numbers never double-tag the same text.
static string HighlightTokens(string code, string[] keywords)
{
    string keywordPattern = string.Join("|", keywords.Select(Regex.Escape));
    string pattern = $@"(?<comment>//[^\n]*|/\*[\s\S]*?\*/)|(?<string>@?\$?""(?:[^""\\]|\\.)*"")|(?<keyword>\b(?:{keywordPattern})\b)|(?<number>\b\d+(?:\.\d+)?[mMfFdDlLuU]?\b)";

    return Regex.Replace(code, pattern, m =>
    {
        if (m.Groups["comment"].Success) return $"<span class=\"tok-com\">{m.Value}</span>";
        if (m.Groups["string"].Success) return $"<span class=\"tok-str\">{m.Value}</span>";
        if (m.Groups["keyword"].Success) return $"<span class=\"tok-kw\">{m.Value}</span>";
        if (m.Groups["number"].Success) return $"<span class=\"tok-num\">{m.Value}</span>";
        return m.Value;
    });
}

// Wraps the <ul>/<ol> immediately following specific headings (Important Notes, Best Practices,
// Notes, Pool Safety) in a styled callout div, so these sections read as call-outs instead of
// blending into the rest of the page.
static string WrapCallouts(string html) =>
    Regex.Replace(
        html,
        @"(<h2[^>]*>(?:Important Notes|Best Practices|Notes|Pool Safety)</h2>\s*)(<[uo]l>[\s\S]*?</[uo]l>)",
        m =>
        {
            string heading = m.Groups[1].Value;
            string list = m.Groups[2].Value;
            string cssClass = heading.Contains("Important Notes") ? "callout callout-important" : "callout callout-tip";
            return $"{heading}<div class=\"{cssClass}\">{list}</div>";
        });

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
    <div class="header-inner">
    <h1><a href="index.html"><span class="logo-mark">J</span>Jaunty</a></h1>
    <p>API Documentation <span class="header-badge">micro-ORM</span></p>
    </div>
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
    --color-bg-soft: #f8f7fd;
    --color-text: #1c1e2b;
    --color-muted: #656d82;
    --color-accent: #7c3aed;
    --color-accent-2: #0ea5a4;
    --color-border: #e6e4f2;
    --color-code-bg: #f6f4fc;
    --color-code-border: #e3ddf7;
    font-size: 16px;
}

* { box-sizing: border-box; }

body {
    margin: 0;
    font-family: -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
    color: var(--color-text);
    background: var(--color-bg);
    line-height: 1.65;
}

header {
    padding: 1.5rem 2rem;
    background: linear-gradient(120deg, var(--color-accent), var(--color-accent-2));
    color: #fff;
}

.header-inner {
    max-width: 1200px;
    margin: 0 auto;
}

header h1 {
    margin: 0;
    font-size: 1.65rem;
    display: flex;
    align-items: center;
    gap: 0.5rem;
}

header h1 a {
    color: #fff;
    text-decoration: none;
    display: flex;
    align-items: center;
    gap: 0.5rem;
}

.logo-mark {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 1.9rem;
    height: 1.9rem;
    background: rgba(255, 255, 255, 0.22);
    border-radius: 8px;
    font-size: 1.1rem;
    font-weight: 800;
}

header p {
    margin: 0.4rem 0 0;
    color: rgba(255, 255, 255, 0.9);
    font-size: 0.9rem;
    display: flex;
    align-items: center;
    gap: 0.6rem;
}

.header-badge {
    background: rgba(255, 255, 255, 0.2);
    padding: 0.1rem 0.55rem;
    border-radius: 999px;
    font-size: 0.75rem;
    letter-spacing: 0.02em;
}

.layout {
    display: flex;
    align-items: flex-start;
    max-width: 1200px;
    margin: 0 auto;
}

nav {
    flex: 0 0 230px;
    padding: 1.5rem 1rem;
    position: sticky;
    top: 0;
    max-height: 100vh;
    overflow-y: auto;
}

nav ul {
    list-style: none;
    margin: 0;
    padding: 0;
}

nav a {
    display: block;
    padding: 0.4rem 0.75rem;
    margin: 0.05rem 0;
    color: var(--color-text);
    text-decoration: none;
    border-radius: 999px;
    font-size: 0.92rem;
    transition: background 0.12s ease, color 0.12s ease, padding-left 0.12s ease;
}

nav a:hover {
    background: var(--color-bg-soft);
    padding-left: 1rem;
}

nav a.active {
    color: #fff;
    font-weight: 600;
    background: linear-gradient(120deg, var(--color-accent), var(--color-accent-2));
}

main {
    flex: 1 1 auto;
    padding: 1.5rem 2rem 4rem;
    min-width: 0;
}

article h1:first-child {
    margin-top: 0;
    background: linear-gradient(120deg, var(--color-accent), var(--color-accent-2));
    -webkit-background-clip: text;
    background-clip: text;
    color: transparent;
}

article h2 {
    border-bottom: 2px solid var(--color-border);
    padding-bottom: 0.35rem;
    margin-top: 2.75rem;
}

article h3 {
    color: var(--color-accent);
    margin-top: 2rem;
}

article code {
    background: var(--color-code-bg);
    color: #5b21b6;
    padding: 0.15em 0.4em;
    border-radius: 4px;
    font-family: "Cascadia Code", Consolas, "SFMono-Regular", monospace;
    font-size: 0.9em;
}

article pre {
    position: relative;
    background: var(--color-code-bg);
    border: 1px solid var(--color-code-border);
    border-radius: 10px;
    padding: 1.1rem 1rem 1rem;
    overflow-x: auto;
    box-shadow: 0 1px 2px rgba(28, 30, 43, 0.04);
}

article pre[data-lang]::before {
    content: attr(data-lang);
    position: absolute;
    top: 0.55rem;
    right: 0.75rem;
    font-size: 0.68rem;
    text-transform: uppercase;
    letter-spacing: 0.06em;
    color: var(--color-accent-2);
    background: rgba(14, 165, 164, 0.1);
    padding: 0.1rem 0.5rem;
    border-radius: 999px;
}

article pre code {
    background: none;
    color: inherit;
    padding: 0;
}

.tok-kw { color: #7c3aed; font-weight: 600; }
.tok-str { color: #0f766e; }
.tok-com { color: #8b8fa3; font-style: italic; }
.tok-num { color: #b45309; }

article table {
    border-collapse: collapse;
    width: 100%;
}

article th, article td {
    border: 1px solid var(--color-border);
    padding: 0.55rem 0.8rem;
    text-align: left;
}

article th {
    background: var(--color-bg-soft);
}

article a {
    color: var(--color-accent);
    text-decoration: underline;
    text-decoration-color: rgba(124, 58, 237, 0.3);
}

article a:hover {
    text-decoration-color: currentColor;
}

.callout {
    border-radius: 10px;
    padding: 1rem 1.25rem;
    margin: 0.75rem 0 1.5rem;
    border-left: 4px solid var(--color-accent-2);
    background: rgba(14, 165, 164, 0.06);
}

.callout ul, .callout ol {
    margin: 0;
    padding-left: 1.2rem;
}

.callout-important {
    border-left-color: var(--color-accent);
    background: rgba(124, 58, 237, 0.06);
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
    nav { position: static; flex: 1 1 auto; width: 100%; max-height: none; }
}
""";
