namespace Jaunty.FlatFiles.Interfaces;

/// <summary>
/// Optional companion to <see cref="IImportDialect"/> for dialects whose target type for a CLR
/// type is not a same-shape widening, so the value has to be reshaped before it is bound.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R34-029. <see cref="IImportDialect.MapClrTypeToSqlType"/>'s contract stops at the declared column type,
/// which is enough while every mapping is a widening - SQL Server and PostgreSQL take
/// <see cref="ulong"/> to <c>DECIMAL(20,0)</c>/<c>NUMERIC(20,0)</c> and bind the value unchanged.
/// SQLite has no unsigned and no 128-bit integer type, so there is no declared type that holds a
/// <see cref="ulong"/> numerically, and the provider silently reinterprets the top half of the
/// range as negative: <see cref="ulong.MaxValue"/> bound through Microsoft.Data.Sqlite arrives as
/// <c>-1</c> whatever the column is declared as. The value itself therefore has to change.
/// </para>
/// <para>
/// Separate interface rather than a member on <see cref="IImportDialect"/> for the same reason
/// <see cref="IQuotedIdentifierDialect"/> is: that interface is public, this assembly targets
/// netstandard2.0, and a default interface implementation is not available to keep existing
/// external implementers compiling. A dialect that does not implement this binds every value
/// unchanged, which is what the pipeline did unconditionally before.
/// </para>
/// </remarks>
public interface IImportValueTransform
{
    /// <summary>
    /// Reshapes a value on its way to an ADO.NET parameter, when this dialect's declared column
    /// type for it is not the value's own shape.
    /// </summary>
    /// <param name="value">The value read from the source, never <see langword="null"/> or DBNull.</param>
    /// <param name="transformed">The value to bind, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the value was reshaped; otherwise <see langword="false"/>.</returns>
    bool TryTransformForBinding(object value, out object transformed);
}
