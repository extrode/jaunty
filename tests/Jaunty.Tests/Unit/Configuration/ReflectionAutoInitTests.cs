using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;
using Jaunty.Internals.Entity;

namespace Jaunty.Tests.Unit.Configuration;

/// <summary>
/// AUD-R35-099. <c>Jaunty</c>'s static constructor calls <c>TryEnableReflectionMapping</c>, which
/// invokes <c>UseReflectionMapping()</c> reflectively. That constructor does not run at startup: it
/// runs on the first touch of any <c>Jaunty</c> static member, so a caller who configures
/// <see cref="JauntyConfig.ReflectionTableMetadataResolver"/> and then makes their first query had
/// the resolver overwritten between the two statements, silently.
/// </summary>
/// <remarks>
/// Shares the "Type Handler Operations" collection with
/// <see cref="Jaunty.Tests.Unit.Configuration.ConfigurationGenerationTests"/>: both mutate the
/// process-wide <see cref="JauntyConfig"/> resolvers and must run serialized against each other.
/// </remarks>
[Collection("Type Handler Operations")]
public class ReflectionAutoInitTests
{
    private sealed class Row
    {
        public long Id { get; set; }
    }

    private static EntityMetadata Metadata() => new(
        tableName: "auto_init_probe",
        schemaName: null,
        columns:
        [
            new ColumnMetadata(
                propertyName: nameof(Row.Id),
                propertyType: typeof(long),
                columnName: "id",
                isPrimaryKey: true,
                isIdentity: true,
                isComputed: false,
                getter: static o => ((Row)o).Id,
                setter: static (o, v) => ((Row)o).Id = (long)v!),
        ]);

    [Fact]
    public void AutoInit_DoesNotOverwriteAResolverTheCallerAlreadySet()
    {
        Func<Type, object>? previous = JauntyConfig.ReflectionTableMetadataResolver;
        Func<Type, object> mine = t => t == typeof(Row) ? Metadata() : null!;
        JauntyConfig.ReflectionTableMetadataResolver = mine;

        try
        {
            global::Jaunty.Jaunty.TryEnableReflectionMapping();

            Assert.Same(mine, JauntyConfig.ReflectionTableMetadataResolver);
            Assert.Equal("auto_init_probe",
                (JauntyConfig.ReflectionTableMetadataResolver!(typeof(Row)) as EntityMetadata)!.TableName);
        }
        finally
        {
            JauntyConfig.ReflectionTableMetadataResolver = previous;
        }
    }

    [Fact]
    public void AutoInit_StillInstallsTheHooksNobodySet()
    {
        Func<Type, object>? previous = JauntyConfig.ReflectionTableMetadataResolver;
        JauntyConfig.ReflectionTableMetadataResolver = null;

        try
        {
            global::Jaunty.Jaunty.TryEnableReflectionMapping();

            Assert.NotNull(JauntyConfig.ReflectionTableMetadataResolver);
            Assert.Equal("JauntyReflectionExtensions",
                JauntyConfig.ReflectionTableMetadataResolver!.Method.DeclaringType!.Name);
        }
        finally
        {
            JauntyConfig.ReflectionTableMetadataResolver = previous ?? JauntyConfig.ReflectionTableMetadataResolver;
            JauntyReflectionExtensions.UseReflectionMapping();
        }
    }

    [Fact]
    public void ExplicitUseReflectionMapping_StillReplacesEverything()
    {
        Func<Type, object>? previous = JauntyConfig.ReflectionTableMetadataResolver;
        Func<Type, object> mine = t => t == typeof(Row) ? Metadata() : null!;
        JauntyConfig.ReflectionTableMetadataResolver = mine;

        try
        {
            JauntyReflectionExtensions.UseReflectionMapping();

            Assert.NotSame(mine, JauntyConfig.ReflectionTableMetadataResolver);
        }
        finally
        {
            JauntyConfig.ReflectionTableMetadataResolver = previous;
        }
    }
}
