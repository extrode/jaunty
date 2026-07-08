namespace Conduit.Infrastructure;

/// <summary>
/// Hand-written idempotent SQLite schema, replacing EF Core's <c>Database.EnsureCreated()</c>
/// (model-driven schema generation). Jaunty has no migrations/schema-from-entity tooling, so the
/// schema is authored directly. Column names match the domain entities' C# property names 1:1
/// since none of them carry a <c>[Column]</c> override.
/// </summary>
public static class ConduitSchema
{
    public const string CreateTablesSql = """
        CREATE TABLE IF NOT EXISTS Persons (
            PersonId INTEGER PRIMARY KEY AUTOINCREMENT,
            Username TEXT,
            Email TEXT,
            Bio TEXT,
            Image TEXT,
            Hash BLOB NOT NULL,
            Salt BLOB NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Articles (
            ArticleId INTEGER PRIMARY KEY AUTOINCREMENT,
            Slug TEXT,
            Title TEXT,
            Description TEXT,
            Body TEXT,
            AuthorId INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            FOREIGN KEY (AuthorId) REFERENCES Persons (PersonId)
        );

        CREATE TABLE IF NOT EXISTS Comments (
            CommentId INTEGER PRIMARY KEY AUTOINCREMENT,
            Body TEXT,
            AuthorId INTEGER NOT NULL,
            ArticleId INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            FOREIGN KEY (AuthorId) REFERENCES Persons (PersonId),
            FOREIGN KEY (ArticleId) REFERENCES Articles (ArticleId)
        );

        CREATE TABLE IF NOT EXISTS Tags (
            TagId TEXT PRIMARY KEY
        );

        CREATE TABLE IF NOT EXISTS ArticleTags (
            ArticleId INTEGER NOT NULL,
            TagId TEXT NOT NULL,
            PRIMARY KEY (ArticleId, TagId),
            FOREIGN KEY (ArticleId) REFERENCES Articles (ArticleId),
            FOREIGN KEY (TagId) REFERENCES Tags (TagId)
        );

        CREATE TABLE IF NOT EXISTS ArticleFavorites (
            ArticleId INTEGER NOT NULL,
            PersonId INTEGER NOT NULL,
            PRIMARY KEY (ArticleId, PersonId),
            FOREIGN KEY (ArticleId) REFERENCES Articles (ArticleId),
            FOREIGN KEY (PersonId) REFERENCES Persons (PersonId)
        );

        CREATE TABLE IF NOT EXISTS FollowedPeople (
            ObserverId INTEGER NOT NULL,
            TargetId INTEGER NOT NULL,
            PRIMARY KEY (ObserverId, TargetId),
            FOREIGN KEY (ObserverId) REFERENCES Persons (PersonId),
            FOREIGN KEY (TargetId) REFERENCES Persons (PersonId)
        );
        """;
}
