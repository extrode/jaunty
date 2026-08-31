using Jaunty.Attributes;

namespace Jaunty.Fluent.Tests.Entities;

/// <summary>
/// Test-only entity with a column name ("order") that collides with a SQL reserved keyword.
/// Used to regression-test the CachedDialectMetadata double-escaping fix: escaping an
/// already-escaped keyword column name a second time made SqlIdentifierValidator reject the
/// bracketed/quoted text, throwing ArgumentException. Not registered in the test database -
/// only used for SQL-generation (ToSql) assertions, never executed.
/// </summary>
[Table("keyword_column_entities")]
public class KeywordColumnEntity
{
    [Column("id")]
    public int Id { get; set; }

    [Column("order")]
    public int Order { get; set; }
}
