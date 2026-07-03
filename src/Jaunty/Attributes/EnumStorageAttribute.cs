namespace Jaunty.Attributes;

/// <summary>
/// Specifies how an enum property is stored in the database.
/// </summary>
/// <remarks>
/// <para>
/// Use this attribute to override the global <see cref="Configuration.JauntyConfig.DefaultEnumStorage"/> strategy
/// on a per-property basis. If not specified, the global default is used.
/// </para>
/// <para>
/// This attribute is only applicable to enum-typed properties. It has no effect on non-enum properties.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using Jaunty.Attributes;
/// 
/// public class Order
/// {
///     [Key]
///     public int Id { get; set; }
///     
///     // Stored as numeric (inherited from global default or JauntyConfig.DefaultEnumStorage)
///     public OrderStatus Status { get; set; }
///     
///     // Stored as string name, overriding global setting
///     [EnumStorage(EnumStorage.String)]
///     public OrderPriority Priority { get; set; }
/// }
/// 
/// public enum OrderStatus { Pending = 0, Completed = 1 }
/// public enum OrderPriority { Low = 0, High = 1 }
/// 
/// // With this configuration, Status is stored as 0 or 1,
/// // while Priority is stored as "Low" or "High".
/// </code>
/// </example>
/// <seealso cref="EnumStorage"/>
/// <seealso cref="Configuration.JauntyConfig.DefaultEnumStorage"/>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class EnumStorageAttribute : Attribute
{
    /// <summary>
    /// Gets the enum storage strategy for this property.
    /// </summary>
    public EnumStorage Storage { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnumStorageAttribute"/> class.
    /// </summary>
    /// <param name="storage">The enum storage strategy.</param>
    public EnumStorageAttribute(EnumStorage storage)
    {
        Storage = storage;
    }
}
