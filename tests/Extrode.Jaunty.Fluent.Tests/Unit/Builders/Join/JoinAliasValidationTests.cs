using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Builders.Join;

public class JoinAliasValidationTests : IClassFixture<FluentDatabaseFixture>
{
    private const string Hostile = "c; DROP TABLE categories --";

    private readonly FluentDatabaseFixture _fixture;

    public JoinAliasValidationTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("inner")]
    [InlineData("left")]
    [InlineData("right")]
    public void FirstJoin_HostileAlias_Throws(string kind)
    {
        var from = _fixture.Connection.From<Product>("p");

        Assert.Throws<ArgumentException>(() => _ = kind switch
        {
            "inner" => from.InnerJoin<Category>(Hostile),
            "left" => from.LeftJoin<Category>(Hostile),
            _ => from.RightJoin<Category>(Hostile),
        });
    }

    [Fact]
    public void SecondJoin_HostileAlias_Throws()
    {
        var joined = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id");

        Assert.Throws<ArgumentException>(() => joined.InnerJoin<Supplier>(Hostile));
    }

    [Fact]
    public void ThirdJoin_HostileAlias_Throws()
    {
        var joined = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id");

        Assert.Throws<ArgumentException>(() => joined.LeftJoin<Product, Category, Supplier, Order>(Hostile));
    }

    [Fact]
    public void PlainAlias_StillAccepted()
    {
        var sql = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .ToSql();

        Assert.Contains("c", sql);
    }
}
