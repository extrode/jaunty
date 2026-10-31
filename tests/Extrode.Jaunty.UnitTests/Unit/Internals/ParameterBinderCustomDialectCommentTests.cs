#if NET
using System.Reflection;

using Extrode.Jaunty.Dialects;
using Extrode.Jaunty.Internals.Parameters;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.Tests.Unit.Internals;

public class ParameterBinderCustomDialectCommentTests
{
    public class ForwardingProxy : DispatchProxy
    {
        public ISqlDialect Inner { get; set; } = new SQLiteDialect();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod!.Invoke(Inner, args);
    }

    private sealed class CustomCommentDialectConnection(string connectionString) : SqliteConnection(connectionString)
    {
    }

    private sealed class Filter
    {
        public int P { get; set; }
    }

    [Fact]
    public void ACustomDialect_EndsABlockCommentAtTheFirstClose()
    {
        SqlDialectFactory.RegisterDialect(nameof(CustomCommentDialectConnection), DispatchProxy.Create<ISqlDialect, ForwardingProxy>());

        const string sql = "SELECT 1 /* a /* b */ WHERE x = @p";
        using var connection = new CustomCommentDialectConnection("Data Source=:memory:");
        using var command = new SqliteCommand(sql, connection);
        ParameterBinder.Bind(command, new Filter { P = 7 });

        Assert.Equal(7, command.Parameters["p"].Value);
    }
}
#endif
