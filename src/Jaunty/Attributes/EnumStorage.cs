namespace Jaunty.Attributes;

/// <summary>
/// Specifies how enum values are stored in the database.
/// </summary>
public enum EnumStorage
{
    /// <summary>
    /// Enum values are stored as their numeric (integer) representation.
    /// This is the default storage strategy.
    /// </summary>
    Numeric = 0,

    /// <summary>
    /// Enum values are stored as their string names.
    /// </summary>
    String = 1
}
