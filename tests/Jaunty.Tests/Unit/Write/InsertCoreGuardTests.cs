using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Write;

/// <summary>
/// AUD-R35-134. <c>InsertCore</c> and <c>InsertCoreAsync</c> each open with two eager guards - no
/// parameter binder, and no insertable columns - and all four throw sites were untested. They are
/// the first thing a caller meets when mapping is not configured or an entity is nothing but a
/// generated key, so the message is the whole diagnostic.
/// </summary>
[Collection("Type Handler Operations")]
public class InsertCoreGuardTests : IDisposable
{
    public InsertCoreGuardTests() => JauntyReflectionExtensions.UseReflectionMapping();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyReflectionExtensions.UseReflectionMapping();
    }

    [Table("identity_only_rows")]
    public class IdentityOnlyRow
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
    }

    [Table("unbindable_rows")]
    public class UnbindableRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    private static SqliteConnection Open(string schema)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText = schema;
        seed.ExecuteNonQuery();

        return connection;
    }

    [Fact]
    public void AnEntityWhoseOnlyColumnIsGenerated_SaysSo()
    {
        using SqliteConnection connection = Open("CREATE TABLE identity_only_rows (id INTEGER PRIMARY KEY);");

        var ex = Assert.Throws<InvalidOperationException>(() => connection.Insert(new IdentityOnlyRow()));

        Assert.Equal(
            "Cannot insert entity of type 'IdentityOnlyRow': No insertable columns found.",
            ex.Message);
    }

    [Fact]
    public async Task AnEntityWhoseOnlyColumnIsGeneratedAsync_SaysSo()
    {
        using SqliteConnection connection = Open("CREATE TABLE identity_only_rows (id INTEGER PRIMARY KEY);");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await connection.InsertAsync(new IdentityOnlyRow(), default, TestContext.Current.CancellationToken));

        Assert.Equal(
            "Cannot insert entity of type 'IdentityOnlyRow': No insertable columns found.",
            ex.Message);
    }

    /// <summary>
    /// With every binder resolver cleared, the first touch of <c>WriteParameterCache&lt;T&gt;</c>
    /// for this entity type finds no binder. The type is used by no other test, so the poisoning
    /// AUD-R26 documented cannot leak sideways - and the generation counter retires the empty
    /// bindings on the next configuration change regardless.
    /// </summary>
    [Fact]
    public void WithNoMappingConfigured_TheBinderGuardNamesTheType()
    {
        using SqliteConnection connection = Open("CREATE TABLE unbindable_rows (id INTEGER PRIMARY KEY, name TEXT);");

        // See the async twin: the static constructor must have run before Reset(), or it undoes it.
        connection.Insert(new UnbindableRow { Id = 1, Name = "warm" });

        JauntyConfig.Reset();

        var ex = Assert.Throws<InvalidOperationException>(
            () => connection.Insert(new UnbindableRow { Id = 2, Name = "a" }));

        Assert.Equal(
            "No parameter binder found for type 'UnbindableRow'. Ensure source generation or reflection extension is used.",
            ex.Message);
    }

    [Fact]
    public async Task WithNoMappingConfiguredAsync_TheBinderGuardNamesTheType()
    {
        using SqliteConnection connection = Open("CREATE TABLE unbindable_rows (id INTEGER PRIMARY KEY, name TEXT);");

        // AUD-R35-099: Jaunty's static constructor calls TryEnableReflectionMapping, and it runs on
        // the first touch of any Jaunty static member - which, without this warm-up, is the very
        // Insert call under test, re-registering the resolvers Reset() had just cleared.
        connection.Insert(new UnbindableRow { Id = 1, Name = "warm" });

        JauntyConfig.Reset();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await connection.InsertAsync(new UnbindableRow { Id = 2, Name = "a" }, default, TestContext.Current.CancellationToken));

        Assert.Equal(
            "No parameter binder found for type 'UnbindableRow'. Ensure source generation or reflection extension is used.",
            ex.Message);
    }
}
