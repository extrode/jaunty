using System.ComponentModel.DataAnnotations.Schema;

using Jaunty.FlatFiles.DuckDB.Internals;

using JauntyColumn = Jaunty.Attributes.ColumnAttribute;
using JauntyKey = Jaunty.Attributes.KeyAttribute;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// Round 35 low, fixed 2026-08-30. The twin of <see cref="EmptyColumnAttributeTests"/>, one
/// attribute along: <c>MappedPropertyFilter.GetColumnName</c> read
/// <c>Jaunty.Attributes.ColumnAttribute</c> only, so an entity annotated with
/// <see cref="ColumnAttribute"/> from DataAnnotations - honoured by <c>MetadataBuilder</c> and,
/// since AUD-R35-070, by <c>JauntyGenerator</c> - mapped by property name on the DuckDB flat-file
/// path and by the attribute's name on every core path.
/// </summary>
public class DataAnnotationsColumnAttributeTests
{
    private sealed class AnnotatedEntity
    {
        [JauntyKey]
        public int Id { get; set; }

        [Column("annotated_name")]
        public string Positional { get; set; } = string.Empty;

        [Column("")]
        public string Empty { get; set; } = string.Empty;

        [Column(TypeName = "varchar")]
        public string TypeOnly { get; set; } = string.Empty;

        // Both attributes: Jaunty's must win, as it does in MetadataBuilder.
        [JauntyColumn("jaunty_wins")]
        [Column("annotations_lose")]
        public string Both { get; set; } = string.Empty;
    }

    private static string ColumnFor(string property) =>
        MappedPropertyFilter.GetColumnName(typeof(AnnotatedEntity).GetProperty(property)!);

    [Fact]
    public void APositionalName_IsUsed() => Assert.Equal("annotated_name", ColumnFor(nameof(AnnotatedEntity.Positional)));

    [Fact]
    public void AnEmptyName_FallsBackToThePropertyName() => Assert.Equal(nameof(AnnotatedEntity.Empty), ColumnFor(nameof(AnnotatedEntity.Empty)));

    [Fact]
    public void TypeNameAlone_FallsBackToThePropertyName() => Assert.Equal(nameof(AnnotatedEntity.TypeOnly), ColumnFor(nameof(AnnotatedEntity.TypeOnly)));

    [Fact]
    public void JauntysOwnAttribute_StillWins() => Assert.Equal("jaunty_wins", ColumnFor(nameof(AnnotatedEntity.Both)));

    [Fact]
    public void NoAttribute_IsThePropertyName() => Assert.Equal(nameof(AnnotatedEntity.Id), ColumnFor(nameof(AnnotatedEntity.Id)));

    [Fact]
    public void ColumnMappingCache_KeysOnTheAnnotatedName()
    {
        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(AnnotatedEntity));

        Assert.Contains("annotated_name", mappings.Keys);
        Assert.Contains("jaunty_wins", mappings.Keys);
        Assert.DoesNotContain(nameof(AnnotatedEntity.Positional), mappings.Keys);
    }
}
