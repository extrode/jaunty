namespace Jaunty.Scaffolding.Abstractions;

/// <summary>
/// Options for code generation.
/// </summary>
public sealed class CodeGeneratorOptions
{
    /// <summary>
    /// The namespace for generated classes.
    /// </summary>
    public string Namespace { get; init; } = "Generated.Entities";

    /// <summary>
    /// Whether to use file-scoped namespaces (C# 10+). Default is true.
    /// </summary>
    public bool UseFileScopedNamespace { get; init; } = true;

    /// <summary>
    /// Whether to generate [Table] attributes.
    /// </summary>
    public bool GenerateTableAttribute { get; init; } = true;

    /// <summary>
    /// Whether to generate [Column] attributes when column name differs from property name.
    /// </summary>
    public bool GenerateColumnAttribute { get; init; } = true;

    /// <summary>
    /// Whether to generate [Key] attributes.
    /// </summary>
    public bool GenerateKeyAttribute { get; init; } = true;

    /// <summary>
    /// Whether to generate [DatabaseGenerated] attributes.
    /// </summary>
    public bool GenerateDatabaseGeneratedAttribute { get; init; } = true;

    /// <summary>
    /// Whether to use nullable reference types (string? instead of string).
    /// </summary>
    public bool UseNullableReferenceTypes { get; init; } = true;

    /// <summary>
    /// Whether to singularize table names for class names (Products -> Product).
    /// </summary>
    public bool Singularize { get; init; } = true;

    /// <summary>
    /// Whether to generate partial classes.
    /// </summary>
    public bool GeneratePartialClasses { get; init; }

    /// <summary>
    /// Optional prefix to add to class names.
    /// </summary>
    public string? ClassPrefix { get; init; }

    /// <summary>
    /// Optional suffix to add to class names.
    /// </summary>
    public string? ClassSuffix { get; init; }

    /// <summary>
    /// Whether to add System.ComponentModel.DataAnnotations attributes.
    /// </summary>
    public bool AddDataAnnotations { get; init; }
}