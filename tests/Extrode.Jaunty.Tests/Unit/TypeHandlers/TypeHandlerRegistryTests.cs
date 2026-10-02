using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.TypeHandlers;
using Extrode.Jaunty.Tests.Helpers;

namespace Extrode.Jaunty.Tests.Unit.TypeHandlers;

/// <summary>
/// Unit tests for the TypeHandler registration and registry APIs.
/// </summary>
/// <remarks>
/// Shares the "Type Handler Operations" collection with
/// <see cref="Extrode.Jaunty.Tests.Unit.TypeHandlers.EnumStorageTests"/>,
/// <see cref="Extrode.Jaunty.Tests.Integration.TypeHandlers.TypeHandlerRoundTripTests"/>, and
/// <see cref="Extrode.Jaunty.Tests.Unit.Read.ParameterBinderTests"/> — all mutate the same process-wide
/// <see cref="JauntyConfig.DefaultEnumStorage"/>/type-handler registry static state and must run
/// serialized against each other.
/// </remarks>
[Collection("Type Handler Operations")]
public class TypeHandlerRegistryTests : IDisposable
{
    public void Dispose()
    {
        // Only remove the specific handlers this test class may have registered.
        // Do NOT call JauntyConfig.Reset() — it wipes ReflectionMapperResolver,
        // causing cross-test mapper failures when running in parallel.
        TypeHandlerRegistry.Remove<int>();
        TypeHandlerRegistry.Remove<string>();
        TypeHandlerRegistry.Remove<Guid>();
        JauntyConfig.Reconfigure(jc => jc.DefaultEnumStorage = EnumStorage.Numeric);
    }

    #region Delegate-based Registration

    [Fact]
    public void RegisterTypeHandler_WithDelegates_RegistersAndRetrievesCorrectly()
    {
        // Arrange & Act
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<int>(
            fromDb: dbValue => dbValue is int i ? i : 0,
            toDb: value => value.ToString()
        ));

