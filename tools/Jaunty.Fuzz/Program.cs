using System.Text;
using Jaunty.Internals.Parameters;
using SharpFuzz;

namespace Jaunty.Fuzz;

/// <summary>
/// libFuzzer harness for <see cref="SqlParameterParser.ExtractParameterNames"/>, the span-based
/// walker that runs on every query Jaunty executes with parameters. The oracle is total-ness plus
/// two shape invariants, not correctness: the parser is called unguarded on the hot path, so any
/// escaping exception surfaces to a caller who passed a perfectly ordinary-looking statement.
///
/// <para>
/// Both escape modes are driven from the same input, because they are different state machines
/// over the same text: <c>backslashEscapes: true</c> is the MySQL/MariaDB rule where a backslash
/// escapes the next character inside a string literal, and applying it to any other engine
/// swallows a literal's terminator. A crash reachable in only one mode is still a crash.
/// </para>
///
/// <para>
/// Run (Linux, libFuzzer). The last line must go through the native driver: launched as a plain
/// <c>dotnet Jaunty.Fuzz.dll</c>, SharpFuzz finds none of the IPC environment variables and
/// replays args[1] as a single file instead of fuzzing.
/// <code>
///   dotnet publish tools/Jaunty.Fuzz -c Release -o out/fuzz
///   sharpfuzz out/fuzz/Jaunty.dll
///   ./libfuzzer-dotnet -max_total_time=600 --target_path="$(command -v dotnet)" \
///     --target_arg=out/fuzz/Jaunty.Fuzz.dll tools/Jaunty.Fuzz/corpus
/// </code>
/// </para>
///
/// <para>
/// Minimise any crash before promoting it (<c>-minimize_crash=1</c>), then add it to
/// <c>ParameterParserPropertyTests</c> as a named case with the reason it was kept — the corpus
/// discipline the plan asks for. A crasher that only lives in the corpus directory is a finding
/// nobody re-checks.
/// </para>
/// </summary>
public static class Program
{
    public static void Main(string[] args) => Fuzzer.LibFuzzer.Run(Fuzz);

    private static void Fuzz(ReadOnlySpan<byte> bytes)
    {
        string sql;
        try
        {
            sql = Encoding.UTF8.GetString(bytes);
        }
        catch (ArgumentException)
        {
            // Not a defect in the parser: every caller hands it a string that already exists.
            return;
        }

        Check(SqlParameterParser.ExtractParameterNames(sql, backslashEscapes: false));
        Check(SqlParameterParser.ExtractParameterNames(sql, backslashEscapes: true));
    }

    /// <summary>
    /// The invariants a caller relies on: a returned name is used to look up a property and then
    /// to build a DbParameter, so an empty name or one carrying a sigil or a quote would fail far
    /// from here, with nothing pointing back at the SQL that produced it.
    /// </summary>
    private static void Check(string[] names)
    {
        foreach (string name in names)
        {
            if (name.Length == 0)
                throw new InvalidOperationException("ExtractParameterNames returned an empty name.");

            foreach (char c in name)
            {
                if (!SqlParameterParser.IsParameterChar(c))
                    throw new InvalidOperationException(
                        $"ExtractParameterNames returned '{name}', which contains '{c}'.");
            }
        }
    }
}
