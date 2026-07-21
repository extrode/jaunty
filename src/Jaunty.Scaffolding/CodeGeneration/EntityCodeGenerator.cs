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

        foreach (var u in usings.OrderBy(x => x))
            sb.AppendLine($"using {u};");
    }

    private static void AppendClassAttributes(StringBuilder sb, TableSchema table, CodeGeneratorOptions options, string indent)
    {
        if (!options.GenerateTableAttribute)
            return;

        var className = GetClassName(table.TableName, options);

        // Only emit [Table] if name differs from class name or schema is specified
        bool needsTableAttr = !table.TableName.Equals(className, StringComparison.OrdinalIgnoreCase) ||
                              !string.IsNullOrEmpty(table.SchemaName);

        if (needsTableAttr)
        {
            if (!string.IsNullOrEmpty(table.SchemaName))
                sb.AppendLine($"{indent}[Jaunty.Attributes.Table(\"{EscapeStringLiteral(table.TableName)}\", \"{EscapeStringLiteral(table.SchemaName)}\")]");
            else
                sb.AppendLine($"{indent}[Jaunty.Attributes.Table(\"{EscapeStringLiteral(table.TableName)}\")]");
        }
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

        // [Column] attribute (only when name differs)
        if (options.GenerateColumnAttribute &&
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
    /// Escapes backslashes and double quotes so an arbitrary database identifier can be safely
    /// embedded in a generated C# string literal (e.g. inside [Table("...")]/[Column("...")]).
    /// </summary>
    private static string EscapeStringLiteral(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}