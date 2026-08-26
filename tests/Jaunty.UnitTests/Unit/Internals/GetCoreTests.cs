using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Regression tests for GetCore.cs: ensures the "no primary key" exception message
/// is correctly interpolated (was previously emitted as the literal text
/// "{typeof(T).Name}" instead of the actual type name).
/// </summary>
public class GetCoreTests : IDisposable
{
    private readonly SQLiteConnection _connection;

    public GetCoreTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    public class NoKeyEntity
    {
        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    // Composite key (2 primary keys) also yields an empty SelectByIdSql, exercising the
    // same "no single primary key" path as NoKeyEntity, but through GetByIdTypedCore.
    public class CompositeKeyEntityTyped : IEntity<int>
    {
        public int Id { get; set; }

        [Key]
        [Column("key2")]
        public int Key2 { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Get_NoPrimaryKey_ThrowsWithInterpolatedTypeName()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _connection.Get<NoKeyEntity>(1));

        Assert.Contains(nameof(NoKeyEntity), ex.Message);
        Assert.DoesNotContain("{typeof(T).Name}", ex.Message);
    }

    [Fact]
    public void GetTyped_CompositePrimaryKey_ThrowsWithInterpolatedTypeName()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _connection.Get<CompositeKeyEntityTyped, int>(1));

        Assert.Contains(nameof(CompositeKeyEntityTyped), ex.Message);
        Assert.DoesNotContain("{typeof(T).Name}", ex.Message);
    }
}
