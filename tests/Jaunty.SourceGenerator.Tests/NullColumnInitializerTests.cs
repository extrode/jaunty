using System.Data;
using Microsoft.Data.Sqlite;

using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R33-009. The generated <c>ReadEntity</c> and <c>CreateRowMapper</c> each have two branches -
/// one for <see cref="System.Data.Common.DbDataReader"/>, one for a plain <see cref="IDataReader"/> -
/// and they disagreed on NULL. The typed branch assigned <c>default(T)!</c>; the plain branch left
/// the property untouched. For a property with an initializer that is the difference between
/// <c>""</c> and <c>null</c>, for the same entity and the same row, decided by which reader
/// interface the provider happens to implement. The reflection twin skips, so skipping is what both
/// branches now do.
/// <para>
/// Each test therefore reads the same row twice - once through the SQLite reader, once through a
/// wrapper that implements only <see cref="IDataReader"/> so the other branch is taken - and asserts
/// the two agree. Asserting one branch alone would have passed throughout the defect.
/// </para>
/// </summary>
public class NullColumnInitializerTests
{
    private static SqliteConnection OpenSeeded()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE gen_initialized (Id INTEGER PRIMARY KEY, Name TEXT, Note TEXT);
            INSERT INTO gen_initialized VALUES (1, NULL, NULL);
            """;
        cmd.ExecuteNonQuery();

        return connection;
    }

    private static IDataReader Read(SqliteConnection connection)
    {
        SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Note FROM gen_initialized";
        return cmd.ExecuteReader();
    }

    [Fact]
    public void ReadEntity_NullColumn_PreservesTheInitializerOnBothReaderBranches()
    {
        using SqliteConnection connection = OpenSeeded();

        using (IDataReader typed = Read(connection))
        {
            Assert.True(typed.Read());
            GenInitializedEntity fromDbDataReader = GenInitializedEntity.ReadEntity(typed);
            Assert.Equal("unset", fromDbDataReader.Name);
            Assert.Null(fromDbDataReader.Note);
        }

        using IDataReader plain = new PlainDataReader(Read(connection));
        Assert.True(plain.Read());
        GenInitializedEntity fromPlainReader = GenInitializedEntity.ReadEntity(plain);
        Assert.Equal("unset", fromPlainReader.Name);
        Assert.Null(fromPlainReader.Note);
    }

    [Fact]
    public void CreateRowMapper_NullColumn_PreservesTheInitializerOnBothReaderBranches()
    {
        using SqliteConnection connection = OpenSeeded();

        using (IDataReader typed = Read(connection))
        {
            Assert.True(typed.Read());
            GenInitializedEntity fromDbDataReader = GenInitializedEntity.CreateRowMapper(typed)(typed);
            Assert.Equal("unset", fromDbDataReader.Name);
        }

        using IDataReader plain = new PlainDataReader(Read(connection));
        Assert.True(plain.Read());
        GenInitializedEntity fromPlainReader = GenInitializedEntity.CreateRowMapper(plain)(plain);
        Assert.Equal("unset", fromPlainReader.Name);
    }

    /// <summary>
    /// A non-NULL column must still overwrite the initializer - the control that the fix did not
    /// turn "skip on NULL" into "skip".
    /// </summary>
    [Fact]
    public void ReadEntity_NonNullColumn_StillOverwritesTheInitializer()
    {
        using SqliteConnection connection = OpenSeeded();

        using (SqliteCommand update = connection.CreateCommand())
        {
            update.CommandText = "UPDATE gen_initialized SET Name = 'from the database'";
            update.ExecuteNonQuery();
        }

        using (IDataReader typed = Read(connection))
        {
            Assert.True(typed.Read());
            Assert.Equal("from the database", GenInitializedEntity.ReadEntity(typed).Name);
        }

        using IDataReader plain = new PlainDataReader(Read(connection));
        Assert.True(plain.Read());
        Assert.Equal("from the database", GenInitializedEntity.ReadEntity(plain).Name);
    }

    /// <summary>
    /// Forwards to a real reader while implementing only <see cref="IDataReader"/>, so the generated
    /// <c>is DbDataReader</c> test fails and the fallback branch runs.
    /// </summary>
    private sealed class PlainDataReader(IDataReader inner) : IDataReader
    {
        public int Depth => inner.Depth;
        public bool IsClosed => inner.IsClosed;
        public int RecordsAffected => inner.RecordsAffected;
        public int FieldCount => inner.FieldCount;
        public object this[int i] => inner[i];
        public object this[string name] => inner[name];

        public void Close() => inner.Close();
        public void Dispose() => inner.Dispose();
        public DataTable? GetSchemaTable() => inner.GetSchemaTable();
        public bool NextResult() => inner.NextResult();
        public bool Read() => inner.Read();
        public bool GetBoolean(int i) => inner.GetBoolean(i);
        public byte GetByte(int i) => inner.GetByte(i);
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => inner.GetBytes(i, fieldOffset, buffer, bufferoffset, length);
        public char GetChar(int i) => inner.GetChar(i);
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => inner.GetChars(i, fieldoffset, buffer, bufferoffset, length);
        public IDataReader GetData(int i) => inner.GetData(i);
        public string GetDataTypeName(int i) => inner.GetDataTypeName(i);
        public DateTime GetDateTime(int i) => inner.GetDateTime(i);
        public decimal GetDecimal(int i) => inner.GetDecimal(i);
        public double GetDouble(int i) => inner.GetDouble(i);
        public Type GetFieldType(int i) => inner.GetFieldType(i);
        public float GetFloat(int i) => inner.GetFloat(i);
        public Guid GetGuid(int i) => inner.GetGuid(i);
        public short GetInt16(int i) => inner.GetInt16(i);
        public int GetInt32(int i) => inner.GetInt32(i);
        public long GetInt64(int i) => inner.GetInt64(i);
        public string GetName(int i) => inner.GetName(i);
        public int GetOrdinal(string name) => inner.GetOrdinal(name);
        public string GetString(int i) => inner.GetString(i);
        public object GetValue(int i) => inner.GetValue(i);
        public int GetValues(object[] values) => inner.GetValues(values);
        public bool IsDBNull(int i) => inner.IsDBNull(i);
    }
}
