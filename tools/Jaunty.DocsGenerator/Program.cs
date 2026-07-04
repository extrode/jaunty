using System.CommandLine;
using System.Text;
using System.Text.RegularExpressions;

using Markdig;

// ---------------------------------------------------------------------------
// CLI  (System.CommandLine 2.0.9 GA)
// ---------------------------------------------------------------------------
var inputArg  = new Argument<DirectoryInfo>("input-dir")  { Description = "Directory containing numbered markdown docs sections." };
var outputArg = new Argument<DirectoryInfo>("output-dir") { Description = "Directory to write the generated static site into." };

var rootCmd = new RootCommand("Jaunty docs generator — converts a numbered markdown tree into a dark-theme static site.");
rootCmd.Arguments.Add(inputArg);
rootCmd.Arguments.Add(outputArg);

rootCmd.SetAction((ParseResult pr) =>
{
    var inputDir  = pr.GetValue(inputArg)!;
    var outputDir = pr.GetValue(outputArg)!;
    if (!inputDir.Exists)
    {
        Console.Error.WriteLine($"ERROR: input directory does not exist: {inputDir.FullName}");
        return 1;
    }
    GenerateSite(inputDir.FullName, outputDir.FullName);
    return 0;
});

return rootCmd.Parse(args).Invoke();

// ---------------------------------------------------------------------------
// Site generation
// ---------------------------------------------------------------------------
static void GenerateSite(string inputDir, string outputDir)
{
    Directory.CreateDirectory(outputDir);

    var pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    // Collect sections and pages from the numbered directory tree
    var sections = CollectSections(inputDir);

    // Flatten all pages for sidebar rendering (section -> page list)
    int totalPages = 0;

    foreach (var section in sections)
    {
        foreach (var page in section.Pages)
        {
            string markdown   = File.ReadAllText(page.SourcePath);
            string title      = ExtractTitle(markdown) ?? page.Label;
            string rewritten  = RewriteMarkdownLinks(markdown);
            string bodyHtml   = Markdown.ToHtml(rewritten, pipeline);
            bodyHtml          = PostProcessCodeBlocks(bodyHtml);
            bodyHtml          = WrapCallouts(bodyHtml);

            string html = RenderPage(title, page, sections);
            string dest = Path.Combine(outputDir, page.OutputFile);
            File.WriteAllText(dest, html, new UTF8Encoding(false));
            totalPages++;
        }
    }

    // index.html: prefer 00-quick-start/README or first page overall
    string? indexSource = sections
        .SelectMany(s => s.Pages)
        .FirstOrDefault(p => p.OutputFile == "00-quick-start-README.html"
                          || p.OutputFile == "00-quick-start-index.html")?
        .OutputFile
        ?? sections.SelectMany(s => s.Pages).FirstOrDefault()?.OutputFile;

    if (indexSource is not null)
    {
        string srcPath  = Path.Combine(outputDir, indexSource);
        string destPath = Path.Combine(outputDir, "index.html");
        // Re-render index.html with its own "active" state pointing at itself
        var firstPage = sections.SelectMany(s => s.Pages).First(p => p.OutputFile == indexSource);
        string markdown  = File.ReadAllText(firstPage.SourcePath);
        string title     = ExtractTitle(markdown) ?? firstPage.Label;
        string bodyHtml  = Markdown.ToHtml(RewriteMarkdownLinks(markdown), pipeline);
        bodyHtml         = PostProcessCodeBlocks(bodyHtml);
        bodyHtml         = WrapCallouts(bodyHtml);
        // Make an "index" page entry that points at index.html
        var indexPage = firstPage with { OutputFile = "index.html" };
        string html = RenderPage(title, indexPage, sections);
        File.WriteAllText(destPath, html, new UTF8Encoding(false));
    }

    // Shared CSS and JS
    File.WriteAllText(Path.Combine(outputDir, "site.css"), SiteCss(), new UTF8Encoding(false));
    File.WriteAllText(Path.Combine(outputDir, "nav.js"),   NavJs(),   new UTF8Encoding(false));

    // Copy image assets
    CopyAssets(inputDir, outputDir);

    Console.WriteLine($"Generated {totalPages} pages -> {outputDir}");
}

