using System.Collections.Immutable;
using System.Reflection;

using Jaunty.Core;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Jaunty.Fluent.Tests.Unit;

public class PagedWriteTerminalTests
{
    private const string Preamble = """
        using System;
        using System.Linq.Expressions;
        using Jaunty.Core;
        using Jaunty.Fluent;

        public class Probe
        {
            public class Row { public int Id { get; set; } }

            public static void Body(IFromClause<Row> q)
            {
        """;

    private const string Postscript = """
            }
        }
        """;

    private static ImmutableArray<Diagnostic> Compile(string body)
    {
        string source = Preamble + Environment.NewLine + body + Environment.NewLine + Postscript;

        var compilation = CSharpCompilation.Create(
            "PagedWriteTerminalProbe",
            [CSharpSyntaxTree.ParseText(source)],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        return [.. compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)];
    }

    private static IEnumerable<MetadataReference> References()
    {
        _ = typeof(IPagedClause<>);
        _ = typeof(CommandOptions);

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location)
            .Distinct()
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
    }

    private static void AssertObsoleteError(string body, string member)
    {
        ImmutableArray<Diagnostic> errors = Compile(body);

        Diagnostic error = Assert.Single(errors, d => d.Id == "CS0619");
        Assert.Contains(member, error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("not carried into", error.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("q.Take(5).DeleteAll();", "DeleteAll")]
    [InlineData("q.Skip(5).DeleteAll();", "DeleteAll")]
    [InlineData("q.Take(5).DeleteAll(CommandOptions.WithTimeout(1));", "DeleteAll")]
    [InlineData("_ = q.Take(5).DeleteAllAsync();", "DeleteAllAsync")]
    [InlineData("_ = q.Take(5).DeleteAllAsync(CommandOptions.WithTimeout(1));", "DeleteAllAsync")]
    [InlineData("q.Take(5).Set(r => r.Id, 1);", "Set")]
    [InlineData("q.Take(5).Set(\"Id\", 1);", "Set")]
    [InlineData("q.Take(5).Set(new { Id = 1 });", "Set")]
    public void APagedQueryCannotReachAWriteTerminal(string body, string member)
        => AssertObsoleteError(body, member);

    [Theory]
    [InlineData("q.Take(5).Where(r => r.Id > 0).Delete();", "Delete")]
    [InlineData("q.Where(r => r.Id > 0).Take(5).Delete();", "Delete")]
    [InlineData("q.Where(r => r.Id > 0).Skip(5).Delete();", "Delete")]
    [InlineData("q.Take(5).Where(\"Id\", 1).Delete(CommandOptions.WithTimeout(1));", "Delete")]
    [InlineData("_ = q.Take(5).Where(r => r.Id > 0).DeleteAsync();", "DeleteAsync")]
    [InlineData("_ = q.Take(5).Where(r => r.Id > 0).DeleteAsync(CommandOptions.WithTimeout(1));", "DeleteAsync")]
    public void PagingStaysStickyThroughTheWhereFamily(string body, string member)
        => AssertObsoleteError(body, member);

    [Theory]
    [InlineData("q.Take(5).WhereIn(r => r.Id, new[] { 1 }).Delete();")]
    [InlineData("q.Take(5).WhereNotIn(r => r.Id, new[] { 1 }).Delete();")]
    [InlineData("q.Take(5).WhereBetween(r => r.Id, 1, 2).Delete();")]
    [InlineData("q.Take(5).WhereNotBetween(r => r.Id, 1, 2).Delete();")]
    [InlineData("q.Take(5).WhereRaw(\"1 = 1\").Delete();")]
    [InlineData("q.Take(5).Where(r => r.Id > 0).And(r => r.Id < 9).Delete();")]
    [InlineData("q.Take(5).Where(r => r.Id > 0).Or(r => r.Id < 9).Delete();")]
    [InlineData("q.Take(5).Where(r => r.Id > 0).AndIn(r => r.Id, new[] { 1 }).Delete();")]
    [InlineData("q.Take(5).Where(r => r.Id > 0).OrBetween(r => r.Id, 1, 2).Delete();")]
    [InlineData("q.Take(5).Where(r => r.Id > 0).AndRaw(\"1 = 1\").Delete();")]
    public void EveryWhereAndConditionEntryPointCarriesThePaging(string body)
        => AssertObsoleteError(body, "Delete");

    [Theory]
    [InlineData("q.DeleteAll();")]
    [InlineData("_ = q.DeleteAllAsync();")]
    [InlineData("q.Set(r => r.Id, 1);")]
    [InlineData("q.Where(r => r.Id > 0).Delete();")]
    [InlineData("_ = q.Where(r => r.Id > 0).DeleteAsync();")]
    public void AnUnpagedQueryStillReachesTheWriteTerminals(string body)
        => Assert.Empty(Compile(body));

    [Theory]
    [InlineData("_ = q.Take(5).Select();")]
    [InlineData("_ = q.Take(5).Skip(2).Select();")]
    [InlineData("_ = q.Take(5).OrderBy(r => r.Id).Select();")]
    [InlineData("_ = q.Take(5).Distinct().Select();")]
    [InlineData("_ = q.Take(5).Where(r => r.Id > 0).Select();")]
    [InlineData("_ = q.Take(5).Where(r => r.Id > 0).OrderBy(r => r.Id).Select();")]
    [InlineData("_ = q.Take(5).ToSql();")]
    [InlineData("_ = q.Take(5).Where(r => r.Id > 0).ToSql();")]
    public void TheReadSurfaceIsUnchangedAfterPaging(string body)
        => Assert.Empty(Compile(body));

    [Fact]
    public void TakeAndSkipReturnThePagedInterfaces()
    {
        Assert.Equal(typeof(IPagedClause<>), typeof(IFromClause<>).GetMethod("Take")!.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(IPagedClause<>), typeof(IFromClause<>).GetMethod("Skip")!.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(IPagedWhereClause<>), typeof(IWhereClause<>).GetMethod("Take")!.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(IPagedWhereClause<>), typeof(IWhereClause<>).GetMethod("Skip")!.ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void EveryHiddenWriteTerminalIsAnErrorNotAWarning()
    {
        MethodInfo[] hidden =
        [
            .. typeof(IPagedClause<>).GetMethods().Where(m => m.GetCustomAttribute<ObsoleteAttribute>() is not null),
            .. typeof(IPagedWhereClause<>).GetMethods().Where(m => m.GetCustomAttribute<ObsoleteAttribute>() is not null),
        ];

        Assert.Equal(11, hidden.Length);
        Assert.All(hidden, m => Assert.True(m.GetCustomAttribute<ObsoleteAttribute>()!.IsError, m.Name));
    }
}
