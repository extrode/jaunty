using System.Data;

using Xunit;

namespace Jaunty.Tests.Unit.Import;

/// <summary>
/// R27 batch 03 (medium). The MySQL LOAD DATA path hardcoded <c>LINES TERMINATED BY '\n'</c>,
/// so importing a CRLF file - the default output of Excel and most Windows tooling - left a
/// trailing <c>\r</c> on the last field of every row, silently. The terminator is now sniffed
/// from the file, the same way SQL Server's ROWTERMINATOR already was. Observed through the
/// generated statement via a stub connection named exactly MySqlConnection (dialect resolution
/// keys on Type.Name).
/// </summary>
public class CsvImportMySqlLineTerminatorTests
{
    private static string WriteTempCsv(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"jaunty_mysql_csv_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void CrlfFileGetsCrlfLineTerminator()
    {
        string path = WriteTempCsv("Name,Age\r\nAda,36\r\n");
        try
        {
            var connection = new MySqlConnection();
            connection.ImportCsv("t", path);

            Assert.Contains("LINES TERMINATED BY '\\r\\n'", connection.LastCommandText, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LfFileGetsLfLineTerminator()
    {
        string path = WriteTempCsv("Name,Age\nAda,36\n");
        try
        {
            var connection = new MySqlConnection();
            connection.ImportCsv("t", path);

            Assert.Contains("LINES TERMINATED BY '\\n'", connection.LastCommandText, StringComparison.Ordinal);
            Assert.DoesNotContain("\\r\\n", connection.LastCommandText, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class MySqlConnection : IDbConnection
    {
        public string LastCommandText { get; private set; } = string.Empty;

        public string ConnectionString { get; set; } = "";
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State { get; private set; } = ConnectionState.Closed;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() => State = ConnectionState.Closed;
        public void Dispose() { }
        public void Open() => State = ConnectionState.Open;

        public IDbCommand CreateCommand() => new StubCommand(this, text => LastCommandText = text);

        private sealed class StubCommand : IDbCommand
        {
            private readonly Action<string> _record;

            public StubCommand(IDbConnection connection, Action<string> record)
            {
                Connection = connection;
                _record = record;
            }

            public string CommandText { get; set; } = "";
            public int CommandTimeout { get; set; }
            public CommandType CommandType { get; set; }
            public IDbConnection? Connection { get; set; }
            public IDataParameterCollection Parameters { get; } = new StubParameterCollection();
            public IDbTransaction? Transaction { get; set; }
            public UpdateRowSource UpdatedRowSource { get; set; }

            public void Cancel() { }
            public IDbDataParameter CreateParameter() => throw new NotSupportedException();
            public void Dispose() { }

            public int ExecuteNonQuery()
            {
                _record(CommandText);
                return 0;
            }

            public IDataReader ExecuteReader() => throw new NotSupportedException();
            public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
            public object ExecuteScalar() => throw new NotSupportedException();
            public void Prepare() { }
        }

        private sealed class StubParameterCollection : System.Collections.ArrayList, IDataParameterCollection
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
}