        // Assert - the delegate-based handler is retrievable and parses as registered
        bool found = TypeHandlerRegistry.TryGetHandler(typeof(int), out var handler);
        Assert.True(found);
        Assert.NotNull(handler);
        Assert.Equal(42, (int)handler!.Parse(42)!);
    }

    [Fact]
    public void RegisterTypeHandler_WithNullFromDb_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<int>(null!, x => x.ToString()))
        );
    }

    [Fact]
    public void RegisterTypeHandler_WithNullToDb_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<int>(x => x is int i ? i : 0, null!))
        );
    }

    #endregion

    #region TypeHandler<T> Registration

    [Fact]
    public void RegisterTypeHandler_WithTypedHandler_RegistersSuccessfully()
    {
        // Arrange
        var handler = new TestTypeHandler();

        // Act
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler(handler));

        // Assert - the typed handler is retrievable and parses as registered
        bool found = TypeHandlerRegistry.TryGetHandler(typeof(int), out var retrieved);
        Assert.True(found);
        Assert.NotNull(retrieved);
        Assert.Equal(7, (int)retrieved!.Parse(7)!);
    }

    [Fact]
    public void RegisterTypeHandler_WithNullHandler_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<int>(null!))
        );
    }

    #endregion

    #region RemoveTypeHandler

    [Fact]
    public void RemoveTypeHandler_AfterRegistration_ReturnsTrue()
    {
        // Arrange
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<int>(
            fromDb: x => 0,
            toDb: x => null
        ));

        // Act
        bool removed = TypeHandlerRegistry.Remove<int>();

        // Assert
        Assert.True(removed);
    }

    [Fact]
    public void RemoveTypeHandler_WithoutPriorRegistration_ReturnsFalse()
    {
        // Act
        bool removed = TypeHandlerRegistry.Remove<Guid>();

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public void RemoveTypeHandler_TwiceInARow_ReturnsFalseOnSecond()
    {
        // Arrange
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<int>(
            fromDb: x => 0,
            toDb: x => null
        ));
        TypeHandlerRegistry.Remove<int>();

        // Act
        bool removed = TypeHandlerRegistry.Remove<int>();

        // Assert
        Assert.False(removed);
    }

    #endregion

    #region Reset Clears Handlers

    [Fact]
    public void Reset_ClearsAllRegisteredHandlers()
    {
        // Arrange
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<int>(fromDb: x => 0, toDb: x => null));
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<string>(fromDb: x => "", toDb: x => null));
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<Guid>(fromDb: x => Guid.Empty, toDb: x => null));

        // Reset() also nulls JauntyConfig.InterceptorPipeline, a process-wide static shared with
        // the "Logging Extensions" collection (running concurrently as a different xunit
        // collection) - capture and clear it atomically (AUD-R7) so an interceptor registered by
        // that collection between a separate capture-then-Reset() pair can't be silently dropped.
        var interceptorsBeforeReset = JauntyConfig.CaptureAndClearInterceptors();

        // Act — Reset() is the API under test; restore reflection mapping afterwards
        // so other concurrently-running test collections are not affected.
        JauntyConfig.Reset();
        TestInitializer.Initialize();
        if (interceptorsBeforeReset is { Length: > 0 })
            JauntyConfig.AddInterceptors(interceptorsBeforeReset);

        // Assert - verify removal works after reset (i.e., nothing is registered)
        Assert.False(TypeHandlerRegistry.Remove<int>());
        Assert.False(TypeHandlerRegistry.Remove<string>());
        Assert.False(TypeHandlerRegistry.Remove<Guid>());
    }

    #endregion

    #region DefaultEnumStorage

    [Fact]
    public void DefaultEnumStorage_InitiallyNumeric()
    {
        // Act & Assert
        Assert.Equal(EnumStorage.Numeric, JauntyConfig.DefaultEnumStorage);
    }

    [Fact]
    public void DefaultEnumStorage_CanBeSetToString()
    {
        // Arrange & Act
        JauntyConfig.Reconfigure(jc => jc.DefaultEnumStorage = EnumStorage.String);

        // Assert
        Assert.Equal(EnumStorage.String, JauntyConfig.DefaultEnumStorage);
    }

    [Fact]
    public void DefaultEnumStorage_ResetRestoresToNumeric()
    {
        // Arrange
        JauntyConfig.Reconfigure(jc => jc.DefaultEnumStorage = EnumStorage.String);

        // Reset() also nulls JauntyConfig.InterceptorPipeline, a process-wide static shared with
        // the "Logging Extensions" collection (running concurrently as a different xunit
        // collection) - capture and clear it atomically (AUD-R7) so an interceptor registered by
        // that collection between a separate capture-then-Reset() pair can't be silently dropped.
        var interceptorsBeforeReset = JauntyConfig.CaptureAndClearInterceptors();

        // Act — Reset() is the API under test; restore reflection mapping afterwards
        // so other concurrently-running test collections are not affected.
        JauntyConfig.Reset();
        TestInitializer.Initialize();
        if (interceptorsBeforeReset is { Length: > 0 })
            JauntyConfig.AddInterceptors(interceptorsBeforeReset);

        // Assert
        Assert.Equal(EnumStorage.Numeric, JauntyConfig.DefaultEnumStorage);
    }

    #endregion

    #region Replace path and TryGetHandler

    [Fact]
    public void RegisterTypeHandler_OverExisting_ReplacesHandler()
    {
        // Register first handler
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<string>(
            fromDb: v => "FIRST:" + v,
            toDb: v => v));

        // Register second handler for same type — should replace, not accumulate
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<string>(
            fromDb: v => "SECOND:" + v,
            toDb: v => v));

        // The new handler must be active
        bool found = TypeHandlerRegistry.TryGetHandler(typeof(string), out var handler);
        Assert.True(found);
        Assert.NotNull(handler);

        var result = handler!.Parse("x");
        Assert.StartsWith("SECOND:", result?.ToString() ?? string.Empty);
    }

    [Fact]
    public void RegisterTypeHandler_ClassBased_ThenReplaceWithDelegate_ReplacesHandler()
    {
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler(new UpperCaseStringHandler()));

        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<string>(
            fromDb: v => "DELEGATE:" + v,
            toDb: v => v));

        bool found = TypeHandlerRegistry.TryGetHandler(typeof(string), out var handler);
        Assert.True(found);
        var result = handler!.Parse("z");
        Assert.StartsWith("DELEGATE:", result?.ToString() ?? string.Empty);
    }

    [Fact]
    public void TryGetHandler_AfterRegister_FindsHandler()
    {
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<string>(
            fromDb: v => v?.ToString() ?? string.Empty,
            toDb: v => v));

        bool found = TypeHandlerRegistry.TryGetHandler(typeof(string), out var handler);

        Assert.True(found);
        Assert.NotNull(handler);
    }

    [Fact]
    public void TryGetHandler_AfterRemove_DoesNotFindHandler()
    {
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<Guid>(
            fromDb: v => Guid.Empty,
            toDb: v => v.ToString()));
        TypeHandlerRegistry.Remove<Guid>();

        bool found = TypeHandlerRegistry.TryGetHandler(typeof(Guid), out var handler);

        Assert.False(found);
        Assert.Null(handler);
    }

    [Fact]
    public void HasHandlers_AfterRegister_ReturnsTrue()
    {
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<string>(
            fromDb: v => v?.ToString() ?? string.Empty,
            toDb: v => v));

        Assert.True(TypeHandlerRegistry.HasHandlers);
    }

    #endregion

    #region Concurrent Register/Remove (Finding: _handlerCount race)

    [Fact]
    public async Task HasHandlers_DuringConcurrentRegisterRemoveOfOtherTypes_NeverFalselyReportsEmpty()
    {
        // A witness handler stays registered for the whole test — HasHandlers must never
        // read false while this is registered, even under heavy concurrent Register/Remove
        // churn on a different type (regression for the non-atomic _handlerCount race).
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<Guid>(fromDb: v => Guid.Empty, toDb: v => v.ToString()));
        try
        {
            const int iterations = 500;
            int threadCount = Math.Max(4, Environment.ProcessorCount);
            var tasks = new Task[threadCount];
            int falseNegatives = 0;

            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<string>(fromDb: v => v?.ToString() ?? string.Empty, toDb: v => v));
                        if (!TypeHandlerRegistry.HasHandlers)
                            Interlocked.Increment(ref falseNegatives);

                        TypeHandlerRegistry.Remove<string>();
                        if (!TypeHandlerRegistry.HasHandlers)
                            Interlocked.Increment(ref falseNegatives);
                    }
                });
            }

            await Task.WhenAll(tasks);

            Assert.Equal(0, falseNegatives);
            Assert.True(TypeHandlerRegistry.HasHandlers);
        }
        finally
        {
            TypeHandlerRegistry.Remove<Guid>();
            TypeHandlerRegistry.Remove<string>();
        }
    }

    #endregion

    private class UpperCaseStringHandler : TypeHandler<string>
    {
        public override string Parse(object? dbValue) =>
            dbValue?.ToString()?.ToUpperInvariant() ?? string.Empty;

        public override object? ToDbValue(string? value) => value?.ToLowerInvariant();
    }

    #region Nullable keying

    [Fact]
    public void Register_NullableValueType_IsKeyedOnTheUnderlyingType()
    {
        var handler = new NullReturningHandler();
        TypeHandlerRegistry.Register<Guid?>(handler);
        try
        {
            Assert.True(TypeHandlerRegistry.TryGetHandler(typeof(Guid), out ITypeHandler? found));
            Assert.Same(handler, found);
            Assert.False(TypeHandlerRegistry.TryGetHandler(typeof(Guid?), out _));
        }
        finally
        {
            Assert.True(TypeHandlerRegistry.Remove<Guid?>());
        }

        Assert.False(TypeHandlerRegistry.TryGetHandler(typeof(Guid), out _));
    }

    [Fact]
    public void RegisterUnderlying_ThenRemoveNullable_RemovesTheSameEntry()
    {
        TypeHandlerRegistry.Register<Guid>(new NullReturningHandler());

        Assert.True(TypeHandlerRegistry.Remove<Guid?>());
        Assert.False(TypeHandlerRegistry.TryGetHandler(typeof(Guid), out _));
    }

    private sealed class NullReturningHandler : ITypeHandler
    {
        public object? Parse(object? dbValue) => null;
        public object? ToDbValue(object? value) => null;
    }

    #endregion

    // Helper for testing
    private class TestTypeHandler : TypeHandler<int>
    {
        public override int Parse(object? dbValue)
        {
            if (dbValue is null)
                return 0;
            if (dbValue is int i)
                return i;
            throw new InvalidOperationException("Expected int");
        }

        public override object? ToDbValue(int value)
        {
            return value.ToString();
        }
    }
}
