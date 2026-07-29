using System.Reflection;

using Jaunty.Dialects;

using Xunit;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// AUD-R26 (batch 5, medium/bug). <see cref="IDecimalBindingDialect"/> carries a provider fact that
/// used to be hard-coded into two fluent builders: both SQLite providers bind a
/// <see cref="decimal"/> parameter as TEXT, and SQLite only converts a TEXT operand when it is
/// compared against a <em>column</em>, whose affinity it can apply. Against an expression there is
/// no affinity, and TEXT sorts above every number - so <c>HAVING SUM(price) &gt; @p</c> matches
/// nothing and <c>&lt; @p</c> matches everything, whatever the values are.
///
/// <para>
/// Two things need pinning, and they pull in opposite directions. The conversion must still reach
/// SQLite - dropping it is what the audit finding proposed, and it turns four
/// <c>GroupBy</c>/<c>Having</c> integration tests red - and it must <em>not</em> reach the other
/// three engines, whose providers bind a decimal exactly and whose <c>DECIMAL(19,4)</c> and
/// <c>NUMERIC</c> comparisons the old unconditional coercion was downgrading to binary floating
/// point for no reason.
/// </para>
///
/// <para>
/// Like <see cref="ISubstringToEndDialect"/>, this is an optional interface rather than an
/// <see cref="ISqlDialect"/> member, so the compiler cannot enforce it. The drift that matters here
/// is a wrapper: <c>SQLiteDialectWithBulkCopy</c> forwards ~40 members by hand, and one it forgot
/// would mean installing <c>Jaunty.Extensions.Reflection</c> silently reintroduces the comparison
/// failure. These tests are what replaces the compiler.
/// </para>
/// </summary>
public class DecimalBindingDialectTests
{
    private static List<Type> ShippingDialects()
    {
        Assembly[] shipping =
        [
            typeof(ISqlDialect).Assembly,                                   // Jaunty
            typeof(global::Jaunty.Extensions.Reflection.JauntyReflectionExtensions).Assembly,
        ];

        List<Type> dialects = [];

        foreach (Assembly assembly in shipping)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(ISqlDialect).IsAssignableFrom(type)) continue;

                dialects.Add(type);
            }
        }

        // The premise: if this finds nothing, every test below would pass vacuously.
        Assert.True(dialects.Count >= 8, $"expected the 4 dialects and 4 bulk-copy wrappers, found {dialects.Count}");
        return dialects;
    }

    /// <summary>
    /// The four bulk-copy wrappers take an optional inner dialect, which is not a parameterless
    /// constructor as far as <see cref="Activator"/> is concerned. Pick the narrowest constructor
    /// and supply its defaults, so a wrapper builds its own inner dialect exactly as it does in
    /// production.
    /// </summary>
    private static ISqlDialect Instantiate(Type type)
    {
        ConstructorInfo ctor = type
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderBy(c => c.GetParameters().Length)
            .First();

        object?[] args = [.. ctor.GetParameters()
            .Select(p => p.HasDefaultValue ? p.DefaultValue : null)];

        return (ISqlDialect)ctor.Invoke(args);
    }

    /// <summary>
    /// The SQLite dialect and every wrapper around it must declare the interface. Discovered by
    /// name rather than listed, so a wrapper added later is covered without anyone remembering.
    /// </summary>
    [Fact]
    public void EverySqliteDialect_DeclaresTheConversion()
    {
        string[] missing = [.. ShippingDialects()
            .Where(t => t.Name.IndexOf("SQLite", StringComparison.OrdinalIgnoreCase) >= 0)
            .Where(t => !typeof(IDecimalBindingDialect).IsAssignableFrom(t))
            .Select(t => t.FullName!)];

        Assert.True(
            missing.Length == 0,
            $"SQLite dialects that bind a decimal as TEXT but do not declare {nameof(IDecimalBindingDialect)}: "
            + string.Join(", ", missing));
    }

    /// <summary>
    /// And nothing else may. This is the half of the fix that the finding was actually right
    /// about - the conversion costs 13 significant digits, and on these three engines it buys
    /// nothing, because their providers bind a decimal as a decimal.
    /// </summary>
    [Fact]
    public void NoOtherDialect_DeclaresTheConversion()
    {
        string[] unexpected = [.. ShippingDialects()
            .Where(t => t.Name.IndexOf("SQLite", StringComparison.OrdinalIgnoreCase) < 0)
            .Where(t => typeof(IDecimalBindingDialect).IsAssignableFrom(t))
            .Select(t => t.FullName!)];

        Assert.True(
            unexpected.Length == 0,
            "these providers bind a decimal exactly, so converting it only loses precision: "
            + string.Join(", ", unexpected));
    }

    /// <summary>
    /// The conversion itself, through the one helper every caller goes through. A dialect that
    /// declares the interface must actually hand back something SQLite reads as a number - a
    /// forwarding wrapper that returned the decimal unchanged would satisfy the interface and fix
    /// nothing.
    /// </summary>
    [Fact]
    public void ASqliteDialect_ConvertsADecimalToADouble()
    {
        foreach (Type type in ShippingDialects()
            .Where(t => typeof(IDecimalBindingDialect).IsAssignableFrom(t)))
        {
            ISqlDialect dialect = Instantiate(type);

            object? bound = DecimalParameterBinding.Normalize(dialect, 150m);

            Assert.IsType<double>(bound);
            Assert.Equal(150.0d, (double)bound!);
        }
    }

    /// <summary>
    /// Everything that is not a decimal passes through untouched, on every dialect. The helper sees
    /// every parameter the two builders bind, not just the numeric ones.
    /// </summary>
    [Fact]
    public void EveryOtherValue_PassesThroughUnchanged()
    {
        object?[] values = ["Chai", 42, 3.5d, null, new DateTime(2020, 1, 1)];

        foreach (Type type in ShippingDialects())
        {
            ISqlDialect dialect = Instantiate(type);

            foreach (object? value in values)
                Assert.Same(value, DecimalParameterBinding.Normalize(dialect, value));
        }
    }

    /// <summary>
    /// A decimal is left alone on the dialects that do not ask for the conversion, at full
    /// precision. <c>decimal.MaxValue</c> because it is the value the finding cited: as a
    /// <see cref="double"/> it becomes 7.922816251426434E+28.
    /// </summary>
    [Fact]
    public void ANonSqliteDialect_BindsTheDecimalUnchanged()
    {
        foreach (Type type in ShippingDialects()
            .Where(t => !typeof(IDecimalBindingDialect).IsAssignableFrom(t)))
        {
            ISqlDialect dialect = Instantiate(type);

            object? bound = DecimalParameterBinding.Normalize(dialect, decimal.MaxValue);

            Assert.IsType<decimal>(bound);
            Assert.Equal(decimal.MaxValue, (decimal)bound!);
        }
    }
}
