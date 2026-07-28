using System.CommandLine;

using Jaunty.Scaffolding.Configuration;

namespace Jaunty.Scaffolding.Cli.Commands;

internal sealed class ScaffoldCommand : Command
{
    public ScaffoldCommand() : base("scaffold", "Generate entity classes from database schema")
    {
        // Required options
        var connectionOption = new Option<string>("--connection", "-c")
        {
            Description = "Database connection string",
            Required = true
        };

        var providerOption = new Option<DatabaseProvider>("--provider", "-p")
        {
            Description = "Database provider (SqlServer, PostgreSql, MySql, SQLite)",
            DefaultValueFactory = _ => DatabaseProvider.AutoDetect
        };

        var outputOption = new Option<string>("--output", "-o")
        {
            Description = "Output directory for generated files",
            DefaultValueFactory = _ => "./Entities"
        };

        var namespaceOption = new Option<string>("--namespace", "-n")
        {
            Description = "Namespace for generated classes",
            DefaultValueFactory = _ => "Generated.Entities"
        };

        // Table filtering
        var tablesOption = new Option<string[]>("--tables")
        {
            Description = "Only scaffold these tables (space-separated)",
            AllowMultipleArgumentsPerToken = true
        };

        var excludeTablesOption = new Option<string[]>("--exclude-tables")
        {
            Description = "Exclude these tables from scaffolding",
            AllowMultipleArgumentsPerToken = true
        };

        var schemasOption = new Option<string[]>("--schemas")
        {
            Description = "Only scaffold tables in these schemas",
            AllowMultipleArgumentsPerToken = true
        };

        // Attribute options
        var noTableAttrOption = new Option<bool>("--no-table-attribute")
        {
            Description = "Don't generate [Table] attributes"
        };

        var noColumnAttrOption = new Option<bool>("--no-column-attribute")
        {
            Description = "Don't generate [Column] attributes"
        };

        var noKeyAttrOption = new Option<bool>("--no-key-attribute")
        {
            Description = "Don't generate [Key] attributes"
        };

        var noDbGenAttrOption = new Option<bool>("--no-database-generated")
        {
            Description = "Don't generate [DatabaseGenerated] attributes"
        };

        // Nullability and style options
        var noNullableOption = new Option<bool>("--no-nullable")
        {
            Description = "Don't use nullable reference types"
        };

        // AUD-R25: inverted from an opt-in --partial. Scaffolded classes must be partial to
        // compose with Jaunty.SourceGenerator, which emits a partial declaration of its own for any
        // [Table] class; without it the build fails with CS0260 in the user's own scaffolded file.
        var noPartialOption = new Option<bool>("--no-partial")
        {
            Description = "Don't generate partial classes (they will not compose with Jaunty.SourceGenerator)"
        };

        var noSingularizeOption = new Option<bool>("--no-singularize")
        {
            Description = "Don't singularize table names for class names"
        };

        var classPrefixOption = new Option<string?>("--class-prefix")
        {
            Description = "Prefix to add to class names"
        };

        var classSuffixOption = new Option<string?>("--class-suffix")
        {
            Description = "Suffix to add to class names"
        };

        var dataAnnotationsOption = new Option<bool>("--data-annotations")
        {
            Description = "Include System.ComponentModel.DataAnnotations attributes"
        };

        var blockNamespaceOption = new Option<bool>("--block-namespace")
        {
            Description = "Use block-scoped namespaces instead of file-scoped"
        };

        var includeForeignKeysOption = new Option<bool>("--include-foreign-keys")
        {
            Description = "Read foreign key information (for future navigation properties)"
        };

        // Advanced options
        var forceOption = new Option<bool>("--force")
        {
            Description = "Overwrite existing files without prompting"
        };

        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Show what would be generated without writing files"
        };

        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Show detailed output"
        };

        // Add all options
        Options.Add(connectionOption);
        Options.Add(providerOption);
        Options.Add(outputOption);
        Options.Add(namespaceOption);
        Options.Add(tablesOption);
        Options.Add(excludeTablesOption);
        Options.Add(schemasOption);
        Options.Add(noTableAttrOption);
        Options.Add(noColumnAttrOption);
        Options.Add(noKeyAttrOption);
        Options.Add(noDbGenAttrOption);
        Options.Add(noNullableOption);
        Options.Add(noPartialOption);
        Options.Add(noSingularizeOption);
        Options.Add(classPrefixOption);
        Options.Add(classSuffixOption);
        Options.Add(dataAnnotationsOption);
        Options.Add(blockNamespaceOption);
        Options.Add(includeForeignKeysOption);
        Options.Add(forceOption);
        Options.Add(dryRunOption);
        Options.Add(verboseOption);

        SetAction(async (parseResult, cancellationToken) =>
        {
            var connection = parseResult.GetValue(connectionOption)!;
            DatabaseProvider provider = parseResult.GetValue(providerOption);
            var output = parseResult.GetValue(outputOption)!;
            var ns = parseResult.GetValue(namespaceOption)!;
            var tables = parseResult.GetValue(tablesOption) ?? [];
            var excludeTables = parseResult.GetValue(excludeTablesOption) ?? [];
            var schemas = parseResult.GetValue(schemasOption) ?? [];
            var noTableAttr = parseResult.GetValue(noTableAttrOption);
            var noColumnAttr = parseResult.GetValue(noColumnAttrOption);
            var noKeyAttr = parseResult.GetValue(noKeyAttrOption);
            var noDbGenAttr = parseResult.GetValue(noDbGenAttrOption);
            var noNullable = parseResult.GetValue(noNullableOption);
            var noPartial = parseResult.GetValue(noPartialOption);
            var noSingularize = parseResult.GetValue(noSingularizeOption);
            var classPrefix = parseResult.GetValue(classPrefixOption);
            var classSuffix = parseResult.GetValue(classSuffixOption);
            var dataAnnotations = parseResult.GetValue(dataAnnotationsOption);
            var blockNamespace = parseResult.GetValue(blockNamespaceOption);
            var includeForeignKeys = parseResult.GetValue(includeForeignKeysOption);
            var force = parseResult.GetValue(forceOption);
            var dryRun = parseResult.GetValue(dryRunOption);
            var verbose = parseResult.GetValue(verboseOption);

            var options = new ScaffoldOptions
            {
                ConnectionString = connection,
                Provider = provider,
                OutputDirectory = output,
                Namespace = ns,
                IncludeTables = [.. tables],
                ExcludeTables = [.. excludeTables],
                IncludeSchemas = [.. schemas],
                GenerateTableAttribute = !noTableAttr,
                GenerateColumnAttribute = !noColumnAttr,
                GenerateKeyAttribute = !noKeyAttr,
                GenerateDatabaseGeneratedAttribute = !noDbGenAttr,
                UseNullableReferenceTypes = !noNullable,
                GeneratePartialClasses = !noPartial,
                Singularize = !noSingularize,
                ClassPrefix = classPrefix,
                ClassSuffix = classSuffix,
                AddDataAnnotations = dataAnnotations,
                UseFileScopedNamespace = !blockNamespace,
                IncludeForeignKeys = includeForeignKeys,
                Force = force,
                DryRun = dryRun
            };

            if (verbose)
            {
                Console.WriteLine($"Provider: {options.Provider}");
                Console.WriteLine($"Output: {options.OutputDirectory}");
                Console.WriteLine($"Namespace: {options.Namespace}");
                if (dryRun)
                    Console.WriteLine("(Dry run - no files will be written)");
                Console.WriteLine();
            }

            var scaffolder = new Scaffolder();
            ScaffoldResult result = await scaffolder.ScaffoldAsync(options, cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                var action = dryRun ? "Would generate" : "Generated";
                Console.WriteLine($"{action} {result.GeneratedFiles.Count} entity file(s):");
                foreach (var file in result.GeneratedFiles)
                {
                    Console.WriteLine($"  {file}");
                }
                return 0;
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                return 1;
            }
        });
    }
}
