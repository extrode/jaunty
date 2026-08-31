using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

/// <summary>
/// A custom type no other entity in this assembly uses, so a type handler registered for it in one
/// test cannot perturb another running beside it.
/// </summary>
public readonly struct GenMoney(decimal amount)
{
    public decimal Amount { get; } = amount;

    public override string ToString() => Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// AUD-R35. The generated write path has consulted <c>TypeHandlerRegistry</c> since AUD-R30-002 and
/// the reflection read path re-resolves it per call, but the generated read path never did - so a
/// registered handler converted this property going in and not coming back out.
/// </summary>
[Table("gen_handled")]
public partial class GenHandledEntity : IMapped<GenHandledEntity>
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("amount")]
    public GenMoney Amount { get; set; }
}
