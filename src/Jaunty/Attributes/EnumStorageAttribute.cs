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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="storage"/> is not a defined <see cref="EnumStorage"/> member.
    /// </exception>
    /// <remarks>
    /// AUD-R35-137. The value used to be stored unchecked, and the two mapping paths then disagreed
    /// about what an out-of-range one meant. Reflection - <c>BuildValueConverter</c>,
    /// <c>ParameterBinder.GetEnumStorage</c>, <c>MetadataCache&lt;T&gt;.CreateFallbackSetter</c> -
    /// treats anything that is not <see cref="EnumStorage.String"/> as numeric, so an explicit
    /// attribute always wins. The generator recognises 0 and 1 and emits <c>null</c> for anything
    /// else, at which point <c>GeneratedBindingSupport.ToDbEnumValue</c> falls back to
    /// <c>JauntyConfig.DefaultEnumStorage</c>. So <c>[EnumStorage((EnumStorage)7)]</c> under a global
    /// default of <c>String</c> wrote a number through reflection and a string through the
    /// generator - the same add-the-generator-package silent behaviour change AUD-R30 closed for the
    /// ordinary case. Rejecting the value at its source removes the divergence rather than picking a
    /// winner for it, and a cast to an undefined member is a caller mistake in every case: there is
    /// no third storage strategy to be forward-compatible with.
    /// </remarks>
    public EnumStorageAttribute(EnumStorage storage)
    {
        if (storage is not (EnumStorage.Numeric or EnumStorage.String))
        {
            throw new ArgumentOutOfRangeException(
                nameof(storage),
                storage,
                "Enum storage must be EnumStorage.Numeric or EnumStorage.String.");
        }

        Storage = storage;
    }
}
