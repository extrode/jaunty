using System.Linq.Expressions;
using System.Reflection;

using Extrode.Jaunty.Fluent.Expressions;
using Extrode.Jaunty.Fluent.Internals;
using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;
using Extrode.Jaunty.Internals.Entity;

using Xunit;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Expressions;

public class JoinedGroupByHavingGuardTests
{
    [Fact]
    public void BindingAHavingOperandOutsideTranslation_NamesTheRequiredContext()
    {
        var dialect = new TestDialect();
        CachedDialectMetadata[] cached = { FluentMetadataCache.GetForDialect<Product>(dialect), FluentMetadataCache.GetForDialect<Category>(dialect) };
        Expression<Func<Product, Category, object>> keySelector = (p, c) => p.CategoryId;
        var visitor = new JoinedGroupByExpressionVisitor(dialect, cached, new[] { "p", "c" }, keySelector);
        MethodInfo add = typeof(JoinedGroupByExpressionVisitor).GetMethod("AddHavingParameter", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var ex = Assert.Throws<TargetInvocationException>(() => add.Invoke(visitor, new object?[] { 1, null }));

        var inner = Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Equal("HAVING operands can only be bound while TranslateHavingPredicate is running.", inner.Message);
    }
}
