using System.CommandLine;

using Jaunty.Scaffolding.Configuration;

namespace Jaunty.Scaffolding.Cli.Commands;

internal sealed class ScaffoldCommand : Command
{
    public ScaffoldCommand() : base("scaffold", "Generate entity classes from database schema")
    {
        // Required options
        var connectionOption = new Option<string>(
            aliases: ["--connection", "-c"],
            description: "Database connection string")
        { IsRequired = true };

        var providerOption = new Option<DatabaseProvider>(
            aliases: ["--provider", "-p"],
            description: "Database provider (SqlServer, PostgreSql, MySql, SQLite)",
            getDefaultValue: () => DatabaseProvider.AutoDetect);

        var outputOption = new Option<string>(
            aliases: ["--output", "-o"],
            description: "Output directory for generated files",
            getDefaultValue: () => "./Entities");

        var namespaceOption = new Option<string>(
            aliases: ["--namespace", "-n"],
            description: "Namespace for generated classes",
            getDefaultValue: () => "Generated.Entities");

        // Table filtering
        var tablesOption = new Option<string[]>(
            "--tables",
            description: "Only scaffold these tables (comma-separated)")
        { AllowMultipleArgumentsPerToken = true };

        var excludeTablesOption = new Option<string[]>(
            "--exclude-tables",
            description: "Exclude these tables from scaffolding")
        { AllowMultipleArgumentsPerToken = true };

        var schemasOption = new Option<string[]>(
            "--schemas",
            description: "Only scaffold tables in these schemas")
        { AllowMultipleArgumentsPerToken = true };

        // Attribute options
        var noTableAttrOption = new Option<bool>(
            "--no-table-attribute",
            description: "Don't generate [Table] attributes");

        var noColumnAttrOption = new Option<bool>(
            "--no-column-attribute",
            description: "Don't generate [Column] attributes");

        var noKeyAttrOption = new Option<bool>(
            "--no-key-attribute",
            description: "Don't generate [Key] attributes");

        var noDbGenAttrOption = new Option<bool>(
            "--no-database-generated",
            description: "Don't generate [DatabaseGenerated] attributes");

        // Nullability and style options
        var noNullableOption = new Option<bool>(
            "--no-nullable",
            description: "Don't use nullable reference types");

        var partialOption = new Option<bool>(
            "--partial",
            description: "Generate partial classes");

        var noSingularizeOption = new Option<bool>(
            "--no-singularize",
            description: "Don't singularize table names for class names");

        var classPrefixOption = new Option<string?>(
            "--class-prefix",
            description: "Prefix to add to class names");

        var classSuffixOption = new Option<string?>(
            "--class-suffix",
            description: "Suffix to add to class names");

        var dataAnnotationsOption = new Option<bool>(
            "--data-annotations",
            description: "Include System.ComponentModel.DataAnnotations attributes");

        var blockNamespaceOption = new Option<bool>(
            "--block-namespace",
            description: "Use block-scoped namespaces instead of file-scoped");

        // Advanced options
        var forceOption = new Option<bool>(
            "--force",
            description: "Overwrite existing files without prompting");

        var dryRunOption = new Option<bool>(
            "--dry-run",
            description: "Show what would be generated without writing files");

        var verboseOption = new Option<bool>(
            "--verbose",
            description: "Show detailed output");

        // Add all options
        AddOption(connectionOption);
        AddOption(providerOption);
        AddOption(outputOption);
        AddOption(namespaceOption);
        AddOption(tablesOption);
        AddOption(excludeTablesOption);
        AddOption(schemasOption);
        AddOption(noTableAttrOption);
        AddOption(noColumnAttrOption);
        AddOption(noKeyAttrOption);
        AddOption(noDbGenAttrOption);
        AddOption(noNullableOption);
        AddOption(partialOption);
        AddOption(noSingularizeOption);
        AddOption(classPrefixOption);
        AddOption(classSuffixOption);
        AddOption(dataAnnotationsOption);
        AddOption(blockNamespaceOption);
        AddOption(forceOption);
        AddOption(dryRunOption);
        AddOption(verboseOption);

        this.SetHandler(async (context) =>
        {
            var connection = context.ParseResult.GetValueForOption(connectionOption)!;
            DatabaseProvider provider = context.ParseResult.GetValueForOption(providerOption);
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var ns = context.ParseResult.GetValueForOption(namespaceOption)!;
            var tables = context.ParseResult.GetValueForOption(tablesOption) ?? [];
            var excludeTables = context.ParseResult.GetValueForOption(excludeTablesOption) ?? [];
            var schemas = context.ParseResult.GetValueForOption(schemasOption) ?? [];
            var noTableAttr = context.ParseResult.GetValueForOption(noTableAttrOption);
            var noColumnAttr = context.ParseResult.GetValueForOption(noColumnAttrOption);
            var noKeyAttr = context.ParseResult.GetValueForOption(noKeyAttrOption);
            var noDbGenAttr = context.ParseResult.GetValueForOption(noDbGenAttrOption);
            var noNullable = context.ParseResult.GetValueForOption(noNullableOption);
            var partial = context.ParseResult.GetValueForOption(partialOption);
            var noSingularize = context.ParseResult.GetValueForOption(noSingularizeOption);
            var classPrefix = context.ParseResult.GetValueForOption(classPrefixOption);
            var classSuffix = context.ParseResult.GetValueForOption(classSuffixOption);
            var dataAnnotations = context.ParseResult.GetValueForOption(dataAnnotationsOption);
            var blockNamespace = context.ParseResult.GetValueForOption(blockNamespaceOption);
            var force = context.ParseResult.GetValueForOption(forceOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

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
                GeneratePartialClasses = partial,
                Singularize = !noSingularize,
                ClassPrefix = classPrefix,
                ClassSuffix = classSuffix,
                AddDataAnnotations = dataAnnotations,
                UseFileScopedNamespace = !blockNamespace,
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
            ScaffoldResult result = await scaffolder.ScaffoldAsync(options, context.GetCancellationToken()).ConfigureAwait(false);

            if (result.Success)
            {
                var action = dryRun ? "Would generate" : "Generated";
                Console.WriteLine($"{action} {result.GeneratedFiles.Count} entity file(s):");
                foreach (var file in result.GeneratedFiles)
                {
                    Console.WriteLine($"  {file}");
                }
                context.ExitCode = 0;
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                context.ExitCode = 1;
            }
        });
    }
}