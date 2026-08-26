using System.Globalization;
using System.Text;

using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.CodeGeneration;

/// <summary>
/// Generates C# entity classes from database schema.
/// </summary>
public sealed class EntityCodeGenerator : ICodeGenerator
{
    private readonly ITypeMapper _typeMapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityCodeGenerator"/> class.
    /// </summary>
    /// <param name="typeMapper">The type mapper for converting database types to C# types.</param>
    public EntityCodeGenerator(ITypeMapper typeMapper)
    {
        _typeMapper = typeMapper;
    }

    /// <inheritdoc />
    public string GenerateEntity(TableSchema table, CodeGeneratorOptions options)
    {
        // AUD-R26: GenerateEntity is public API in its own right, so the namespace is checked
        // here as well as in Scaffolder.ValidateOptions - a caller using the generator directly
        // gets the same guarantee. A namespace containing a semicolon would otherwise inject
        // arbitrary top-level C# into every generated file and still parse cleanly.
        if (!NamingHelper.IsValidNamespace(options.Namespace))
            throw new ArgumentException(
                $"Namespace '{options.Namespace}' is not a valid C# namespace. It must be a " +
                "dot-separated sequence of identifiers, each starting with a letter or underscore.",
                nameof(options));

        var sb = new StringBuilder();

        // Usings
        AppendUsings(sb, table, options);
        sb.AppendLine();

        // Namespace
        if (options.UseFileScopedNamespace)
        {
            sb.AppendLine($"namespace {options.Namespace};");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"namespace {options.Namespace}");
            sb.AppendLine("{");
        }

        string indent = options.UseFileScopedNamespace ? "" : "    ";

        // Class attributes
        AppendClassAttributes(sb, table, options, indent);

        // Class declaration
        var className = GetClassName(table.TableName, options);
        var partialModifier = options.GeneratePartialClasses ? "partial " : "";
        sb.AppendLine($"{indent}public {partialModifier}class {className}");
        sb.AppendLine($"{indent}{{");

        // Properties. Track generated property names (seeded with the class name itself) so
        // a column whose PascalCased name collides with the class name (CS0542) or with
        // another column's PascalCased name (duplicate member) gets disambiguated instead of
        // emitting code that won't compile.
        string propIndent = options.UseFileScopedNamespace ? "    " : "        ";
        var usedPropertyNames = new HashSet<string>(StringComparer.Ordinal) { className };
        bool isFirst = true;
        foreach (ColumnSchema column in table.Columns)
        {
            if (!isFirst)
                sb.AppendLine();
            isFirst = false;

            var propertyName = ResolvePropertyName(column.ColumnName, usedPropertyNames);
            usedPropertyNames.Add(propertyName);
            AppendProperty(sb, column, options, propIndent, propertyName);
        }

        sb.AppendLine($"{indent}}}");

        if (!options.UseFileScopedNamespace)
            sb.AppendLine("}");

