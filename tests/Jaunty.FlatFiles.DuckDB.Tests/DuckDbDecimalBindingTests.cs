using System.Data;

using DuckDB.NET.Data;

using Jaunty.Dialects;
using Jaunty.FlatFiles.DuckDB.Dialects;

namespace Jaunty.FlatFiles.DuckDB.Tests;

/// <summary>
/// AUD-R26-050. <c>DecimalBindingDialectTests</c> in <c>Jaunty.Tests</c> pins which dialects ask for
/// the decimal-to-double conversion, but it can only scan the assemblies that project references -
/// which does not include this one. <see cref="DuckDbDialect"/> is a shipping
/// <see cref="ISqlDialect"/>, registered for <c>DuckDBConnection</c> by <c>DuckDb.cs</c>, so the
/// fluent builders resolve it and it is inside the blast radius of that fix while being invisible to
/// its drift test. This closes that hole from the side that can see it.
///
/// <para>
/// AUD-R26-050 changed DuckDB's behaviour and did not say so: the coercion it removed was
/// unconditional, so a <see cref="decimal"/> bound by the joined or grouped builders against DuckDB
/// used to be flattened to <see cref="double"/> and now is not. That is an improvement rather than a
/// regression, but it was shipped unmeasured, so it is measured here.
/// </para>
/// </summary>
public class DuckDbDecimalBindingTests
{
    /// <summary>
    /// DuckDB must not ask for the conversion - it costs 13 significant digits and buys nothing
    /// here. The counterpart of <c>NoOtherDialect_DeclaresTheConversion</c>, for the assembly that
    /// test cannot see.
    /// </summary>
    [Fact]
    public void TheDuckDbDialect_DoesNotAskForTheDecimalConversion()
    {
        Assert.False(
            DuckDbDialect.Instance is IDecimalBindingDialect,
            "DuckDB compares a bound decimal correctly; converting it would only lose precision.");

        object? bound = DecimalParameterBinding.Normalize(DuckDbDialect.Instance, decimal.MaxValue);

        Assert.IsType<decimal>(bound);
        Assert.Equal(decimal.MaxValue, (decimal)bound!);
    }

    /// <summary>
    /// And the reason it must not: measured in raw ADO.NET, DuckDB compares a bound
    /// <see cref="decimal"/> correctly in every shape that defeats SQLite - column equality, and
    /// against both an aggregate and an arithmetic expression. If a DuckDB.NET upgrade ever changed
    /// that, the assertion above would become wrong and this is what would say so.
    /// </summary>
    [Fact]
    public void DuckDb_ComparesABoundDecimalCorrectly_IncludingAgainstExpressions()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        Execute(connection, "CREATE TABLE t (price DECIMAL(18,4), cat INTEGER)");
        Execute(connection, "INSERT INTO t VALUES (40.5, 1), (121.5, 1), (30.0, 2)");

        // Category 1 sums to 162.0, category 2 to 30.0.
        Assert.Equal(1, RowCount(connection, "SELECT 1 FROM t WHERE price = $p", 40.5m));
        Assert.Equal(1, RowCount(connection, "SELECT 1 FROM t GROUP BY cat HAVING SUM(price) > $p", 150m));
        Assert.Equal(1, RowCount(connection, "SELECT 1 FROM t GROUP BY cat HAVING SUM(price) < $p", 150m));
        Assert.Equal(1, RowCount(connection, "SELECT 1 FROM t WHERE price * 1 > $p", 100m));
    }

    private static void Execute(DuckDBConnection connection, string sql)
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static int RowCount(DuckDBConnection connection, string sql, decimal value)
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = sql;

        IDbDataParameter parameter = command.CreateParameter();
        parameter.ParameterName = "p";
        parameter.Value = value;
        command.Parameters.Add(parameter);

        int rows = 0;
        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
            rows++;

        return rows;
    }
}
