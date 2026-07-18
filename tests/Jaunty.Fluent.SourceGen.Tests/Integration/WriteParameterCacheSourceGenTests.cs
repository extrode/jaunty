using System.Data;

using Jaunty.Fluent.SourceGen.Tests.Entities;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Write;

using Microsoft.Data.Sqlite;

namespace Jaunty.Fluent.SourceGen.Tests.Integration;

/// <summary>
/// Regression test: WriteParameterCache&lt;T&gt; (whose InsertValueSetter/UpdateValueSetter/
/// DeleteValueSetter back the bulk write operations - BulkInsert/BulkUpdate/BulkDelete) must
/// resolve entity metadata via the source-generated static surface first, exactly like
/// CrudSqlCache.TryResolveMetadata does, instead of relying solely on
/// JauntyConfig.ReflectionTableMetadataResolver. This project has no reference to
/// Jaunty.Extensions.Reflection at all (see the .csproj), so ReflectionTableMetadataResolver
/// is guaranteed null for the lifetime of this process - a non-null value setter here proves
/// the source-gen tier resolves standalone (the exact NativeAOT scenario this fix targets:
/// before the fix, the bulk value-setters silently no-op'd in a source-gen-only build).
/// </summary>
public sealed class WriteParameterCacheSourceGenTests
{
    [Fact]
    public void InsertValueSetter_SourceGeneratedEntity_ResolvesWithoutReflectionResolver()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        Action<IDataParameterCollection, CrudSourceGenWidget>? setter = WriteParameterCache<CrudSourceGenWidget>.InsertValueSetter;
        Assert.NotNull(setter);

        CachedCrudSql cached = CrudSqlCache.GetSql<CrudSourceGenWidget>(connection);
        IReadOnlyList<ColumnMetadata> insertColumns = cached.Metadata.InsertColumns;
        Assert.NotEmpty(insertColumns);

        using IDbCommand command = connection.CreateCommand();
        foreach (ColumnMetadata column in insertColumns)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = "@" + column.ColumnName;
            command.Parameters.Add(parameter);
        }

        var widget = new CrudSourceGenWidget { WidgetId = 7, Name = "Gadget", Price = 9.99m };
        setter!(command.Parameters, widget);

        Assert.Equal(insertColumns.Count, command.Parameters.Count);
        for (int i = 0; i < insertColumns.Count; i++)
        {
            object? actual = ((IDbDataParameter)command.Parameters[i]!).Value;
            object expected = insertColumns[i].PropertyName switch
            {
                nameof(CrudSourceGenWidget.WidgetId) => widget.WidgetId,
                nameof(CrudSourceGenWidget.Name) => widget.Name,
                nameof(CrudSourceGenWidget.Price) => widget.Price,
                _ => throw new InvalidOperationException($"Unexpected insert column '{insertColumns[i].PropertyName}'.")
            };
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void UpdateValueSetter_SourceGeneratedEntity_ResolvesWithoutReflectionResolver()
    {
        Action<IDataParameterCollection, CrudSourceGenWidget>? setter = WriteParameterCache<CrudSourceGenWidget>.UpdateValueSetter;
        Assert.NotNull(setter);
    }

    [Fact]
    public void DeleteValueSetter_SourceGeneratedEntity_ResolvesWithoutReflectionResolver()
    {
        Action<IDataParameterCollection, CrudSourceGenWidget>? setter = WriteParameterCache<CrudSourceGenWidget>.DeleteValueSetter;
        Assert.NotNull(setter);
    }
}
