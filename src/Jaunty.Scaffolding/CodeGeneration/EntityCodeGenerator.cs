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

        // Properties
        string propIndent = options.UseFileScopedNamespace ? "    " : "        ";
        bool isFirst = true;
        foreach (var column in table.Columns)
        {
            if (!isFirst)
                sb.AppendLine();
            isFirst = false;
            AppendProperty(sb, column, options, propIndent);
        }

        sb.AppendLine($"{indent}}}");

        if (!options.UseFileScopedNamespace)
            sb.AppendLine("}");

        return sb.ToString();
    }

    private void AppendUsings(StringBuilder sb, TableSchema table, CodeGeneratorOptions options)
    {
        var usings = new HashSet<string>();

        // Always need Jaunty.Attributes if generating any attributes
        if (options.GenerateTableAttribute || options.GenerateColumnAttribute ||
            options.GenerateKeyAttribute || options.GenerateDatabaseGeneratedAttribute)
        {
            usings.Add("Jaunty.Attributes");
        }

        if (options.AddDataAnnotations)
        {
            usings.Add("System.ComponentModel.DataAnnotations");
            usings.Add("System.ComponentModel.DataAnnotations.Schema");
        }

        // Check if any columns need additional usings
        foreach (var column in table.Columns)
        {
            var typeInfo = _typeMapper.MapToCSharpType(column);
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
                sb.AppendLine($"{indent}[Table(\"{table.TableName}\", \"{table.SchemaName}\")]");
            else
                sb.AppendLine($"{indent}[Table(\"{table.TableName}\")]");
        }
    }

    private void AppendProperty(StringBuilder sb, ColumnSchema column, CodeGeneratorOptions options, string indent)
    {
        var typeInfo = _typeMapper.MapToCSharpType(column);
        var propertyName = GetPropertyName(column.ColumnName);
        var attrs = new List<string>();

        // [Key] attribute
        if (column.IsPrimaryKey && options.GenerateKeyAttribute)
            attrs.Add("[Key]");

        // [DatabaseGenerated] attribute
        if (options.GenerateDatabaseGeneratedAttribute)
        {
            if (column.IsIdentity)
                attrs.Add("[DatabaseGenerated(DatabaseGeneratedOption.Identity)]");
            else if (column.IsComputed)
                attrs.Add("[DatabaseGenerated(DatabaseGeneratedOption.Computed)]");
        }

        // [Column] attribute (only when name differs)
        if (options.GenerateColumnAttribute &&
            !column.ColumnName.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
        {
            attrs.Add($"[Column(\"{column.ColumnName}\")]");
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

    private static string GetClassName(string tableName, CodeGeneratorOptions options)
    {
        var className = NamingHelper.ToPascalCase(tableName);

        if (options.Singularize)
            className = NamingHelper.Singularize(className);

        if (!string.IsNullOrEmpty(options.ClassPrefix))
            className = options.ClassPrefix + className;

        if (!string.IsNullOrEmpty(options.ClassSuffix))
            className = className + options.ClassSuffix;

        return NamingHelper.EscapeIdentifier(className);
    }

    private static string GetPropertyName(string columnName)
    {
        var propertyName = NamingHelper.ToPascalCase(columnName);
        return NamingHelper.EscapeIdentifier(propertyName);
    }
}