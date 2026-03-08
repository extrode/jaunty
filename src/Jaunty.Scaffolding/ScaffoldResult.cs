namespace Jaunty.Scaffolding;

/// <summary>
/// Result of a scaffolding operation.
/// </summary>
public sealed class ScaffoldResult
{
    /// <summary>
    /// Whether the scaffolding operation succeeded.
    /// </summary>
    public bool Success { get; private init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? Error { get; private init; }

    /// <summary>
    /// List of generated file paths.
    /// </summary>
    public IReadOnlyList<string> GeneratedFiles { get; private init; } = [];

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ScaffoldResult Succeeded(IReadOnlyList<string> files) =>
        new() { Success = true, GeneratedFiles = files };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static ScaffoldResult Failed(string error) =>
        new() { Success = false, Error = error };
}