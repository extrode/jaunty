using System.Reflection;

using Jaunty.Attributes;
using Jaunty.Configuration;

namespace Jaunty.FlatFiles.DuckDB.Internals.Import;

/// <summary>
/// The part of <c>IImportDialect.MapClrTypeToSqlType</c> that is the same on every engine:
/// deciding <em>which</em> CLR type a column is really about, and what to do when the answer is a
/// type the dialect has no mapping for.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R26 (batch 7, medium/bug). All three import dialects mapped thirteen CLR types explicitly
/// and sent everything else to a text column - <c>_ =&gt; "NVARCHAR(MAX)"</c> on SQL Server,
/// <c>_ =&gt; "TEXT"</c> on PostgreSQL and SQLite. The unmapped set was not exotic: <b>any enum</b>,
/// <see cref="DateOnly"/>, <see cref="TimeOnly"/>, <see cref="TimeSpan"/>, <see cref="char"/>,
/// <see cref="uint"/>, <see cref="ulong"/>, <see cref="sbyte"/> and <see cref="ushort"/>.
/// </para>
/// <para>
/// The result was a table whose column type did not correspond to the entity the caller supplied,
/// with no diagnostic. The parameterized INSERT that follows then binds a value of the real CLR
/// type into that text column. SQLite's dynamic typing absorbs it; PostgreSQL does not - Npgsql
/// maps <see cref="DateOnly"/> to <c>date</c> and an enum to its numeric or named type, and writing
/// either into a <c>TEXT</c> column fails at insert time, one step removed from the DDL that caused
/// it and with no mention of the column type.
/// </para>
/// <para>
/// A silent widening default is the wrong shape for a DDL generator. The types above are now mapped
/// on every dialect, and anything still unrecognised throws instead of quietly becoming text.
/// </para>
/// </remarks>
internal static class ImportTypeMapping
{
    /// <summary>
    /// Reduces a property's CLR type to the type the dialect should map: <see cref="Nullable{T}"/>
    /// unwrapped, and an enum replaced by whatever it is actually stored as.
    /// </summary>
    /// <param name="clrType">The type taken from the entity property.</param>
    /// <param name="property">
    /// The property it came from, when the caller has it. Only used to read
    /// <see cref="EnumStorageAttribute"/>, which is per-property and therefore invisible to
    /// <c>MapClrTypeToSqlType(Type)</c> - a dialect called directly with an enum falls back to
    /// <see cref="JauntyConfig.DefaultEnumStorage"/>, which is the best answer available from a
    /// type alone.
    /// </param>
    /// <remarks>
    /// <para>
    /// The <see cref="Nullable{T}"/> unwrapping is deliberately here as well as in
    /// <c>TargetDdlGenerator.GetColumnDefinitions</c>, which already did it before calling the
    /// dialect. <c>MapClrTypeToSqlType</c> is a <b>public</b> member of a <b>public</b> interface
    /// with no documented precondition, so any other caller that did not happen to replicate that
    /// line got <c>NVARCHAR(MAX)</c> for an <c>int?</c>. Unwrapping in both places costs one null
    /// check and removes a trap.
    /// </para>
    /// <para>
    /// Enum storage follows the rest of the library rather than inventing a rule:
    /// <see cref="EnumStorage.String"/> stores the name, so the column is a string column;
    /// <see cref="EnumStorage.Numeric"/> stores the value, so the column is whatever the enum's
    /// underlying integral type maps to - which is why this returns a <see cref="Type"/> rather
    /// than a SQL type name, leaving the engine-specific half to the dialect.
    /// </para>
    /// </remarks>
    public static Type Normalize(Type clrType, PropertyInfo? property = null)
    {
        Type type = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (!type.IsEnum)
            return type;

        EnumStorage storage =
            property?.GetCustomAttribute<EnumStorageAttribute>()?.Storage
            ?? JauntyConfig.DefaultEnumStorage;

        return storage == EnumStorage.String
            ? typeof(string)
            : Enum.GetUnderlyingType(type);
    }

    /// <summary>
    /// The exception a dialect throws for a type it has no column type for.
    /// </summary>
    /// <remarks>
    /// Naming the type and the engine is the whole point: the alternative this replaced produced a
    /// text column and surfaced as an insert-time error that mentioned neither. Callers that know
    /// the property add its name on the way out - see
    /// <c>TargetDdlGenerator.GetColumnDefinitions</c>.
    /// </remarks>
    public static NotSupportedException Unsupported(Type clrType, string engineName) =>
        new($"{engineName} import has no column type for '{clrType.FullName ?? clrType.Name}'. " +
            "The import pipeline creates the target table from the entity, so every mapped " +
            "property needs a column type it can be inserted into. Map the property to a " +
            "supported type, exclude it from the entity, or create the target table yourself " +
            "before importing.");

    /// <summary>
    /// Maps one column's type, adding the column name to
    /// <see cref="Unsupported(Type, string)"/>'s message on the way out.
    /// </summary>
    /// <remarks>
    /// The dialects' <c>MapClrTypeToSqlType</c> takes a <see cref="Type"/> and so cannot name the
    /// column it is failing on, but the CREATE TABLE loop can - and in a table with several columns
    /// of the same unsupported type, the type alone does not say which one to change. The column
    /// name rather than the property name because that is what appears in the DDL, and because
    /// <c>[Column]</c> may have renamed it.
    /// </remarks>
    public static string MapForColumn(Func<Type, string> map, string columnName, Type clrType)
    {
        try
        {
            return map(clrType);
        }
        catch (NotSupportedException ex)
        {
            throw new NotSupportedException($"Column '{columnName}': {ex.Message}", ex);
        }
    }
}
