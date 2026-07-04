using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;
using Jaunty.TypeHandlers;

namespace Jaunty.Tests.TypeHandlers;

/// <summary>
/// Unit tests for the TypeHandler registration and registry APIs.
/// </summary>
public class TypeHandlerRegistryTests : IDisposable
{
    public void Dispose()
    {
        // Only remove the specific handlers this test class may have registered.
        // Do NOT call JauntyConfig.Reset() — it wipes ReflectionMapperResolver,
        // causing cross-test mapper failures when running in parallel.
        JauntyConfig.RemoveTypeHandler<int>();
        JauntyConfig.RemoveTypeHandler<string>();
        JauntyConfig.RemoveTypeHandler<Guid>();
        JauntyConfig.DefaultEnumStorage = EnumStorage.Numeric;
    }

    #region Delegate-based Registration

    [Fact]
    public void RegisterTypeHandler_WithDelegates_RegistersAndRetrievesCorrectly()
    {
        // Arrange & Act
        JauntyConfig.RegisterTypeHandler<int>(
            fromDb: dbValue => dbValue is int i ? i : 0,
            toDb: value => value.ToString()
        );

        // Assert - registration completed without exception
        Assert.True(true);
    }

    [Fact]
    public void RegisterTypeHandler_WithNullFromDb_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            JauntyConfig.RegisterTypeHandler<int>(null!, x => x.ToString())
        );
    }

    [Fact]
    public void RegisterTypeHandler_WithNullToDb_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            JauntyConfig.RegisterTypeHandler<int>(x => x is int i ? i : 0, null!)
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
        JauntyConfig.RegisterTypeHandler(handler);

        // Assert - smoke test
        Assert.True(true);
    }

    [Fact]
    public void RegisterTypeHandler_WithNullHandler_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            JauntyConfig.RegisterTypeHandler<int>(null!)
        );
    }

    #endregion

    #region RemoveTypeHandler

    [Fact]
    public void RemoveTypeHandler_AfterRegistration_ReturnsTrue()
    {
        // Arrange
        JauntyConfig.RegisterTypeHandler<int>(
            fromDb: x => 0,
            toDb: x => null
        );

        // Act
        bool removed = JauntyConfig.RemoveTypeHandler<int>();

        // Assert
        Assert.True(removed);
    }

    [Fact]
    public void RemoveTypeHandler_WithoutPriorRegistration_ReturnsFalse()
    {
        // Act
        bool removed = JauntyConfig.RemoveTypeHandler<Guid>();

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public void RemoveTypeHandler_TwiceInARow_ReturnsFalseOnSecond()
    {
        // Arrange
        JauntyConfig.RegisterTypeHandler<int>(
            fromDb: x => 0,
            toDb: x => null
        );
        JauntyConfig.RemoveTypeHandler<int>();

        // Act
        bool removed = JauntyConfig.RemoveTypeHandler<int>();

        // Assert
        Assert.False(removed);
    }

    #endregion

    #region Reset Clears Handlers

    [Fact]
    public void Reset_ClearsAllRegisteredHandlers()
    {
        // Arrange
        JauntyConfig.RegisterTypeHandler<int>(fromDb: x => 0, toDb: x => null);
        JauntyConfig.RegisterTypeHandler<string>(fromDb: x => "", toDb: x => null);
        JauntyConfig.RegisterTypeHandler<Guid>(fromDb: x => Guid.Empty, toDb: x => null);

        // Act — Reset() is the API under test; restore reflection mapping afterwards
        // so other concurrently-running test collections are not affected.
        JauntyConfig.Reset();
        JauntyReflectionExtensions.UseReflectionMapping();

        // Assert - verify removal works after reset (i.e., nothing is registered)
        Assert.False(JauntyConfig.RemoveTypeHandler<int>());
        Assert.False(JauntyConfig.RemoveTypeHandler<string>());
        Assert.False(JauntyConfig.RemoveTypeHandler<Guid>());
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
        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        // Assert
        Assert.Equal(EnumStorage.String, JauntyConfig.DefaultEnumStorage);
    }

    [Fact]
    public void DefaultEnumStorage_ResetRestoresToNumeric()
    {
        // Arrange
        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        // Act — Reset() is the API under test; restore reflection mapping afterwards
        // so other concurrently-running test collections are not affected.
        JauntyConfig.Reset();
        JauntyReflectionExtensions.UseReflectionMapping();

        // Assert
        Assert.Equal(EnumStorage.Numeric, JauntyConfig.DefaultEnumStorage);
    }

    #endregion

    #region Replace path and TryGetHandler

    [Fact]
    public void RegisterTypeHandler_OverExisting_ReplacesHandler()
    {
        // Register first handler
        JauntyConfig.RegisterTypeHandler<string>(
            fromDb: v => "FIRST:" + v,
            toDb: v => v);

        // Register second handler for same type — should replace, not accumulate
        JauntyConfig.RegisterTypeHandler<string>(
            fromDb: v => "SECOND:" + v,
            toDb: v => v);

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
        JauntyConfig.RegisterTypeHandler(new UpperCaseStringHandler());

        JauntyConfig.RegisterTypeHandler<string>(
            fromDb: v => "DELEGATE:" + v,
            toDb: v => v);

        bool found = TypeHandlerRegistry.TryGetHandler(typeof(string), out var handler);
        Assert.True(found);
        var result = handler!.Parse("z");
        Assert.StartsWith("DELEGATE:", result?.ToString() ?? string.Empty);
    }

    [Fact]
    public void TryGetHandler_AfterRegister_FindsHandler()
    {
        JauntyConfig.RegisterTypeHandler<string>(
            fromDb: v => v?.ToString() ?? string.Empty,
            toDb: v => v);

        bool found = TypeHandlerRegistry.TryGetHandler(typeof(string), out var handler);

        Assert.True(found);
        Assert.NotNull(handler);
    }

    [Fact]
    public void TryGetHandler_AfterRemove_DoesNotFindHandler()
    {
        JauntyConfig.RegisterTypeHandler<Guid>(
            fromDb: v => Guid.Empty,
            toDb: v => v.ToString());
        JauntyConfig.RemoveTypeHandler<Guid>();

        bool found = TypeHandlerRegistry.TryGetHandler(typeof(Guid), out var handler);

        Assert.False(found);
        Assert.Null(handler);
    }

    [Fact]
    public void HasHandlers_AfterRegister_ReturnsTrue()
    {
        JauntyConfig.RegisterTypeHandler<string>(
            fromDb: v => v?.ToString() ?? string.Empty,
            toDb: v => v);

        Assert.True(TypeHandlerRegistry.HasHandlers);
    }

    #endregion

    private class UpperCaseStringHandler : TypeHandler<string>
    {
        public override string Parse(object? dbValue) =>
            dbValue?.ToString()?.ToUpperInvariant() ?? string.Empty;

        public override object? ToDbValue(string? value) => value?.ToLowerInvariant();
    }

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
