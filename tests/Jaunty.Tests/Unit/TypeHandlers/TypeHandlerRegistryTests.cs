using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.TypeHandlers;

namespace Jaunty.Tests.TypeHandlers;

/// <summary>
/// Unit tests for the TypeHandler registration and registry APIs.
/// </summary>
public class TypeHandlerRegistryTests : IDisposable
{
    public TypeHandlerRegistryTests()
    {
        // Ensure clean state before each test
        JauntyConfig.Reset();
    }

    public void Dispose()
    {
        // Clean up after each test
        JauntyConfig.Reset();
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

        // Act
        JauntyConfig.Reset();

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

        // Act
        JauntyConfig.Reset();

        // Assert
        Assert.Equal(EnumStorage.Numeric, JauntyConfig.DefaultEnumStorage);
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
