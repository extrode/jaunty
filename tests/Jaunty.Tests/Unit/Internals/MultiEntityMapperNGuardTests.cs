using System;
using System.Data;

using Jaunty.Internals.Read;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R32. Direct unit tests of <see cref="MultiEntityMapperNGuard.Resolve"/> against a stub
/// resolver, exercising all three guard branches independently of the reflection extension and
/// of <c>MultiEntityMapper&lt;T1,T2,T3&gt;.Build</c>.
/// </summary>
public class MultiEntityMapperNGuardTests
{
    public class GuardT1 { public int A { get; set; } }
    public class GuardT2 { public int B { get; set; } }
    public class GuardT3 { public int C { get; set; } }

    private static SqliteDataReader OpenReader(SqliteConnection connection)
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1 AS A, 2 AS B, 3 AS C";
        SqliteDataReader reader = command.ExecuteReader();
        reader.Read();
        return reader;
    }

    [Fact]
    public void Resolve_ResolverReturnsNull_Throws()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using SqliteDataReader reader = OpenReader(connection);

        Func<Type[], IDataReader, Action<object, IDataRecord>[]> resolver = (_, _) => null!;
        Type[] types = { typeof(GuardT1), typeof(GuardT2), typeof(GuardT3) };

        var exception = Assert.Throws<InvalidOperationException>(
            () => MultiEntityMapperNGuard.Resolve(resolver, types, reader));

        Assert.Contains("ReflectionMultiMapperResolverN", exception.Message, StringComparison.Ordinal);
        Assert.Contains("arity 3", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_ResolverReturnsFewerDelegatesThanTypes_Throws()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using SqliteDataReader reader = OpenReader(connection);

        Func<Type[], IDataReader, Action<object, IDataRecord>[]> resolver = (_, _) => new Action<object, IDataRecord>[]
        {
            (_, _) => { },
            (_, _) => { },
        };
        Type[] types = { typeof(GuardT1), typeof(GuardT2), typeof(GuardT3) };

        var exception = Assert.Throws<InvalidOperationException>(
            () => MultiEntityMapperNGuard.Resolve(resolver, types, reader));

        Assert.Contains("returned 2 delegate(s)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("arity 3", exception.Message, StringComparison.Ordinal);
    }

    // AUD-R35-119: an over-long array used to be accepted and the first N used. The likely cause
    // of one is the same misalignment the guard exists to catch, so it now throws too.
    [Fact]
    public void Resolve_ResolverReturnsMoreDelegatesThanTypes_Throws()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using SqliteDataReader reader = OpenReader(connection);

        Func<Type[], IDataReader, Action<object, IDataRecord>[]> resolver = (_, _) => new Action<object, IDataRecord>[]
        {
            (_, _) => { },
            (_, _) => { },
            (_, _) => { },
            (_, _) => { },
        };
        Type[] types = { typeof(GuardT1), typeof(GuardT2), typeof(GuardT3) };

        var exception = Assert.Throws<InvalidOperationException>(
            () => MultiEntityMapperNGuard.Resolve(resolver, types, reader));

        Assert.Contains("returned 4 delegate(s)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("arity 3", exception.Message, StringComparison.Ordinal);
        Assert.Contains("exactly one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_ResolverReturnsExactlyOnePerType_Succeeds()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using SqliteDataReader reader = OpenReader(connection);

        var expected = new Action<object, IDataRecord>[]
        {
            (_, _) => { },
            (_, _) => { },
            (_, _) => { },
        };
        Func<Type[], IDataReader, Action<object, IDataRecord>[]> resolver = (_, _) => expected;
        Type[] types = { typeof(GuardT1), typeof(GuardT2), typeof(GuardT3) };

        Assert.Same(expected, MultiEntityMapperNGuard.Resolve(resolver, types, reader));
    }

    [Fact]
    public void Resolve_ResolverReturnsNullDelegateAtIndex_Throws()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using SqliteDataReader reader = OpenReader(connection);

        Func<Type[], IDataReader, Action<object, IDataRecord>[]> resolver = (_, _) => new Action<object, IDataRecord>[]
        {
            (_, _) => { },
            null!,
            (_, _) => { },
        };
        Type[] types = { typeof(GuardT1), typeof(GuardT2), typeof(GuardT3) };

        var exception = Assert.Throws<InvalidOperationException>(
            () => MultiEntityMapperNGuard.Resolve(resolver, types, reader));

        Assert.Contains("index 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("GuardT2", exception.Message, StringComparison.Ordinal);
    }
}
