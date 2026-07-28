namespace Jaunty.Scaffolding.Configuration;

/// <summary>
/// Configuration options for scaffolding entities from a database.
/// </summary>
public sealed class ScaffoldOptions
{
    // Connection settings

    /// <summary>
    /// The database connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The database provider to use. Default is AutoDetect.
    /// </summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.AutoDetect;

    // Output settings

    /// <summary>
    /// The output directory for generated files. Default is "./Entities".
    /// </summary>
    public string OutputDirectory { get; set; } = "./Entities";

    /// <summary>
    /// The namespace for generated classes. Default is "Generated.Entities".
    /// </summary>
    public string Namespace { get; set; } = "Generated.Entities";

    /// <summary>
    /// Whether to use file-scoped namespaces (C# 10+). Default is true.
    /// </summary>
    public bool UseFileScopedNamespace { get; set; } = true;

    // Table filtering

    /// <summary>
    /// If specified, only scaffold these tables.
    /// </summary>
    public List<string> IncludeTables { get; set; } = [];

    /// <summary>
    /// Tables to exclude from scaffolding.
    /// </summary>
    public List<string> ExcludeTables { get; set; } = [];

    /// <summary>
    /// If specified, only scaffold tables from these schemas.
    /// </summary>
    public List<string> IncludeSchemas { get; set; } = [];

    // Naming options

    /// <summary>
    /// Whether to singularize table names for class names (Products -> Product). Default is true.
    /// </summary>
    public bool Singularize { get; set; } = true;

    /// <summary>
    /// Optional prefix to add to class names.
    /// </summary>
    public string? ClassPrefix { get; set; }

    /// <summary>
    /// Optional suffix to add to class names.
    /// </summary>
    public string? ClassSuffix { get; set; }

    // Attribute generation

    /// <summary>
    /// Whether to generate [Table] attributes. Default is true.
    /// </summary>
    public bool GenerateTableAttribute { get; set; } = true;

    /// <summary>
    /// Whether to generate [Column] attributes when column name differs from property name. Default is true.
    /// </summary>
    public bool GenerateColumnAttribute { get; set; } = true;

    /// <summary>
    /// Whether to generate [Key] attributes. Default is true.
    /// </summary>
    public bool GenerateKeyAttribute { get; set; } = true;

    /// <summary>
    /// Whether to generate [DatabaseGenerated] attributes. Default is true.
    /// </summary>
    public bool GenerateDatabaseGeneratedAttribute { get; set; } = true;

    // Nullability and other code style

    /// <summary>
    /// Whether to use nullable reference types (string? instead of string). Default is true.
    /// </summary>
    public bool UseNullableReferenceTypes { get; set; } = true;

    /// <summary>
    /// Whether to generate partial classes. Default is true.
    /// </summary>
    /// <remarks>
    /// AUD-R25: see <c>CodeGeneratorOptions.GeneratePartialClasses</c>
    /// - scaffolded output that is not <c>partial</c> fails to compile with CS0260 as soon as
    /// <c>Jaunty.SourceGenerator</c> is referenced.
    /// </remarks>
    public bool GeneratePartialClasses { get; set; } = true;

    /// <summary>
    /// Whether to add System.ComponentModel.DataAnnotations attributes. Default is false.
    /// </summary>
    public bool AddDataAnnotations { get; set; }

    // Advanced options

    /// <summary>
    /// Whether to read foreign key information (for future navigation properties). Default is false.
    /// </summary>
    public bool IncludeForeignKeys { get; set; }

    /// <summary>
    /// Whether to overwrite existing files without prompting. Default is false.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// Whether to show what would be generated without writing files. Default is false.
    /// </summary>
    public bool DryRun { get; set; }
}