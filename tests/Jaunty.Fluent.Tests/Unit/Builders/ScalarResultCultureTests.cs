using System.Data;
using System.Globalization;

using Jaunty.Dialects;
using Jaunty.Fluent.Tests.Entities;

namespace Jaunty.Fluent.Tests.Unit.Builders;

/// <summary>
/// AUD-R25: <c>QueryBuilder.ConvertScalarResult&lt;TResult&gt;</c> called
/// <c>Convert.ChangeType(value, underlyingType)</c> with no <see cref="IFormatProvider"/>, so it ran
/// under the ambient <see cref="CultureInfo.CurrentCulture"/>. It backs all six scalar terminals
/// (<c>Sum</c>/<c>Min</c>/<c>Max</c> and their <c>Select*</c> aliases), whose values come straight
/// out of the provider - and providers routinely hand back a <see cref="string"/> where the column
/// is TEXT/NUMERIC (SQLite in particular). Under a comma-decimal culture
/// <c>Convert.ChangeType("1.5", typeof(decimal))</c> does not throw: it reads the period as a group
/// separator and returns 15, silently corrupting the aggregate by a factor of ten.
///
/// <para>
/// Driven through a fake connection whose <c>ExecuteScalar</c> returns a string, so the assertion is
/// about the conversion rather than about any particular provider's scalar typing.
/// </para>
/// </summary>
public class ScalarResultCultureTests
{
    public ScalarResultCultureTests()
        => SqlDialectFactory.RegisterDialect(nameof(ScalarConnection), new SQLiteDialect());

    private static void InCulture(string cultureName, Action body)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            body();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("pt-BR")]
    public void Sum_StringScalarFromProvider_IsCultureInvariant(string culture)
    {
        var connection = new ScalarConnection("1.5");

        InCulture(culture, () =>
            Assert.Equal(1.5m, connection.From<Product>().Sum(p => p.UnitPrice)));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("pt-BR")]
    public void Max_StringScalarFromProvider_IsCultureInvariant(string culture)
    {
        var connection = new ScalarConnection("1234.56");

        InCulture(culture, () =>
            Assert.Equal(1234.56m, connection.From<Product>().Max(p => p.UnitPrice)));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("pt-BR")]
    public void Min_StringScalarFromProvider_IsCultureInvariant(string culture)
    {
        var connection = new ScalarConnection("0.25");

        InCulture(culture, () =>
            Assert.Equal(0.25m, connection.From<Product>().Min(p => p.UnitPrice)));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void Sum_StringScalarThatIsAValidGroupSeparatorSpelling_DoesNotSilentlyScale(string culture)
    {
        // "1.234" is a valid de-DE spelling of 1234, so the conversion succeeds and returns the
        // wrong number rather than throwing - the reason this was silent.
        var connection = new ScalarConnection("1.234");

        InCulture(culture, () =>
            Assert.Equal(1.234m, connection.From<Product>().Sum(p => p.UnitPrice)));
    }

    [Fact]
    public void Sum_UnderInvariantCulture_IsUnchanged()
    {
        var connection = new ScalarConnection("1.5");

        InCulture("en-US", () =>
            Assert.Equal(1.5m, connection.From<Product>().Sum(p => p.UnitPrice)));
    }

    // ------------------------------------------------------------------
    // Fake connection: ExecuteScalar returns a caller-supplied value
    // ------------------------------------------------------------------

    private sealed class ScalarConnection(object scalar) : IDbConnection
    {
        private readonly object _scalar = scalar;

        public string ConnectionString { get => ""; set { } }
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Open;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => new ScalarCommand(this, _scalar);
        public void Open() { }
        public void Dispose() { }
    }

    private sealed class ScalarCommand(IDbConnection connection, object scalar) : IDbCommand
    {
        private readonly object _scalar = scalar;

        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; } = connection;
        public IDataParameterCollection Parameters { get; } = new ParameterCollection();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new Parameter();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => throw new NotSupportedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
        public object? ExecuteScalar() => _scalar;
        public void Prepare() { }
    }

    private sealed class Parameter : IDbDataParameter
    {
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        public string ParameterName { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
    }

    private sealed class ParameterCollection : List<object>, IDataParameterCollection
    {
        public object this[string parameterName]
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public bool Contains(string parameterName) => false;
        public int IndexOf(string parameterName) => -1;
        public void RemoveAt(string parameterName) { }
    }
}
