#if CSCHECK

using System.Reflection;

using CsCheck;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Import;

public class CsvImportIdentifierFuzzTests
{
    private static readonly MethodInfo ValidateIdentifierMethod = ClassicImplementation();

    private static void ValidateIdentifier(string identifier, string paramName)
    {
        try
        {
            ValidateIdentifierMethod.Invoke(null, [identifier, paramName]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static readonly string[] MalformedIdentifiers =
    [
        "", " ", "\t", "\n", "\r\n",
        "'; DROP TABLE users; --",
        "table\"; DROP TABLE users; --",
        "table`; DROP TABLE users; --",
        "a b", "a-b", "a/b", "a\\b", "a;b", "a'b", "a\"b", "a`b",
        "1table", "table.1column",
        "table.column.extra",
        "table.", ".table", "..",
        "a\0b", "\0", "a\0",
        "café", "テーブル", "ácombining", "a​b",
        "a\r\nb", "a\n",
        "/* comment */table", "table -- comment",
    ];

    [Fact]
    public void MalformedIdentifiers_AlwaysThrowArgumentExceptionNamingTheParameter()
    {
        foreach (string identifier in MalformedIdentifiers)
        {
            var ex = Assert.Throws<ArgumentException>(() => ValidateIdentifier(identifier, "tableName"));
            Assert.Equal("tableName", ex.ParamName);
        }
    }

    private static Gen<string> GenValidIdentifierSegment =>
        Gen.Select(
            Gen.Char['a', 'z'],
            Gen.OneOf(
                Gen.Const(Array.Empty<char>()),
                Gen.OneOf(Gen.Char['a', 'z'], Gen.Char['0', '9'], Gen.Const('_')).Array[1, 20]))
            .Select((first, rest) => first + new string(rest));

    private static Gen<string> GenValidIdentifier =>
        Gen.Select(GenValidIdentifierSegment, Gen.Bool.SelectMany(hasSchema =>
            hasSchema ? GenValidIdentifierSegment.Select(s => "." + s) : Gen.Const("")))
            .Select((a, b) => a + b);

    [Fact]
    public void GeneratedValidIdentifiers_NeverThrow()
    {
        GenValidIdentifier.Sample(identifier => ValidateIdentifier(identifier, "tableName"), iter: 5_000);
    }

    [Fact]
    public void ArbitraryText_NeverThrowsAnythingOtherThanArgumentException()
    {
        Gen.Char[(char)1, (char)0x2100].Array[0, 64].Select(cs => new string(cs)).Sample(text =>
        {
            try
            {
                ValidateIdentifier(text, "tableName");
            }
            catch (ArgumentException)
            {
            }
        }, iter: 20_000);
    }

    private static MethodInfo ClassicImplementation()
    {
        MethodInfo? method = typeof(Extrode.Jaunty.CsvImportExtensions).GetMethod(
            "ValidateIdentifier",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.True(method is not null,
            "CsvImportExtensions.ValidateIdentifier was not found. If it was renamed or removed, " +
            "update this test rather than deleting it.");

        return method!;
    }
}

#endif
