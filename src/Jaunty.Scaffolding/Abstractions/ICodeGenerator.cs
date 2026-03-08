using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.Abstractions;

/// <summary>
/// Interface for generating C# entity code from database schema.
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// Generates C# entity code for a table.
    /// </summary>
    /// <param name="table">The table schema.</param>
    /// <param name="options">Code generation options.</param>
    /// <returns>The generated C# code as a string.</returns>
    string GenerateEntity(TableSchema table, CodeGeneratorOptions options);
}