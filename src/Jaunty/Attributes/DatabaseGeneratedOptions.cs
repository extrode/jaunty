namespace Jaunty.Attributes;

/// <summary>
/// Specifies how a database generates values for a property.
/// </summary>
public enum DatabaseGeneratedOption
{
    /// <summary>
    /// The database does not generate a value.
    /// </summary>
    None = 0,

    /// <summary>
    /// The database generates a value when a row is inserted.
    /// </summary>
    Identity = 1,

    /// <summary>
    /// The database generates a value when a row is inserted or updated.
    /// </summary>
    Computed = 2
}
