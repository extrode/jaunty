using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Jaunty.SourceGenerator;

/// <summary>
/// Everything <c>JauntyGenerator</c> needs to emit one entity's mapper, extracted from the
/// compilation and holding no Roslyn symbols, syntax nodes or <see cref="Compilation"/> reference.
/// </summary>
/// <remarks>
/// AUD-R25 (B8-7): this type exists so the generator's expensive half can be skipped. Roslyn's
/// incremental driver caches a step's output and re-runs the next step only when the input compares
/// unequal, so what a step yields decides whether anything downstream can ever be cached. The
/// generator used to yield <c>ClassDeclarationSyntax</c> combined with the
/// <see cref="Compilation"/>: syntax nodes do not implement value equality, and the compilation is
/// a new object on every keystroke anywhere in the project, so the combined input was different
/// every single time and the source-output step re-ran in full - re-deriving metadata for and
/// re-emitting every <c>[Table]</c> entity in the project - for every character typed in any file,
/// including files with no entity in them.
///
/// <para>
/// A model of plain strings and value types compares by content, so editing a method body inside an
/// entity, or anything in an unrelated file, produces an equal model and the emit step is skipped
/// entirely. Registering the source output per entity rather than over the collected array also
/// keeps one entity's edit from re-emitting its neighbours.
/// </para>
///
/// <para>
/// The invariant to preserve when adding to this type: <b>every member must be value-equatable and
/// must not transitively reference the compilation.</b> A single <see cref="ISymbol"/>,
/// <see cref="SyntaxNode"/>, <see cref="Location"/> or bare <c>ImmutableArray</c> field is enough to
/// make every comparison report "changed" and silently restore the old behaviour - and nothing will
/// fail, it will just be slow again. <c>GeneratorCachingTests</c> pins this.
/// </para>
/// </remarks>
internal sealed record EntityModel(
    string? Namespace,
    string ClassName,
    string AccessibilityKeyword,
    string HintName,
    string TableName,
    string? SchemaName,
    EquatableArray<PropertyMetadata> Properties,
    LocationInfo? DiagnosticLocation,
    EquatableArray<ContainingTypeInfo> ContainingTypes = default,
    string? UnsupportedNestingReason = null,
    EquatableArray<DroppedPropertyInfo> DroppedProperties = default);

/// <summary>
/// A property the generated mapper leaves out but the reflection mapper maps, with the reason.
/// </summary>
/// <remarks>
/// AUD-R34-030 (round-33 carry-forward). Only the divergent skips are recorded: a get-only property
/// is skipped by both paths (<c>MetadataBuilder</c> tests <c>property.CanWrite</c>), and an
/// <c>[Ignore]</c>/<c>[NotMapped]</c> one is skipped on purpose by both. An init-only or
/// inaccessible setter is different - <c>CanWrite</c> is true and <c>SetValue</c> reaches both, so
/// the column is mapped under reflection and silently lost the moment the generator package is
/// referenced.
/// </remarks>
internal readonly record struct DroppedPropertyInfo(string PropertyName, string Reason, LocationInfo? Location);

/// <summary>
/// One link in the chain of types enclosing a nested entity, outermost first.
/// </summary>
/// <remarks>
/// AUD-R33-006: the generated partial has to be re-declared inside the same enclosing types as the
/// entity, or it is a different type altogether. Only what the re-declaration needs is kept -
/// the keyword (<c>class</c>, <c>record</c>, <c>struct</c>...) and the accessibility, both of which
/// every part of a partial type must agree on, and the name.
/// </remarks>
internal readonly record struct ContainingTypeInfo(
    string AccessibilityKeyword,
    string Keyword,
    string Name);

/// <summary>
/// Holds the resolved mapping metadata for a single property of an entity class.
/// </summary>
internal readonly record struct PropertyMetadata(
    string PropertyName,
    string ColumnName,
    bool IsPrimaryKey,
    bool IsIdentity,
    bool IsComputed,
    string TypeName,
    bool IsEnum,
    bool IsIdentityInferred,
    int? EnumStorageOverride)
{
    /// <summary>
    /// AUD-R25: whether the property's type - or, for a nullable value type, its underlying
    /// type - is an enum. Enums are the one catch-all type that cannot be read through
    /// <c>GetFieldValue&lt;T&gt;</c> even on the <c>DbDataReader</c> path, because the base
    /// implementation is an unboxing cast.
    /// </summary>
    public bool IsEnum { get; init; } = IsEnum;

    /// <summary>
    /// AUD-R25: whether <see cref="IsIdentity"/> came from the int/long-key convention rather
    /// than an explicit <c>[DatabaseGenerated]</c>. Only inferred identity is withdrawn for a
    /// composite-key entity; an explicit attribute is always honoured.
    /// </summary>
    public bool IsIdentityInferred { get; init; } = IsIdentityInferred;

    /// <summary>
    /// AUD-R30: the <c>[EnumStorage]</c> attribute's raw value (0 = Numeric, 1 = String) when the
    /// property carries one, or <see langword="null"/> to defer to
    /// <c>JauntyConfig.DefaultEnumStorage</c> at bind time. An explicit attribute is immutable and
    /// safe to bake into the generated binder; the config default is mutable process-wide state
    /// and must be read per call - the same split the reflection path's
    /// <c>BuildValueConverter</c> makes. Meaningful only when <see cref="IsEnum"/> is true.
    /// </summary>
    public int? EnumStorageOverride { get; init; } = EnumStorageOverride;
}

/// <summary>
/// A value-equatable stand-in for a <see cref="Location"/>, used to anchor generator diagnostics.
/// </summary>
/// <remarks>
/// AUD-R25 (B8-7): a <see cref="Location"/> cannot be held in a cached model. It carries a
/// <see cref="SyntaxTree"/> reference, which both roots the whole tree in memory for as long as the
/// driver caches the model and changes identity on every edit to that file, defeating the caching
/// the model exists to provide. Path plus spans is the standard substitute; <see cref="ToLocation"/>
/// rebuilds an equivalent location at emit time.
/// </remarks>
internal readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    /// <summary>
    /// Converts a symbol location, or returns <see langword="null"/> for a location with no source
    /// tree (a metadata or external symbol), for which no file-anchored diagnostic is possible.
    /// </summary>
    public static LocationInfo? From(Location? location)
        => location?.SourceTree is null
            ? null
            : new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
}
