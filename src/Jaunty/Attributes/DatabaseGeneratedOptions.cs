namespace Jaunty.Attributes;

/// <summary>
/// Specifies how a database generates values for a property.
/// </summary>
/// <remarks>
/// <para>
/// This enumeration is used with the <see cref="DatabaseGeneratedAttribute"/> to indicate 
/// how the database handles value generation for a property.
/// </para>
/// </remarks>
/// <seealso cref="DatabaseGeneratedAttribute"/>
public enum DatabaseGeneratedOption
{
    /// <summary>
    /// The database does not generate a value. The application must provide a value.
    /// </summary>
    /// <remarks>
    /// Use this option for properties where the application is responsible for setting the value,
    /// such as GUIDs generated in code or business keys.
    /// </remarks>
    /// <example>
    /// <code>
    /// [DatabaseGenerated(DatabaseGeneratedOption.None)]
    /// public Guid ExternalId { get; set; }
    /// </code>
    /// </example>
    None = 0,

    /// <summary>
    /// The database generates a value when a row is inserted.
    /// </summary>
    /// <remarks>
    /// Use this option for identity columns (auto-increment) or columns with default values.
    /// The value is generated only on INSERT, not on UPDATE.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Key]
    /// [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    /// public int Id { get; set; }
    /// </code>
    /// </example>
    Identity = 1,

    /// <summary>
    /// The database generates a value when a row is inserted or updated.
    /// </summary>
    /// <remarks>
    /// Use this option for computed columns, triggers, or columns with DEFAULT constraints 
    /// that may change on UPDATE. The value is generated on both INSERT and UPDATE operations.
    /// </remarks>
    /// <example>
    /// <code>
    /// [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    /// public DateTime LastModified { get; set; }
    /// 
    /// [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    /// public string FullName { get; set; }  // Computed from FirstName + LastName
    /// </code>
    /// </example>
    Computed = 2
}