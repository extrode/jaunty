using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;

using Jaunty.Fluent;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Builders;

/// <summary>
/// AUD-R19 batch-5: <c>GroupedQueryBuilder.BindParameters(IDbCommand)</c> previously bound via
/// <c>_parameters.ToParameterObject()</c>, which strips the dialect's parameter prefix (e.g. "@")
/// for consumption by Jaunty's core reflection-based binder - but this method binds directly to a
/// raw <see cref="IDbCommand"/>, whose <see cref="IDbDataParameter.ParameterName"/> must match the
/// prefixed placeholder already embedded in the generated SQL text. These tests invoke the private
/// BindParameters method via reflection (the class is internal but Jaunty.Fluent.Tests has
/// InternalsVisibleTo access) and assert the bound parameter name exactly matches the prefixed
/// placeholder in the generated HAVING clause - this fails pre-fix (bound name has no "@") and
/// passes post-fix.
/// </summary>
public class GroupedQueryBuilderParameterBindingTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public GroupedQueryBuilderParameterBindingTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void BindParameters_HavingWithCapturedVariable_UsesPrefixedParameterName()
    {
        int minCount = 3;

        IGroupedQuery<Product, short?> query = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Count() > minCount);

        string sql = query.ToSql(g => new { CategoryId = g.Key, Count = g.Count() });
        Match match = Regex.Match(sql, "@hp\\w+");
        Assert.True(match.Success, $"Expected a prefixed '@hp...' placeholder in generated SQL: {sql}");
        string expectedParamName = match.Value;

        using var command = _fixture.Connection.CreateCommand();
        MethodInfo bindParameters = query.GetType().GetMethod(
            "BindParameters",
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            [typeof(IDbCommand)],
            null)!;
        bindParameters.Invoke(query, [command]);

        Assert.True(command.Parameters.Count > 0, "Expected at least one bound parameter.");
        var bound = (IDbDataParameter)command.Parameters[0]!;
        Assert.Equal(expectedParamName, bound.ParameterName);
    }
}
