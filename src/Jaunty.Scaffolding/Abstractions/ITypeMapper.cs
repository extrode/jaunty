using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.Abstractions;

/// <summary>
/// Interface for mapping database column types to C# types.
/// </summary>
public interface ITypeMapper
{
    /// <summary>
    /// Maps a database column to its corresponding C# type.
    /// </summary>
    /// <param name="column">The column schema information.</param>
    /// <returns>Information about the C# type to use.</returns>
    CSharpTypeInfo MapToCSharpType(ColumnSchema column);
}
