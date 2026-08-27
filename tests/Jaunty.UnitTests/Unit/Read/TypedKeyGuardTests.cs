using Jaunty.Internals;
using Jaunty.Interfaces;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R26 (batch 1, low/performance). The typed-key families validated the key with
/// <c>ArgumentNullException.ThrowIfNull(id)</c>, which takes <c>object?</c>, so every call with a
/// value-type key boxed it purely to run a null test that can never fire - in the very family that
/// exists to avoid boxing.
///
/// <para>
/// Measured before the fix with <see cref="GC.GetAllocatedBytesForCurrentThread"/> over 100,000
/// calls: <c>ThrowIfNull(int)</c> allocated 2,400,000 bytes (24 per call) and
/// <c>ThrowIfNull(Guid)</c> 3,200,000 (32 per call). <see cref="KeyGuard.ThrowIfNull"/> allocated
/// zero for both.
/// </para>
///
/// <para>
/// 010 T6: .NET 10's escape analysis elides the <c>ThrowIfNull</c> box outright - measured through
/// <see cref="Measure"/> on 2026-07-30, the old form allocated 239,952 bytes on net8 (the box
/// survives only until tier-up) and 0 on net10. The original comparison is therefore true on
/// net8/net472 and moot on net10, and the old form can no longer serve as the control: a workload
/// the JIT can prove dead proves nothing about the harness. The control below boxes into a static
/// sink instead, which escapes by construction on every runtime (2,400,000 bytes on both net8 and
/// net10 through the same harness).
/// </para>
/// </summary>
public class TypedKeyGuardTests
{
    // ------------------------------------------------------------------
    // The guard allocates nothing for a value-type key (net8 only)
    // ------------------------------------------------------------------

#if NET8_0_OR_GREATER
    // GC.GetAllocatedBytesForCurrentThread and ArgumentNullException.ThrowIfNull are both
    // net8-only; this project also targets net472, where the boxing claim cannot be measured at
    // all. The behaviour assertions below are unguarded and run on both.

    private static long Measure(Action body, int iterations)
    {
        // Warm up so the method is tiered up and the typeof(TId).IsValueType branch is folded;
        // measuring a cold, unoptimised call would measure the JIT, not the box.
        for (int i = 0; i < 1_000; i++)
            body();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < iterations; i++)
            body();

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void AnIntKey_AllocatesNothing()
    {
        int id = 42;
        Assert.Equal(0, Measure(() => KeyGuard.ThrowIfNull(id, "id"), 100_000));
    }

    [Fact]
    public void ALongKey_AllocatesNothing()
    {
        long id = 42L;
        Assert.Equal(0, Measure(() => KeyGuard.ThrowIfNull(id, "id"), 100_000));
    }

    [Fact]
    public void AGuidKey_AllocatesNothing()
    {
        Guid id = Guid.NewGuid();
        Assert.Equal(0, Measure(() => KeyGuard.ThrowIfNull(id, "id"), 100_000));
    }

    /// <summary>
    /// The control. Without it a passing test above proves only that the measurement is blind - this
    /// is what shows the harness does see a box when there is one to see. The workload boxes into a
    /// static sink because it must escape: .NET 10 elides the non-escaping
    /// <c>ArgumentNullException.ThrowIfNull(id)</c> box this test used until 010 T6 (see the class
    /// remarks). Asserts <c>&gt; 0</c> and never a magnitude - almost every store is dead after the
    /// first, so a future JIT that elides dead static stores would leave exactly one box, and a
    /// magnitude assertion would turn into a JIT-version tripwire.
    /// </summary>
    [Fact]
    public void AnEscapingBox_IsSeen_WhichIsWhatMakesTheMeasurementMeaningful()
    {
        int id = 42;
        Assert.True(Measure(() => _boxSink = id, 100_000) > 0);
    }

    private static object? _boxSink;

#endif

    // ------------------------------------------------------------------
    // Behaviour is unchanged for every key that could ever have thrown
    // ------------------------------------------------------------------

    [Fact]
    public void ANullReferenceKey_StillThrows()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => KeyGuard.ThrowIfNull<string>(null!, "id"));

        Assert.Equal("id", ex.ParamName);
    }

    [Fact]
    public void ANullNullableValueKey_StillThrows()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => KeyGuard.ThrowIfNull<int?>(null, "id"));

        Assert.Equal("id", ex.ParamName);
    }

    [Fact]
    public void ANonNullReferenceKey_DoesNotThrow()
        => KeyGuard.ThrowIfNull("abc", "id");

    /// <summary>
    /// A value-type key can never be null, so it must never throw - including default(TId), which is
    /// the case a naive <c>EqualityComparer&lt;TId&gt;.Default.Equals(id, default)</c> rewrite would
    /// have broken by rejecting id 0.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void AValueTypeKey_NeverThrows(int id)
        => KeyGuard.ThrowIfNull(id, "id");

    [Fact]
    public void AnEmptyGuidKey_DoesNotThrow()
        => KeyGuard.ThrowIfNull(Guid.Empty, "id");

    // ------------------------------------------------------------------
    // End to end, through the public typed-key APIs
    // ------------------------------------------------------------------

    [Fact]
    public void Get_WithANullReferenceKey_StillThrowsWithTheSameParamName()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");

        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => connection.Get<KeyedByString, string>(null!));

        Assert.Equal("id", ex.ParamName);
    }

    [Fact]
    public void Delete_WithANullReferenceKey_StillThrowsWithTheSameParamName()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");

        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => connection.Delete<KeyedByString, string>(null!));

        Assert.Equal("id", ex.ParamName);
    }

    /// <summary>
    /// The connection guard sits in the same block and must keep firing first, unchanged - the fix
    /// split the two checks apart, which is exactly where an ordering regression would hide.
    /// </summary>
    [Fact]
    public void ANullConnection_StillThrowsForTheConnection_NotTheKey()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => Jaunty.Get<KeyedByString, string>(null!, null!));

        Assert.Equal("connection", ex.ParamName);
    }

    private sealed class KeyedByString : IEntity<string>
    {
        public string Id { get; set; } = string.Empty;
    }
}