// ---------------------------------------------------------------------------
// Directory walker
// ---------------------------------------------------------------------------
static List<SectionEntry> CollectSections(string root)
{
    var sections = new List<SectionEntry>();

    // Top-level numbered dirs (00-*, 01-*, …)
    var dirs = Directory.GetDirectories(root)
        .Where(d => Regex.IsMatch(Path.GetFileName(d), @"^\d+[-_]"))
        .OrderBy(d => d)
        .ToArray();

    // Also include loose .md files at root as a synthetic "root" section
    var rootMd = Directory.GetFiles(root, "*.md")
        .Where(f => Path.GetFileName(f) != "README.md")
        .OrderBy(f => f)
        .ToArray();

    if (rootMd.Length > 0)
    {
        var rootPages = rootMd.Select(f => MakePage(f, "root", root)).ToArray();
        sections.Add(new SectionEntry("root", "Overview", rootPages));
    }

    foreach (var dir in dirs)
    {
        string dirName   = Path.GetFileName(dir);
        string sectionId = dirName;
        string label     = StripNumberPrefix(dirName);

        // Skip archive and assets folders in the main nav
        if (Regex.IsMatch(dirName, @"^99|_assets", RegexOptions.IgnoreCase))
            continue;

        var mdFiles = Directory.GetFiles(dir, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(f => SortKey(Path.GetFileName(f)))
            .ToArray();

        if (mdFiles.Length == 0)
            continue;

        var pages = mdFiles.Select(f => MakePage(f, sectionId, root)).ToArray();
        sections.Add(new SectionEntry(sectionId, label, pages));
    }

    return sections;
}

static PageEntry MakePage(string filePath, string sectionId, string root)
{
    string rel   = Path.GetRelativePath(root, filePath);    // e.g. 01-api-reference/query-methods.md
    string fname = Path.GetFileNameWithoutExtension(filePath);
    string label = StripNumberPrefix(fname)
                    .Replace('-', ' ')
                    .Replace('_', ' ');
    label = char.ToUpperInvariant(label[0]) + label[1..];   // sentence-case first letter

    // Build a flat output filename so all pages live in one directory
    string relDir   = Path.GetDirectoryName(rel) ?? "";
    string outName  = relDir == ""
        ? fname + ".html"
        : relDir.Replace(Path.DirectorySeparatorChar, '-').Replace(Path.AltDirectorySeparatorChar, '-')
            + "-" + fname + ".html";

    return new PageEntry(label, filePath, outName, sectionId);
}

static string StripNumberPrefix(string name)
{
    // Strip leading digits + separator: "01-api-reference" -> "api-reference"
    var m = Regex.Match(name, @"^\d+[-_](.+)$");
    return m.Success ? m.Groups[1].Value : name;
}

static string SortKey(string filename)
{
    // README sorts first within a dir
    if (filename.Equals("README.md", StringComparison.OrdinalIgnoreCase)) return "000_" + filename;
    var m = Regex.Match(filename, @"^(\d+)");
    return m.Success ? m.Groups[1].Value.PadLeft(6, '0') + "_" + filename : filename;
}

// ---------------------------------------------------------------------------
// Asset copying
// ---------------------------------------------------------------------------
static void CopyAssets(string inputDir, string outputDir)
{
    string[] imageExts = [".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".ico"];
    var images = Directory.GetFiles(inputDir, "*", SearchOption.AllDirectories)
        .Where(f => imageExts.Contains(Path.GetExtension(f).ToLowerInvariant()));

    foreach (var img in images)
    {
        string rel  = Path.GetRelativePath(inputDir, img);
        string dest = Path.Combine(outputDir, rel.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_'));
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(img, dest, overwrite: true);
    }
}

// ---------------------------------------------------------------------------
// Markdown processing
// ---------------------------------------------------------------------------
static string? ExtractTitle(string markdown)
{
    var m = Regex.Match(markdown, @"^#\s+(.+)$", RegexOptions.Multiline);
    return m.Success ? m.Groups[1].Value.Trim() : null;
}

static string RewriteMarkdownLinks(string markdown) =>
    Regex.Replace(markdown, @"\]\(([a-zA-Z0-9\-_]+)\.md([#?][^)]*)?\)", m =>
    {
        string file    = m.Groups[1].Value;
        string anchor  = m.Groups[2].Value;
        return $"]({file}.html{anchor})";
    });

static string PostProcessCodeBlocks(string html)
{
    string[] csharpKw =
    [
        "public", "private", "protected", "internal", "static", "readonly", "const", "class", "struct",
        "interface", "enum", "void", "new", "return", "using", "namespace", "var", "this", "base", "null",
        "true", "false", "if", "else", "for", "foreach", "while", "do", "switch", "case", "break", "continue",
        "try", "catch", "finally", "throw", "async", "await", "where", "get", "set", "override", "virtual",
        "abstract", "sealed", "partial", "in", "out", "ref", "params", "typeof", "is", "as", "default", "yield",
        "record", "init", "required", "with", "not", "and", "or", "when",
    ];
    string[] sqlKw =
    [
        "SELECT", "FROM", "WHERE", "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE", "JOIN", "INNER",
        "LEFT", "RIGHT", "ON", "GROUP", "BY", "ORDER", "AS", "AND", "OR", "NOT", "NULL", "COUNT", "SUM", "AVG",
        "MAX", "MIN", "DISTINCT", "LIMIT", "OFFSET", "CREATE", "TABLE", "PRIMARY", "KEY", "WITH", "RETURNING",
    ];
    return Regex.Replace(html, @"<pre><code class=""language-(\w+)"">([\s\S]*?)</code></pre>", m =>
    {
        string lang        = m.Groups[1].Value;
        string code        = m.Groups[2].Value;
        string highlighted = lang.ToLowerInvariant() switch
        {
            "csharp" or "cs"  => HighlightTokens(code, csharpKw),
            "sql"             => HighlightTokens(code, sqlKw),
            _                 => code,
        };
        string langLabel = lang.ToLowerInvariant() switch
        {
            "csharp" or "cs" => "C#",
            "sql"            => "SQL",
            "bash" or "sh"   => "bash",
            "json"           => "JSON",
            "xml"            => "XML",
            _                => lang,
        };
        return $"""
            <div class="code-block">
              <div class="code-header">
                <span class="code-lang">{System.Net.WebUtility.HtmlEncode(langLabel)}</span>
                <button class="copy-btn" onclick="copyCode(this)">copy</button>
              </div>
              <pre data-lang="{lang}"><code class="language-{lang}">{highlighted}</code></pre>
            </div>
            """;
    });
}

static string HighlightTokens(string code, string[] keywords)
{
    string kwPat  = string.Join("|", keywords.Select(Regex.Escape));
    string pattern = $@"(?<comment>//[^\n]*|/\*[\s\S]*?\*/)|(?<string>@?\$?""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*')|(?<keyword>\b(?:{kwPat})\b)|(?<number>\b\d+(?:\.\d+)?[mMfFdDlLuU]?\b)";

    return Regex.Replace(code, pattern, m =>
    {
        if (m.Groups["comment"].Success) return $"""<span class="tok-com">{m.Value}</span>""";
        if (m.Groups["string"].Success)  return $"""<span class="tok-str">{m.Value}</span>""";
        if (m.Groups["keyword"].Success) return $"""<span class="tok-kw">{m.Value}</span>""";
        if (m.Groups["number"].Success)  return $"""<span class="tok-num">{m.Value}</span>""";
        return m.Value;
    });
}

static string WrapCallouts(string html) =>
    Regex.Replace(
        html,
        @"(<h[23][^>]*>(?:Important Notes?|Best Practices?|Notes?|Pool Safety|Warning|Tip)</h[23]>\s*)(<[uo]l>[\s\S]*?</[uo]l>)",
        m =>
        {
            string heading = m.Groups[1].Value;
            string list    = m.Groups[2].Value;
            bool isWarn = Regex.IsMatch(heading, @"Important|Warning", RegexOptions.IgnoreCase);
            string cls  = isWarn ? "callout callout-warn" : "callout callout-tip";
            return $"{heading}<div class=\"{cls}\">{list}</div>";
        });

// ---------------------------------------------------------------------------
// HTML rendering
// ---------------------------------------------------------------------------
static string RenderPage(string title, PageEntry current, List<SectionEntry> sections)
{
    var sidebar = BuildSidebar(current, sections);
    string enc  = System.Net.WebUtility.HtmlEncode(title);

    return $$"""
        <!DOCTYPE html>
        <html lang="en" class="jt">
        <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>{{enc}} — jaunty docs</title>
        <link rel="preconnect" href="https://fonts.googleapis.com">
        <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
        <link href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:ital,wght@0,400;0,500;0,600;1,400;1,500&display=swap" rel="stylesheet">
        <link rel="stylesheet" href="site.css">
        </head>
        <body class="jt">
        <div class="jt-shell">

          <!-- ===== header ===== -->
          <header class="jt-header">
            <button class="hamburger" aria-label="Toggle navigation" onclick="toggleNav()">&#9776;</button>
            <div class="header-brand">
              <span class="brand-name">jaunty</span>
              <span class="brand-crumb">{{enc}}</span>
            </div>
            <div class="header-spacer"></div>
          </header>

          <!-- ===== body ===== -->
          <div class="jt-body">

            <!-- sidebar backdrop (mobile) -->
            <div class="nav-backdrop" id="nav-backdrop" onclick="toggleNav()"></div>

            <!-- sidebar -->
            <nav class="jt-sidebar" id="jt-sidebar" aria-label="Documentation navigation">
              <div class="sidebar-filter">
                <span class="filter-icon">/</span>
                <input type="text" id="nav-filter" placeholder="filter nav" oninput="filterNav(this.value)" autocomplete="off">
              </div>
              <div class="sidebar-scroll" id="sidebar-scroll">
                {{sidebar}}
              </div>
            </nav>

            <!-- content -->
            <main class="jt-content" id="jt-content">
              <div class="content-inner">
                <nav class="breadcrumb" aria-label="Breadcrumb">{{enc}}</nav>
                <article>
        {{GetBodyHtml(current, sections)}}
                </article>
                <div class="page-nav" id="page-nav">
                  {{BuildPageNav(current, sections)}}
                </div>
              </div>
            </main>
          </div>

          <!-- ===== status bar ===== -->
          <footer class="jt-statusbar">
            <span class="status-dot">&#9679; jaunty docs</span>
          </footer>
        </div>
        <script src="nav.js"></script>
        </body>
        </html>
        """;
}

// Reads body HTML fresh for the given page (already written to disk); re-derive from source.
// Actually we pass it through a static helper that re-processes inline.
static string GetBodyHtml(PageEntry page, List<SectionEntry> sections)
{
    // We can't easily pass it through without re-processing; instead the caller in GenerateSite
    // stores it, but RenderPage is called fresh for index.html. Re-process here.
    var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    string md    = File.ReadAllText(page.SourcePath);
    string body  = Markdown.ToHtml(RewriteMarkdownLinks(md), pipeline);
    body         = PostProcessCodeBlocks(body);
    body         = WrapCallouts(body);
    return body;
}

static string BuildSidebar(PageEntry current, List<SectionEntry> sections)
{
    var sb = new StringBuilder();
    foreach (var section in sections)
    {
        bool sectionActive = section.Pages.Any(p => p.OutputFile == current.OutputFile);
        string openAttr    = sectionActive ? " data-open=\"true\"" : "";
        string chevron     = "›";

        sb.Append($"""
            <div class="nav-group" data-group{openAttr}>
              <div class="nav-group-header" onclick="toggleGroup(this)">
                <span class="nav-chev">{chevron}</span>
                <span class="nav-group-label">{System.Net.WebUtility.HtmlEncode(section.Label)}</span>
              </div>
              <div class="nav-group-items">
            """);

        foreach (var page in section.Pages)
        {
            bool active = page.OutputFile == current.OutputFile;
            string cls  = active ? " class=\"active\"" : "";
            string aria = active ? " aria-current=\"page\"" : "";
            sb.Append($"""
                  <a href="{page.OutputFile}"{cls}{aria}>{System.Net.WebUtility.HtmlEncode(page.Label)}</a>
                """);
        }

        sb.AppendLine("""
              </div>
            </div>
            """);
    }
    return sb.ToString();
}

static string BuildPageNav(PageEntry current, List<SectionEntry> sections)
{
    var allPages = sections.SelectMany(s => s.Pages).ToList();
    int idx      = allPages.FindIndex(p => p.OutputFile == current.OutputFile);
    var sb       = new StringBuilder();

    sb.Append("""<div class="prev-next">""");

    if (idx > 0)
    {
        var prev = allPages[idx - 1];
        sb.Append($"""
            <a class="pn-prev" href="{prev.OutputFile}">
              <span class="pn-label">&#8592; prev</span>
              <span class="pn-title">{System.Net.WebUtility.HtmlEncode(prev.Label)}</span>
            </a>
            """);
    }
    else
    {
        sb.Append("""<span></span>""");
    }

    if (idx >= 0 && idx < allPages.Count - 1)
    {
        var next = allPages[idx + 1];
        sb.Append($"""
            <a class="pn-next" href="{next.OutputFile}">
              <span class="pn-label">next &#8594;</span>
              <span class="pn-title">{System.Net.WebUtility.HtmlEncode(next.Label)}</span>
            </a>
            """);
    }
    else
    {
        sb.Append("""<span></span>""");
    }

    sb.Append("</div>");
    return sb.ToString();
}

// ---------------------------------------------------------------------------
// CSS — dark-theme, JetBrains Mono, extracted from "Jaunty Docs App.dc.html"
// ---------------------------------------------------------------------------
static string SiteCss() => """
    /* =========================================================
       Jaunty Docs — site.css
       Design tokens extracted from docs/07-design/Jaunty Docs App.dc.html
       Dark theme by default; light theme via [data-theme="light"].
    ========================================================= */

    /* ---------- design tokens ---------- */
    .jt {
      --bg:       #0e0e10;
      --bg2:      #0a0a0c;
      --panel:    #141419;
      --border:   #1e1e23;
      --border2:  #26262c;
      --tx:       #e6e6e2;
      --tx2:      #b9b9bd;
      --mut:      #8d8d95;
      --dim:      #63636b;
      --faint:    #4a4a52;
      --hi:       #ffffff;
      --hov:      #15151a;
      --code-bg:  #101013;
      --code-hd:  #121215;
      --code-bd:  #232329;
      --code-tx:  #d6d6d2;
    }

    .jt[data-theme="light"] {
      --bg:       #f6f6f4;
      --bg2:      #efefec;
      --panel:    #e8e8e3;
      --border:   #e1e1da;
      --border2:  #d5d5cd;
      --tx:       #1a1a1d;
      --tx2:      #3c3c42;
      --mut:      #5e5e66;
      --dim:      #8b8b91;
      --faint:    #b0b0b6;
      --hi:       #000000;
      --hov:      #eaeae5;
      --code-bg:  #15151a;
      --code-hd:  #1b1b21;
      --code-bd:  #28282f;
      --code-tx:  #d6d6d2;
    }

    /* ---------- reset / base ---------- */
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

    html.jt {
      font-size: 14px;
      height: 100%;
    }

    body.jt {
      font-family: 'JetBrains Mono', monospace;
      background: var(--bg);
      color: var(--tx);
      height: 100%;
      overflow: hidden;
    }

    /* ---------- shell grid ---------- */
    .jt-shell {
      display: grid;
      grid-template-rows: 46px 1fr 26px;
      height: 100vh;
      overflow: hidden;
    }

    /* ---------- header ---------- */
    .jt-header {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 0 16px;
      background: var(--bg2);
      border-bottom: 1px solid var(--border);
      z-index: 30;
      position: relative;
    }

    .hamburger {
      display: none;
      background: none;
      border: none;
      color: var(--tx);
      font-size: 16px;
      cursor: pointer;
      width: 40px;
      height: 40px;
      align-items: center;
      justify-content: center;
      border-radius: 4px;
      flex: none;
    }
    .hamburger:hover { color: var(--hi); background: var(--hov); }

    .header-brand {
      display: flex;
      align-items: baseline;
      gap: 10px;
    }

    .brand-name {
      font-size: 13.5px;
      font-weight: 600;
      color: var(--hi);
    }

    .brand-crumb {
      font-size: 11px;
      color: var(--dim);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      max-width: 320px;
    }

    .header-spacer { flex: 1; }

    /* ---------- body grid ---------- */
    .jt-body {
      display: grid;
      grid-template-columns: 240px 1fr;
      overflow: hidden;
      min-height: 0;
    }

    /* ---------- sidebar ---------- */
    .nav-backdrop {
      display: none;
      position: fixed;
      inset: 46px 0 26px 0;
      background: rgba(0,0,0,.45);
      z-index: 35;
    }

    .jt-sidebar {
      background: var(--bg);
      border-right: 1px solid var(--border);
      display: flex;
      flex-direction: column;
      overflow: hidden;
      z-index: 40;
    }

    .sidebar-filter {
      display: flex;
      align-items: center;
      gap: 7px;
      margin: 10px 10px 4px;
      padding: 5px 9px;
      background: var(--panel);
      border: 1px solid var(--border2);
      border-radius: 4px;
    }

    .filter-icon {
      font-size: 11px;
      color: var(--faint);
      flex: none;
    }

    .sidebar-filter input {
      flex: 1;
      min-width: 0;
      font: 400 11.5px/1 'JetBrains Mono', monospace;
      color: var(--tx);
      background: transparent;
      border: none;
      outline: none;
    }
    .sidebar-filter input::placeholder { color: var(--faint); }

    .sidebar-scroll {
      flex: 1;
      overflow-y: auto;
      padding: 4px 8px 12px;
      scrollbar-width: thin;
      scrollbar-color: var(--border2) transparent;
    }

    /* nav groups */
    .nav-group { margin-bottom: 2px; }

    .nav-group-header {
      display: flex;
      align-items: center;
      gap: 7px;
      cursor: pointer;
      padding: 5px 7px;
      border-radius: 3px;
    }
    .nav-group-header:hover { background: var(--hov); }

    .nav-chev {
      font-size: 10px;
      color: var(--dim);
      transition: transform .15s ease;
      display: inline-block;
    }
    .nav-group[data-open="true"] .nav-chev { transform: rotate(90deg); }

    .nav-group-label {
      font-size: 10px;
      font-weight: 600;
      letter-spacing: .12em;
      text-transform: uppercase;
      color: var(--mut);
    }

    .nav-group-items {
      display: none;
      flex-direction: column;
      padding: 1px 0 4px;
    }
    .nav-group[data-open="true"] .nav-group-items { display: flex; }

    .nav-group-items a {
      display: block;
      padding: 5px 9px 5px 18px;
      font-size: 12px;
      color: var(--tx2);
      text-decoration: none;
      border-left: 2px solid transparent;
      border-radius: 0 3px 3px 0;
    }
    .nav-group-items a:hover {
      background: var(--hov);
      color: var(--tx);
    }
    .nav-group-items a.active {
      color: var(--hi);
      font-weight: 500;
      border-left-color: var(--tx);
      background: var(--hov);
    }

    /* filter hidden */
    .nav-hidden { display: none !important; }

    /* ---------- content ---------- */
    .jt-content {
      overflow-y: auto;
      min-height: 0;
      background: var(--bg);
      scrollbar-width: thin;
      scrollbar-color: var(--border2) transparent;
    }

    .content-inner {
      max-width: 820px;
      padding: 24px 36px 60px;
      margin: 0 auto;
    }

    /* breadcrumb */
    .breadcrumb {
      font-size: 10.5px;
      color: var(--dim);
      margin-bottom: 16px;
    }

    /* ---------- article typography ---------- */
    article h1 {
      font: 600 25px/1.1 'JetBrains Mono', monospace;
      letter-spacing: -.02em;
      color: var(--hi);
      margin: 0 0 6px;
    }

    article h2 {
      font-size: 15px;
      font-weight: 600;
      color: var(--tx);
      border-bottom: 1px solid var(--border);
      padding-bottom: 6px;
      margin: 32px 0 14px;
    }

    article h3 {
      font-size: 13px;
      font-weight: 600;
      color: var(--tx2);
      margin: 22px 0 10px;
    }

    article h4 {
      font-size: 12px;
      font-weight: 600;
      color: var(--mut);
      text-transform: uppercase;
      letter-spacing: .08em;
      margin: 18px 0 8px;
    }

    article p {
      font: 400 13px/1.85 'JetBrains Mono', monospace;
      color: var(--tx2);
      margin: 0 0 18px;
      max-width: 74ch;
    }

    article ul, article ol {
      margin: 0 0 18px 1.2em;
      padding: 0;
    }

    article li {
      font: 400 12.5px/1.75 'JetBrains Mono', monospace;
      color: var(--tx2);
      margin-bottom: 4px;
    }

    article strong { color: var(--tx); font-weight: 600; }

    article a {
      color: var(--tx);
      text-decoration: underline;
      text-decoration-color: var(--border2);
    }
    article a:hover { text-decoration-color: var(--mut); }

    article hr {
      border: none;
      border-top: 1px solid var(--border);
      margin: 24px 0;
    }

    /* ---------- inline code ---------- */
    article code {
      font: 400 12px/1 'JetBrains Mono', monospace;
      color: var(--tx);
      background: var(--panel);
      border: 1px solid var(--border2);
      border-radius: 3px;
      padding: 1px 5px;
    }

    /* ---------- code blocks ---------- */
    .code-block {
      border: 1px solid var(--code-bd);
      border-radius: 6px;
      margin: 0 0 22px;
      overflow: hidden;
    }

    .code-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 7px 12px;
      background: var(--code-hd);
      border-bottom: 1px solid var(--code-bd);
    }

    .code-lang {
      font-size: 10px;
      letter-spacing: .1em;
      text-transform: uppercase;
      color: var(--dim);
    }

    .copy-btn {
      font: 400 10.5px 'JetBrains Mono', monospace;
      color: var(--mut);
      background: none;
      border: 1px solid var(--border2);
      border-radius: 3px;
      padding: 2px 9px;
      cursor: pointer;
    }
    .copy-btn:hover { color: var(--hi); border-color: var(--faint); }

    article pre {
      margin: 0;
      padding: 14px 16px;
      background: var(--code-bg);
      overflow-x: auto;
      scrollbar-width: thin;
      scrollbar-color: var(--border2) transparent;
    }

    article pre code {
      font: 400 12.5px/1.8 'JetBrains Mono', monospace;
      color: var(--code-tx);
      background: none;
      border: none;
      padding: 0;
    }

    /* syntax tokens */
    .tok-kw  { color: #8b8bdb; font-weight: 600; }
    .tok-str { color: #87b47a; }
    .tok-com { color: var(--faint); font-style: italic; }
    .tok-num { color: #c19a6b; }

    /* ---------- tables ---------- */
    article table {
      border-collapse: collapse;
      width: 100%;
      margin: 0 0 22px;
      font-size: 12px;
    }

    article th, article td {
      border: 1px solid var(--border2);
      padding: 7px 10px;
      text-align: left;
      color: var(--tx2);
    }

    article th {
      background: var(--panel);
      color: var(--tx);
      font-weight: 600;
    }

    article tr:nth-child(even) td { background: rgba(255,255,255,.015); }

    /* ---------- callouts ---------- */
    .callout {
      margin: 0 0 22px;
      padding: 13px 16px;
      background: var(--panel);
      border: 1px solid var(--border2);
      border-left: 3px solid var(--mut);
      border-radius: 0 6px 6px 0;
      font-size: 12px;
      line-height: 1.75;
      color: var(--mut);
    }

    .callout ul, .callout ol { margin: 0; padding-left: 1.2em; color: var(--mut); }

    .callout-warn { border-left-color: #c19a6b; }
    .callout-tip  { border-left-color: #7b9eaf; }

    /* ---------- prev/next ---------- */
    .page-nav { margin-top: 44px; padding-top: 18px; border-top: 1px solid var(--border); }

    .prev-next {
      display: flex;
      justify-content: space-between;
      gap: 20px;
    }

    .pn-prev, .pn-next {
      display: inline-flex;
      flex-direction: column;
      gap: 3px;
      text-decoration: none;
      opacity: .85;
    }
    .pn-next { align-items: flex-end; text-align: right; }
    .pn-prev:hover, .pn-next:hover { opacity: 1; }

    .pn-label { font-size: 10px; color: var(--dim); }
    .pn-title { font-size: 12.5px; font-weight: 600; color: var(--tx); }

    /* ---------- status bar ---------- */
    .jt-statusbar {
      display: flex;
      align-items: center;
      gap: 18px;
      padding: 0 14px;
      background: var(--bg2);
      border-top: 1px solid var(--border);
      font-size: 10px;
      color: var(--dim);
    }

    .status-dot { color: var(--mut); }

    /* ---------- responsive ---------- */
    @media (max-width: 760px) {
      .hamburger { display: flex; }
      .brand-crumb { display: none; }

      .jt-body { grid-template-columns: 1fr; }

      .jt-sidebar {
        position: fixed;
        inset: 46px auto 26px 0;
        width: 260px;
        transform: translateX(-100%);
        transition: transform .2s ease;
        border-right: 1px solid var(--border2);
        box-shadow: 4px 0 24px rgba(0,0,0,.4);
      }
      .jt-sidebar.open { transform: translateX(0); }
      .nav-backdrop.open { display: block; }
    }
    """;

// ---------------------------------------------------------------------------
// Nav JS — collapse/expand groups, filter, copy-code
// ---------------------------------------------------------------------------
static string NavJs() => """
    (function () {
      // ---- group toggle ----
      window.toggleGroup = function (header) {
        var group = header.closest('[data-group]');
        var open  = group.dataset.open === 'true';
        group.dataset.open = open ? 'false' : 'true';
      };

      // ---- sidebar nav (mobile) ----
      window.toggleNav = function () {
        var sidebar  = document.getElementById('jt-sidebar');
        var backdrop = document.getElementById('nav-backdrop');
        if (!sidebar) return;
        var isOpen = sidebar.classList.contains('open');
        sidebar.classList.toggle('open', !isOpen);
        backdrop.classList.toggle('open', !isOpen);
      };

      // ---- nav filter ----
      window.filterNav = function (q) {
        q = q.toLowerCase().trim();
        document.querySelectorAll('.nav-group').forEach(function (group) {
          var items   = group.querySelectorAll('.nav-group-items a');
          var anyVis  = false;
          items.forEach(function (a) {
            var match = !q || a.textContent.toLowerCase().includes(q);
            a.classList.toggle('nav-hidden', !match);
            if (match) anyVis = true;
          });
          group.classList.toggle('nav-hidden', !anyVis);
          if (q && anyVis) group.dataset.open = 'true';
        });
      };

      // ---- copy code ----
      window.copyCode = function (btn) {
        var pre = btn.closest('.code-block').querySelector('pre');
        if (!pre) return;
        var text = pre.innerText || pre.textContent;
        try {
          navigator.clipboard.writeText(text);
          btn.textContent = 'copied!';
          setTimeout(function () { btn.textContent = 'copy'; }, 1800);
        } catch (e) {
          btn.textContent = 'error';
        }
      };

      // ---- scroll active item into view ----
      var active = document.querySelector('.nav-group-items a.active');
      if (active) active.scrollIntoView({ block: 'nearest' });
    })();
    """;

// ---------------------------------------------------------------------------
// Types (must follow all local functions in a top-level program)
// ---------------------------------------------------------------------------
record PageEntry(string Label, string SourcePath, string OutputFile, string SectionId);
record SectionEntry(string Id, string Label, PageEntry[] Pages);
