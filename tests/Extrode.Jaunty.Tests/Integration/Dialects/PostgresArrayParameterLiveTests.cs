using Extrode.Jaunty.Tests.Helpers.Dialects;

namespace Extrode.Jaunty.Tests.Integration.Dialects;

/// <summary>
/// Round 38 follow-up against a live server: the parameter walkers read <c>[</c> as a quoted
/// identifier on every engine, so a placeholder inside a PostgreSQL array was never bound.
/// </summary>
public class PostgresArrayParameterLiveTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public PostgresArrayParameterLiveTests(DialectFixture fixture) => _fixture = fixture;

    [Theory]
    [Postgres]
    public void Postgres_PlaceholdersInsideAnArrayConstructor_AreBound(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        Assert.Equal(12, connection.QueryScalar<int>("SELECT (ARRAY[@A, @B])[2] + @C", new { A = 1, B = 5, C = 7 }));
    }

    [Theory]
    [Postgres]
    public void Postgres_APlaceholderAsAnArraySubscript_IsBound(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        Assert.Equal(30, connection.QueryScalar<int>("SELECT (ARRAY[10, 20, 30])[@I]", new { I = 3 }));
    }
}
