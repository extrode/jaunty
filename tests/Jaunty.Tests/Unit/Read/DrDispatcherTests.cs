using System.Data;
using System.Data.Common;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Internals.Read;

using Microsoft.Data.Sqlite;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// <c>Func&lt;in T, out TResult&gt;</c> is contravariant in its argument, so a caller's
/// <c>Func&lt;IDataReader, T&gt;</c> is already a <c>Func&lt;DbDataReader, T&gt;</c>. The
/// <see cref="DbDataReader"/> overload of <c>DrDispatcher.Resolve</c> used to wrap it in a lambda
/// anyway: a closure per query and a second delegate call per row, which was the whole of the
/// pipeline overhead left on the custom-mapper path (measured 2026-09-02 at 1.09x of hand-coded
/// ADO.NET on SQLite, 10k rows).
/// </summary>
public sealed class DrDispatcherTests
{
    private sealed class Row
    {
        public int Id { get; set; }
    }

    [Fact]
    public void ACustomMapper_IsHandedBackUnwrapped_ForADbDataReader()
    {
        Func<IDataReader, Row> mapper = static r => new Row { Id = r.GetInt32(0) };
        CommandOptions<Row> options = CommandOptions<Row>.WithMapper(mapper);
        using DbDataReader reader = OpenReader();

        Func<DbDataReader, Row> resolved = DrDispatcher.Resolve(reader, options, MappingMode.Strict);

        Assert.Same(mapper, resolved);
    }

    [Fact]
    public void ACustomMapper_IsHandedBackUnwrapped_ForAnIDataReader()
    {
        Func<IDataReader, Row> mapper = static r => new Row { Id = r.GetInt32(0) };
        CommandOptions<Row> options = CommandOptions<Row>.WithMapper(mapper);
        using DbDataReader reader = OpenReader();

        Func<IDataReader, Row> resolved = DrDispatcher.Resolve((IDataReader)reader, options, MappingMode.Strict);

        Assert.Same(mapper, resolved);
    }

    [Fact]
    public void TheUnwrappedMapper_StillMapsTheRow()
    {
        CommandOptions<Row> options = CommandOptions<Row>.WithMapper(static r => new Row { Id = r.GetInt32(0) });
        using DbDataReader reader = OpenReader();
        Func<DbDataReader, Row> resolved = DrDispatcher.Resolve(reader, options, MappingMode.Strict);

        Assert.True(reader.Read());
        Row row = resolved(reader);

        Assert.Equal(42, row.Id);
    }

    private static DbDataReader OpenReader()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT 42";
        return command.ExecuteReader(CommandBehavior.CloseConnection);
    }
}
