// Emits the linker alias flags that map System.Data.SQLite's mangled export names
// onto the plain names a from-source build of SQLite.Interop produces.
//
// The System.Data.SQLite NuGet package ships a managed assembly whose P/Invokes
// import mangled names ("SI" + 16 hex digits) rather than the sqlite3_* names the
// published source builds. The mangling is applied after the fact by the vendor's
// build and is not reproducible from the source drop, so the map is recovered
// instead: the assembly's P/Invoke table gives (managed method -> mangled name),
// and UnsafeNativeMethods.cs gives (managed method -> real entry point).
//
// Deriving the map from the installed package rather than hard-coding it means a
// version bump self-corrects: the names change, and so does the map.
//
//   usage: manglemap <System.Data.SQLite.dll> <UnsafeNativeMethods.cs> <exports.txt>
//
// exports.txt is `nm -gU` output for a first-pass link of the library with no
// aliases applied; it is what decides which aliases are actually resolvable.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

if (args.Length != 3)
{
    Console.Error.WriteLine("usage: manglemap <System.Data.SQLite.dll> <UnsafeNativeMethods.cs> <exports.txt>");
    return 2;
}

// Variadic entry points that System.Data.SQLite P/Invokes with fixed signatures.
// Aliasing the mangled name straight onto these would reinstate the arm64 ABI
// mismatch, so they are redirected to the wrappers in arm64-varargs-shim.c.
Dictionary<string, string> shimmed = new(StringComparer.Ordinal)
{
    ["sqlite3_config"] = "jaunty_shim_sqlite3_config",
    ["sqlite3_db_config"] = "jaunty_shim_sqlite3_db_config",
};

Dictionary<string, string> mangled = ReadPInvokes(args[0]);
Dictionary<string, string> entryPoints = ReadEntryPoints(args[1]);
HashSet<string> exports = ReadExports(args[2]);

SortedDictionary<string, string> aliases = new(StringComparer.Ordinal);
List<string> dropped = [];

foreach ((string method, string mangledName) in mangled)
{
    string target = entryPoints.TryGetValue(method, out string? entryPoint) ? entryPoint : method;

    if (shimmed.TryGetValue(target, out string? wrapper))
        target = wrapper;

    if (!exports.Contains(target))
    {
        dropped.Add($"{method} -> {target}");
        continue;
    }

    // Several managed declarations can share one native entry point, and therefore
    // one mangled name - the three sqlite3_config_* overloads, for instance. They
    // must agree on the target.
    if (aliases.TryGetValue(mangledName, out string? existing) && existing != target)
        throw new InvalidOperationException($"{mangledName} maps to both '{existing}' and '{target}'.");

    aliases[mangledName] = target;
}

foreach ((string mangledName, string target) in aliases)
    Console.WriteLine($"-Wl,-alias,_{target},_{mangledName}");

Console.Error.WriteLine($"manglemap: {aliases.Count} alias(es) from {mangled.Count} mangled import(s).");

if (dropped.Count > 0)
{
    // Expected on macOS: the three sqlite3_win32_* entry points, which the shipped
    // osx-x64 binary does not export either. Anything else here is a real gap and
    // will surface as an EntryPointNotFoundException at runtime, so it is listed
    // rather than silently swallowed.
    Console.Error.WriteLine($"manglemap: {dropped.Count} import(s) with no matching export:");
    foreach (string entry in dropped)
        Console.Error.WriteLine($"  {entry}");
}

return 0;

static Dictionary<string, string> ReadPInvokes(string assemblyPath)
{
    Dictionary<string, string> result = new(StringComparer.Ordinal);

    using FileStream stream = File.OpenRead(assemblyPath);
    using PEReader pe = new(stream);
    MetadataReader metadata = pe.GetMetadataReader();

    foreach (MethodDefinitionHandle handle in metadata.MethodDefinitions)
    {
        MethodDefinition method = metadata.GetMethodDefinition(handle);
        MethodImport import = method.GetImport();

        if (import.Name.IsNil)
            continue;

        string importName = metadata.GetString(import.Name);

        // The mangled form is "SI" followed by 16 lowercase hex digits. Plain
        // imports (dlopen, uname, the Win32 helpers) are left alone.
        if (!IsMangled(importName))
            continue;

        result[metadata.GetString(method.Name)] = importName;
    }

    return result;
}

static bool IsMangled(string name)
{
    if (name.Length != 18 || name[0] != 'S' || name[1] != 'I')
        return false;

    for (int i = 2; i < name.Length; i++)
    {
        if (!Uri.IsHexDigit(name[i]))
            return false;
    }

    return true;
}

// Pairs each `internal static extern` declaration with the DllImport attribute
// above it. A regex spanning both is not enough: the attribute and the signature
// are routinely separated by #if/#else blocks that vary the calling convention,
// so the attribute is remembered and consumed when the signature arrives.
static Dictionary<string, string> ReadEntryPoints(string sourcePath)
{
    Regex dllImport = new(@"\[DllImport\((?<args>[^\]]*)\)\]", RegexOptions.Compiled);
    Regex entryPoint = new(@"EntryPoint\s*=\s*""(?<name>[^""]+)""", RegexOptions.Compiled);
    Regex signature = new(@"\bstatic\s+extern\s+.*?(?<name>\w+)\s*\(", RegexOptions.Compiled);

    Dictionary<string, string> result = new(StringComparer.Ordinal);
    string? pending = null;
    bool pendingIsSqlite = false;

    foreach (string line in File.ReadLines(sourcePath))
    {
        Match import = dllImport.Match(line);
        if (import.Success)
        {
            string arguments = import.Groups["args"].Value;
            pendingIsSqlite = arguments.Contains("SQLITE_DLL", StringComparison.Ordinal);
            Match name = entryPoint.Match(arguments);
            pending = name.Success ? name.Groups["name"].Value : null;
            continue;
        }

        Match declaration = signature.Match(line);
        if (!declaration.Success)
            continue;

        if (pendingIsSqlite)
        {
            // No explicit EntryPoint means the entry point is the method name.
            result[declaration.Groups["name"].Value] = pending ?? declaration.Groups["name"].Value;
        }

        pending = null;
        pendingIsSqlite = false;
    }

    return result;
}

static HashSet<string> ReadExports(string exportsPath)
{
    HashSet<string> result = new(StringComparer.Ordinal);

    foreach (string line in File.ReadLines(exportsPath))
    {
        // `nm -gU` prints "<address> T _symbol"; Mach-O prefixes C symbols with '_'.
        string[] fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length == 0)
            continue;

        string symbol = fields[^1];
        result.Add(symbol.StartsWith('_') ? symbol[1..] : symbol);
    }

    return result;
}