        return sb.ToString();
    }

    private void AppendUsings(StringBuilder sb, TableSchema table, CodeGeneratorOptions options)
    {
        var usings = new HashSet<string>();

        // Jaunty's own [Table]/[Column]/[Key]/[DatabaseGenerated] attributes are emitted fully
        // qualified (see AppendClassAttributes/AppendProperty) rather than via a
        // "using Jaunty.Attributes;" so they never collide with the identically-named
        // System.ComponentModel.DataAnnotations(.Schema) types when AddDataAnnotations is also on.

        if (options.AddDataAnnotations)
        {
            usings.Add("System.ComponentModel.DataAnnotations");
            usings.Add("System.ComponentModel.DataAnnotations.Schema");
        }

        // Check if any columns need additional usings
        foreach (ColumnSchema column in table.Columns)
        {
            CSharpTypeInfo typeInfo = _typeMapper.MapToCSharpType(column);
            if (!string.IsNullOrEmpty(typeInfo.RequiredUsing))
                usings.Add(typeInfo.RequiredUsing);
        }

        // AUD-R35-265: ordinal, not Comparer<string>.Default. The default comparer is
        // culture-sensitive, so the using block's order - and therefore the whole generated file -
        // could differ between two machines running the same scaffold under different cultures.
        foreach (var u in usings.OrderBy(x => x, StringComparer.Ordinal))
            sb.AppendLine($"using {u};");
    }

    private static void AppendClassAttributes(StringBuilder sb, TableSchema table, CodeGeneratorOptions options, string indent)
    {
        if (!options.GenerateTableAttribute)
            return;

        // AUD-R25: always emit [Table]. This used to be skipped when the table name equalled the
        // class name and the schema was empty, on the reasoning that the attribute was redundant -
        // but it is not redundant to Jaunty.SourceGenerator, whose GetSemanticTargetForGeneration
        // requires a [Table] attribute to consider a class at all. The elided case is the *normal*
        // one for two of the four providers - MySqlSchemaReader hardcodes '' AS SchemaName and
        // SQLite has no schemas - so a SQLite table named `Customer` scaffolded to a `Customer`
        // class with no attribute, got no generated mapper, and silently fell back to reflection
        // (or threw, if UseReflectionMapping() was never called) with nothing to indicate why.
        //
        // Set GenerateTableAttribute = false (--no-table-attr) if the attribute really is unwanted;
        // that is an explicit choice rather than an invisible consequence of a name matching.
        if (!string.IsNullOrEmpty(table.SchemaName))
            sb.AppendLine($"{indent}[Jaunty.Attributes.Table(\"{EscapeStringLiteral(table.TableName)}\", \"{EscapeStringLiteral(table.SchemaName)}\")]");
        else
            sb.AppendLine($"{indent}[Jaunty.Attributes.Table(\"{EscapeStringLiteral(table.TableName)}\")]");
    }

    private void AppendProperty(
        StringBuilder sb, ColumnSchema column, CodeGeneratorOptions options, string indent, string propertyName)
    {
        CSharpTypeInfo typeInfo = _typeMapper.MapToCSharpType(column);
        var attrs = new List<string>();

        // [Key] attribute
        if (column.IsPrimaryKey && options.GenerateKeyAttribute)
            attrs.Add("[Jaunty.Attributes.Key]");

        // [DatabaseGenerated] attribute
        if (options.GenerateDatabaseGeneratedAttribute)
        {
            if (column.IsIdentity)
                attrs.Add("[Jaunty.Attributes.DatabaseGenerated(Jaunty.Attributes.DatabaseGeneratedOption.Identity)]");
            else if (column.IsComputed)
                attrs.Add("[Jaunty.Attributes.DatabaseGenerated(Jaunty.Attributes.DatabaseGeneratedOption.Computed)]");
        }

        // [Column] attribute (only when name differs).
        //
        // AUD-R35-036: emitted regardless of GenerateColumnAttribute when the name was
        // disambiguated. ResolvePropertyName renames a colliding property (Name -> Name1), and the
        // only thing that preserves the column mapping afterwards is this attribute. Under
        // --no-column-attr the renamed property was emitted bare, so it mapped to a column "Name1"
        // that does not exist - the reflection path leaves it unset and the generated path resolves
        // a missing ordinal. The disambiguation is generator-internal, not something the caller
        // asked for, so opting out of [Column] silently converted a compile error the generator was
        // fixing into a runtime mapping fault. Opting out is a preference about names that already
        // match; it cannot be a preference about names the generator itself changed.
        bool wasDisambiguated = !string.Equals(propertyName, GetPropertyName(column.ColumnName), StringComparison.Ordinal);

        if ((options.GenerateColumnAttribute || wasDisambiguated) &&
            !column.ColumnName.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
        {
            attrs.Add($"[Jaunty.Attributes.Column(\"{EscapeStringLiteral(column.ColumnName)}\")]");
        }

        // Data annotations
        if (options.AddDataAnnotations)
        {
            if (!column.IsNullable && !typeInfo.IsValueType)
                attrs.Add("[Required]");

            if (column.MaxLength.HasValue && column.MaxLength > 0 && typeInfo.TypeName == "string")
                attrs.Add($"[MaxLength({column.MaxLength})]");
        }

        // Write attributes
        foreach (var attr in attrs)
            sb.AppendLine($"{indent}{attr}");

        // Type with nullability
        string typeName = typeInfo.TypeName;
        if (column.IsNullable)
        {
            if (typeInfo.IsValueType)
                typeName += "?";
            else if (options.UseNullableReferenceTypes)
                typeName += "?";
        }

        // Default value for required reference types
        string defaultValue = "";
        if (!column.IsNullable && !typeInfo.IsValueType && options.UseNullableReferenceTypes)
        {
            defaultValue = typeInfo.TypeName switch
            {
                "string" => " = string.Empty;",
                "byte[]" => " = [];",
                _ => " = null!;"
            };
        }

        sb.AppendLine($"{indent}public {typeName} {propertyName} {{ get; set; }}{defaultValue}");
    }

    private static string GetClassName(string tableName, CodeGeneratorOptions options) =>
        NamingHelper.ToClassName(tableName, options.Singularize, options.ClassPrefix, options.ClassSuffix);

    private static string GetPropertyName(string columnName)
    {
        var propertyName = NamingHelper.ToPascalCase(columnName);
        return NamingHelper.EscapeIdentifier(propertyName);
    }

    /// <summary>
    /// Resolves a column's generated property name, disambiguating it against
    /// <paramref name="usedNames"/> (which is seeded with the class name so a property that
    /// would otherwise collide with its enclosing type - CS0542 - is also caught) by appending
    /// an incrementing numeric suffix until the name is unique.
    /// </summary>
    private static string ResolvePropertyName(string columnName, HashSet<string> usedNames)
    {
        var baseName = GetPropertyName(columnName);
        if (!usedNames.Contains(baseName))
            return baseName;

        var suffix = 1;
        string candidate;
        do
        {
            candidate = baseName + suffix;
            suffix++;
        } while (usedNames.Contains(candidate));

        return candidate;
    }

    /// <summary>
    /// Escapes an arbitrary database identifier so it can be embedded in a generated C# string
    /// literal (e.g. inside [Table("...")]/[Column("...")]).
    /// </summary>
    /// <remarks>
    /// AUD-R26: this handled backslashes and double quotes but not control characters, and a
    /// regular C# string literal cannot span a line. A quoted identifier may contain one -
    /// <c>CREATE TABLE t ("line1\nline2" TEXT)</c> is accepted by SQLite and by SQL Server in
    /// bracket form. Measured: such a column emitted
    /// <code>
    ///     [Jaunty.Attributes.Column("line1
    ///     line2")]
    /// </code>
    /// which does not compile (CS1010, newline in constant). Escaping only the two characters
    /// that break *most* identifiers left the generator producing a file the user cannot build,
    /// with the cause several steps removed from the table it came from.
    /// </remarks>
    private static string EscapeStringLiteral(string value)
    {
        var sb = new StringBuilder(value.Length);

        foreach (char c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\0': sb.Append("\\0"); break;
                case '\a': sb.Append("\\a"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\v': sb.Append("\\v"); break;
                default:
                    // Any other control character - including the Unicode line separators
                    // U+2028/U+2029, which the C# lexer also treats as line terminators.
                    if (char.IsControl(c) || c is '\u0085' or '\u2028' or '\u2029')
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }
}