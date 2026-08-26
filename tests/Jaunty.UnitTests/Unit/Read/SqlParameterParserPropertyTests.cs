#if CSCHECK

using System.Reflection;
using System.Text;

using CsCheck;

using Jaunty.Internals.Parameters;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// Property-based companion to <see cref="SqlParameterParserTests"/>'s 43 curated cases.
///
/// <para>
/// The parser ships twice. <c>ExtractParameterNamesSpan</c> serves net8.0 and net10.0;
/// <c>ExtractParameterNamesClassic</c> is a hand-maintained duplicate that serves the
/// netstandard2.0 build, which is what .NET Framework consumers load. Measured 2026-08-27, line
/// coverage of the two halves on the net10.0 leg was <b>81/81</b> and <b>3/80</b>: the classic
/// half is compiled but dead on modern targets, and CI has no net472 leg to run it (ci.yml is
/// ubuntu-latest throughout), so nothing in CI has ever executed it.
/// </para>
///
/// <para>
/// <see cref="BothImplementationsAgree"/> closes that by calling the classic method directly
/// through reflection on the same target CI already runs. Divergence between the two is a silent
/// wrong-parameter-set for .NET Framework users - the failure mode with no error message.
/// </para>
/// </summary>
public class SqlParameterParserPropertyTests
{
    /// <summary>
    /// Fragments chosen for the constructs the parser has dedicated branches for, so generated
    /// statements land inside quoting, comment and sigil handling rather than on plain text.
    /// </summary>
    private static readonly string[] Fragments =
    [
        "SELECT", "*", "FROM", "t", "WHERE", "AND", "OR", "=", ",", " ", "\t", "\n", "\r\n",
        "@p", "@@IDENTITY", "@@ROWCOUNT", "$1", "$p", "$", "@", "a@b", "x$y", "_9",
        "'lit'", "'it''s'", "'@notparam'", "'unterminated", "'a\\'", "\\",
        "\"quoted\"", "\"@notparam\"", "\"\"\"\"", "[bracket]", "[@notparam]", "[unclosed",
        "`tick`", "`it's`", "`@notparam`", "`unclosed",
        "-- @comment", "/* @block */", "/* unterminated", "*/",
        "$$body$$", "$tag$@x$tag$", "$$unterminated", "$tag$",
        "\u0085", "\u2028", "\u2029", "\u00e9", "\0",
    ];

    private static Gen<string> GenSql =>
        Gen.Const(Fragments).SelectMany(f => Gen.Int[0, 24].SelectMany(n =>
            Gen.Int[0, f.Length - 1].Array[n].Select(ix =>
            {
                StringBuilder sb = new();
                foreach (int i in ix)
                    sb.Append(f[i]);
                return sb.ToString();
            })));

    /// <summary>Arbitrary text, to reach shapes the fragment vocabulary cannot compose.</summary>
    private static Gen<string> GenNoise =>
        Gen.Char[(char)1, (char)0x2030].Array[0, 64].Select(cs => new string(cs));

    [Fact]
    public void ExtractionIsTotalOverGeneratedSql()
    {
        Gen.Select(GenSql, Gen.Bool).Sample((sql, backslashEscapes) =>
        {
            string[] names = SqlParameterParser.ExtractParameterNames(sql, backslashEscapes);
            Assert.NotNull(names);
        }, iter: 20_000);
    }

    [Fact]
    public void ExtractionIsTotalOverArbitraryText()
    {
        Gen.Select(GenNoise, Gen.Bool).Sample((sql, backslashEscapes) =>
        {
            string[] names = SqlParameterParser.ExtractParameterNames(sql, backslashEscapes);
            Assert.NotNull(names);
        }, iter: 20_000);
    }

    [Fact]
    public void EveryExtractedNameIsAValidParameterName()
    {
        Gen.Select(GenSql, Gen.Bool).Sample((sql, backslashEscapes) =>
        {
            foreach (string name in SqlParameterParser.ExtractParameterNames(sql, backslashEscapes))
            {
                Assert.NotEmpty(name);

                foreach (char c in name)
                    Assert.True(SqlParameterParser.IsParameterChar(c),
                        $"'{name}' from \"{sql}\" contains '{c}', which is not a parameter character.");
            }
        }, iter: 20_000);
    }

    [Fact]
    public void ExtractionIsDeterministic()
    {
        Gen.Select(GenSql, Gen.Bool).Sample((sql, backslashEscapes) =>
        {
            Assert.Equal(
                SqlParameterParser.ExtractParameterNames(sql, backslashEscapes),
                SqlParameterParser.ExtractParameterNames(sql, backslashEscapes));
        }, iter: 5_000);
    }

    [Fact]
    public void BothImplementationsAgree()
    {
        MethodInfo classic = ClassicImplementation();

        Gen.Select(GenSql, Gen.Bool).Sample((sql, backslashEscapes) =>
        {
            string[] span = SqlParameterParser.ExtractParameterNames(sql, backslashEscapes);
            string[] legacy = InvokeClassic(classic, sql, backslashEscapes);

            Assert.True(span.SequenceEqual(legacy),
                $"span and classic disagree on \"{Printable(sql)}\" (backslashEscapes: " +
                $"{backslashEscapes}). span: [{string.Join(", ", span)}] classic: " +
                $"[{string.Join(", ", legacy)}]. The classic body is what netstandard2.0 " +
                "consumers execute.");
        }, iter: 20_000);
    }

    [Fact]
    public void BothImplementationsAgreeOnArbitraryText()
    {
        MethodInfo classic = ClassicImplementation();

        Gen.Select(GenNoise, Gen.Bool).Sample((sql, backslashEscapes) =>
        {
            string[] span = SqlParameterParser.ExtractParameterNames(sql, backslashEscapes);
            string[] legacy = InvokeClassic(classic, sql, backslashEscapes);

            Assert.True(span.SequenceEqual(legacy),
                $"span and classic disagree on \"{Printable(sql)}\" (backslashEscapes: " +
                $"{backslashEscapes}).");
        }, iter: 20_000);
    }

    /// <summary>
    /// Both overloads of <see cref="SqlParameterParser.IsSigilInsideIdentifier(string, int)"/>
    /// exist so the classic path has a string-based twin; they must not diverge either.
    /// </summary>
    [Fact]
    public void SigilOverloadsAgree()
    {
        Gen.Select(GenSql, Gen.Int[0, 64]).Sample((sql, offset) =>
        {
            if (sql.Length == 0)
                return;

            int position = offset % sql.Length;

            Assert.Equal(
                SqlParameterParser.IsSigilInsideIdentifier(sql, position),
                SqlParameterParser.IsSigilInsideIdentifier(sql.AsSpan(), position));
        }, iter: 20_000);
    }

    private static MethodInfo ClassicImplementation()
    {
        MethodInfo? method = typeof(SqlParameterParser).GetMethod(
            "ExtractParameterNamesClassic",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.True(method is not null,
            "SqlParameterParser.ExtractParameterNamesClassic was not found. If it was renamed or " +
            "removed, this fact stops comparing anything - update it rather than deleting it, " +
            "because the netstandard2.0 build has no other test on this leg.");

        return method!;
    }

    private static string[] InvokeClassic(MethodInfo classic, string sql, bool backslashEscapes)
    {
        // The public entry point short-circuits whitespace-only input before dispatching; the
        // classic body itself is only ever reached past that guard, so mirror it here.
        if (string.IsNullOrWhiteSpace(sql))
            return [];

        return (string[])classic.Invoke(null, [sql, backslashEscapes])!;
    }

    private static string Printable(string sql) =>
        sql.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\0", "\\0");
}

#endif
