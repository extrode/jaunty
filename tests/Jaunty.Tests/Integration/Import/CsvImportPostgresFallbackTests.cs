using System.Data;

using Xunit;

namespace Jaunty.Tests.Integration.Import.PostgresFallback;

/// <summary>
/// AUD-R30: the PostgreSQL server-side COPY FROM fallback (taken when the connection has no
/// Npgsql <c>BeginTextImport</c>) silently discarded a non-default
/// <c>CsvImportOptions.Encoding</c> - the server opens the file, so the setting cannot be
/// honoured and must fail loudly like the sqlite3-CLI/LOAD DATA/BULK INSERT paths. Isolated in
/// its own file because the fake below must be <i>named</i> <c>NpgsqlConnection</c> for
/// <c>SqlDialectFactory</c>'s type-name switch, which would shadow the real Npgsql type for
/// the live-server tests in <c>CsvImportTests</c>.
/// </summary>
public class CsvImportPostgresFallbackTests
{
    [Fact]
    public void ImportCsv_ServerSideFallback_NonDefaultEncodingThrows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"jaunty_pgfb_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "Name,Age\nAlice,30\n");
        try
        {
            using var connection = new NpgsqlConnection();
            var options = new CsvImportOptions { Encoding = System.Text.Encoding.GetEncoding("ISO-8859-1") };

            var ex = Assert.Throws<NotSupportedException>(() => connection.ImportCsv("csv_import_test", path, options));
            Assert.Contains("PostgreSQL server-side COPY FROM", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportCsv_ServerSideFallback_DefaultEncodingStillRuns()
    {
        var path = Path.Combine(Path.GetTempPath(), $"jaunty_pgfb_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "Name,Age\nAlice,30\n");
        try
        {
            using var connection = new NpgsqlConnection();

            long rows = connection.ImportCsv("csv_import_test", path, new CsvImportOptions());
            Assert.Equal(0L, rows);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class NpgsqlConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = "";
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State { get; private set; } = ConnectionState.Closed;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() => State = ConnectionState.Closed;
        public IDbCommand CreateCommand() => new FallbackCommand { Connection = this };
        public void Dispose() { }
        public void Open() => State = ConnectionState.Open;
    }

    private sealed class FallbackCommand : IDbCommand
    {
        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters => throw new NotSupportedException();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => throw new NotSupportedException();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => throw new NotSupportedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
        public object? ExecuteScalar() => null;
        public void Prepare() { }
    }
}
