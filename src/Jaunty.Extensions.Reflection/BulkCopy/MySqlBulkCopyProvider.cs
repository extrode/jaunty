using System;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Jaunty.Configuration;

namespace Jaunty.Extensions.Reflection.BulkCopy;

/// <summary>
/// MySQL/MariaDB bulk copy provider using chunked multi-row parameterized INSERT.
/// </summary>
/// <remarks>
/// <para>
/// Rewritten 2026-07-04 (PROD-120). The previous implementation wrote a temp CSV and
/// drove <c>MySqlBulkLoader</c> (<c>LOAD DATA LOCAL INFILE</c>) via reflection, which
/// fails on any MySQL 8+/MariaDB server with the default <c>local_infile=0</c> and
/// additionally requires <c>AllowLoadLocalInfile=true</c> in the connection string.
/// </para>
/// <para>
/// Chunked multi-row INSERT needs no server or connection-string configuration, works
/// with both MySqlConnector and MySql.Data (plain ADO.NET), and performs in the same
/// class as LOAD DATA for the 100-10k row range this path targets.
/// </para>
/// </remarks>
internal sealed class MySqlBulkCopyProvider : IBulkCopyProvider
{
    // Stay well under MySQL's practical placeholder limits and default
    // max_allowed_packet regardless of column count.
    private const int MaxParametersPerStatement = 2000;

    /// <inheritdoc/>
    public bool IsSupported => true;

    /// <inheritdoc/>
    public int CopyToServer(IDbConnection connection, string tableName, IDataReader data, BulkCopyOptions options)
    {
        bool wasClosed = connection.State == ConnectionState.Closed;
        IDbTransaction? transaction = options.Transaction;
        bool ownTransaction = transaction is null;

        try
        {
            if (wasClosed)
                connection.Open();

            if (ownTransaction)
                transaction = connection.BeginTransaction();

            int columnCount = data.FieldCount;
            var columnNames = new string[columnCount];
            for (int i = 0; i < columnCount; i++)
                columnNames[i] = data.GetName(i);

            int rowsPerChunk = Math.Max(1, MaxParametersPerStatement / Math.Max(1, columnCount));

            int total = 0;
            var buffer = new object?[rowsPerChunk][];
            int buffered = 0;

            // Reused command for full chunks; the partial final chunk gets its own.
            IDbCommand? fullChunkCommand = null;
            try
            {
                while (data.Read())
                {
                    var row = new object?[columnCount];
                    for (int i = 0; i < columnCount; i++)
                        row[i] = data.GetValue(i);
                    buffer[buffered++] = row;

                    if (buffered == rowsPerChunk)
                    {
                        fullChunkCommand ??= BuildChunkCommand(connection, transaction, tableName, columnNames, rowsPerChunk, options);
                        BindChunk(fullChunkCommand, buffer, buffered, columnCount);
                        total += ExecuteAffectedRows(fullChunkCommand, buffered);
                        buffered = 0;
                    }
                }

                if (buffered > 0)
                {
                    using IDbCommand tail = BuildChunkCommand(connection, transaction, tableName, columnNames, buffered, options);
                    BindChunk(tail, buffer, buffered, columnCount);
                    total += ExecuteAffectedRows(tail, buffered);
                }
            }
            finally
            {
                fullChunkCommand?.Dispose();
            }

            if (ownTransaction)
                transaction!.Commit();

            return total;
        }
        catch
        {
            if (ownTransaction)
                transaction?.Rollback();
            throw;
        }
        finally
        {
            if (ownTransaction)
                transaction?.Dispose();
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> CopyToServerAsync(DbConnection connection, string tableName, IDataReader data, BulkCopyOptions options, CancellationToken cancellationToken)
    {
        bool wasClosed = connection.State == ConnectionState.Closed;
        DbTransaction? transaction = options.Transaction as DbTransaction;
        bool ownTransaction = options.Transaction is null;

        try
        {
            if (wasClosed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (ownTransaction)
                transaction = connection.BeginTransaction();

            int columnCount = data.FieldCount;
            var columnNames = new string[columnCount];
            for (int i = 0; i < columnCount; i++)
                columnNames[i] = data.GetName(i);

            int rowsPerChunk = Math.Max(1, MaxParametersPerStatement / Math.Max(1, columnCount));

            int total = 0;
            var buffer = new object?[rowsPerChunk][];
            int buffered = 0;

            DbCommand? fullChunkCommand = null;
            try
            {
                while (data.Read())
                {
                    var row = new object?[columnCount];
                    for (int i = 0; i < columnCount; i++)
                        row[i] = data.GetValue(i);
                    buffer[buffered++] = row;

                    if (buffered == rowsPerChunk)
                    {
                        fullChunkCommand ??= (DbCommand)BuildChunkCommand(connection, transaction, tableName, columnNames, rowsPerChunk, options);
                        BindChunk(fullChunkCommand, buffer, buffered, columnCount);
                        int affected = await fullChunkCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        total += affected < 0 ? buffered : affected;
                        buffered = 0;
                    }
                }

                if (buffered > 0)
                {
                    using var tail = (DbCommand)BuildChunkCommand(connection, transaction, tableName, columnNames, buffered, options);
                    BindChunk(tail, buffer, buffered, columnCount);
                    int affected = await tail.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    total += affected < 0 ? buffered : affected;
                }
            }
            finally
            {
                fullChunkCommand?.Dispose();
            }

            if (ownTransaction)
                transaction!.Commit();

            return total;
        }
        catch
        {
            if (ownTransaction)
                transaction?.Rollback();
            throw;
        }
        finally
        {
            if (ownTransaction)
                transaction?.Dispose();
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static IDbCommand BuildChunkCommand(IDbConnection connection, IDbTransaction? transaction, string tableName, string[] columnNames, int rows, BulkCopyOptions options)
    {
        IDbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        if (options.Timeout > 0)
            command.CommandTimeout = options.Timeout;

        var sb = new StringBuilder(64 + rows * columnNames.Length * 8);
        sb.Append("INSERT INTO ").Append(EscapeIdentifier(tableName)).Append(" (");
        for (int c = 0; c < columnNames.Length; c++)
        {
            if (c > 0) sb.Append(", ");
            sb.Append(EscapeIdentifier(columnNames[c]));
        }
        sb.Append(") VALUES ");

        for (int r = 0; r < rows; r++)
        {
            sb.Append(r > 0 ? ",(" : "(");
            for (int c = 0; c < columnNames.Length; c++)
            {
                if (c > 0) sb.Append(',');
                sb.Append("@p").Append(r).Append('_').Append(c);

                IDbDataParameter param = command.CreateParameter();
                param.ParameterName = $"@p{r}_{c}";
                command.Parameters.Add(param);
            }
            sb.Append(')');
        }

        command.CommandText = sb.ToString();
        return command;
    }

    private static void BindChunk(IDbCommand command, object?[][] buffer, int rows, int columnCount)
    {
        IDataParameterCollection parameters = command.Parameters;
        int index = 0;
        for (int r = 0; r < rows; r++)
        {
            object?[] row = buffer[r];
            for (int c = 0; c < columnCount; c++)
            {
                ((IDbDataParameter)parameters[index++]!).Value = row[c] ?? DBNull.Value;
            }
        }
    }

    private static int ExecuteAffectedRows(IDbCommand command, int expectedRows)
    {
        int affected = command.ExecuteNonQuery();
        // Some providers report -1 for multi-row statements; trust the row count we sent.
        return affected < 0 ? expectedRows : affected;
    }

    private static string EscapeIdentifier(string identifier)
        => "`" + identifier.Replace("`", "``") + "`";
}
