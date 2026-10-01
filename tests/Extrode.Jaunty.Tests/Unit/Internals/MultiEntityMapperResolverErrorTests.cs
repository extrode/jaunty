using System.Data;
using System.Data.SQLite;

using Extrode.Jaunty.Configuration;
using CoreRead = Extrode.Jaunty.Internals.Read;

namespace Extrode.Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R38-134: arity 2 told a custom resolver that answered wrongly to load the extension, and
/// arities 3-7 checked for a resolver before rejecting a struct entity.
/// </summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class MultiEntityMapperResolverErrorTests : IDisposable
{
    private readonly SQLiteConnection _connection = new("Data Source=:memory:");
    private readonly Func<Type, Type, object>? _resolver = JauntyConfig.ReflectionMultiMapperResolver;
    private readonly Func<Type[], IDataReader, Action<object, IDataRecord>[]>? _resolverN = JauntyConfig.ReflectionMultiMapperResolverN;

    public MultiEntityMapperResolverErrorTests() => _connection.Open();

    public void Dispose()
    {
        JauntyConfig.ReflectionMultiMapperResolver = _resolver;
        JauntyConfig.ReflectionMultiMapperResolverN = _resolverN;
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    public class Left { public int A { get; set; } }

    public class Right { public int B { get; set; } }

    public class Other { public int C { get; set; } }

    public struct Point { public int X { get; set; } }

    private IDataReader Reader()
    {
        using IDbCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT 1 AS A, 2 AS B, 3 AS C";
        return command.ExecuteReader();
    }

    [Fact]
    public void Arity2_NoResolver_SaysToLoadTheExtension()
    {
        JauntyConfig.ReflectionMultiMapperResolver = null;
        using IDataReader reader = Reader();

        var ex = Assert.Throws<InvalidOperationException>(() => CoreRead.MultiEntityMapper<Left, Right>.Build(reader));

        Assert.Contains("Extrode.Jaunty.Extensions.Reflection", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Arity2_AResolverReturningNull_IsNamedInsteadOfBlamingTheExtension()
    {
        JauntyConfig.ReflectionMultiMapperResolver = (_, _) => null!;
        using IDataReader reader = Reader();

        var ex = Assert.Throws<InvalidOperationException>(() => CoreRead.MultiEntityMapper<Right, Left>.Build(reader));

        Assert.Contains("ReflectionMultiMapperResolver returned null", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Action<Right, Left, IDataRecord>", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("is loaded", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Arity2_AResolverReturningTheWrongType_NamesWhatItReturned()
    {
        JauntyConfig.ReflectionMultiMapperResolver = (_, _) => "not a delegate";
        using IDataReader reader = Reader();

        var ex = Assert.Throws<InvalidOperationException>(() => CoreRead.MultiEntityMapper<Left, Other>.Build(reader));

        Assert.Contains("returned a 'String'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Action<Left, Other, IDataRecord>", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Arity2_AStructEntityWithNoResolver_IsRejectedAsAStruct()
    {
        JauntyConfig.ReflectionMultiMapperResolver = null;
        using IDataReader reader = Reader();

        Assert.Throws<NotSupportedException>(() => CoreRead.MultiEntityMapper<Point, Left>.Build(reader));
    }

    [Fact]
    public void Arity3_AStructEntityWithNoResolver_IsRejectedAsAStruct_LikeArity2()
    {
        JauntyConfig.ReflectionMultiMapperResolverN = null;
        using IDataReader reader = Reader();

        var ex = Assert.Throws<NotSupportedException>(() => CoreRead.MultiEntityMapper<Point, Left, Right>.Build(reader));

        Assert.Contains("'Point'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ArityFourToSeven_AStructEntityWithNoResolver_IsRejectedAsAStruct()
    {
        JauntyConfig.ReflectionMultiMapperResolverN = null;
        using IDataReader reader = Reader();

        Assert.Throws<NotSupportedException>(() => CoreRead.MultiEntityMapper<Left, Point, Right, Other>.Build(reader));
        Assert.Throws<NotSupportedException>(() => CoreRead.MultiEntityMapper<Left, Right, Point, Other, Left>.Build(reader));
        Assert.Throws<NotSupportedException>(() => CoreRead.MultiEntityMapper<Left, Right, Other, Point, Left, Right>.Build(reader));
        Assert.Throws<NotSupportedException>(() => CoreRead.MultiEntityMapper<Left, Right, Other, Left, Point, Right, Other>.Build(reader));
    }
}
