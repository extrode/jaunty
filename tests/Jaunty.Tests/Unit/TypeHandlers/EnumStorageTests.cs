using Jaunty.Attributes;
using Jaunty.Configuration;

namespace Jaunty.Tests.TypeHandlers;

/// <summary>
/// Unit tests for enum storage strategy configuration and attribute handling.
/// </summary>
public class EnumStorageTests : IDisposable
{
    public EnumStorageTests()
    {
        JauntyConfig.Reset();
    }

    public void Dispose()
    {
        JauntyConfig.Reset();
    }

    #region Attribute Tests

    [Fact]
    public void EnumStorageAttribute_CanBeAppliedToProperty()
    {
        // Arrange
        var prop = typeof(TestEntity).GetProperty(nameof(TestEntity.StringStoredEnum));

        // Act
        var attr = prop?.GetCustomAttributes(typeof(EnumStorageAttribute), false).FirstOrDefault() as EnumStorageAttribute;

        // Assert
        Assert.NotNull(attr);
        Assert.Equal(EnumStorage.String, attr.Storage);
    }

    [Fact]
    public void EnumStorageAttribute_Defaults()
    {
        // Arrange
        var prop = typeof(TestEntity).GetProperty(nameof(TestEntity.DefaultEnum));

        // Act
        var attr = prop?.GetCustomAttributes(typeof(EnumStorageAttribute), false).FirstOrDefault() as EnumStorageAttribute;

        // Assert
        Assert.Null(attr); // No attribute on Default property
    }

    #endregion

    #region Global DefaultEnumStorage

    [Fact]
    public void DefaultEnumStorage_IsNumericByDefault()
    {
        // Assert
        Assert.Equal(EnumStorage.Numeric, JauntyConfig.DefaultEnumStorage);
    }

    [Fact]
    public void DefaultEnumStorage_CanBeChangedToString()
    {
        // Arrange & Act
        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        // Assert
        Assert.Equal(EnumStorage.String, JauntyConfig.DefaultEnumStorage);
    }

    [Fact]
    public void DefaultEnumStorage_CanBeChangedBackToNumeric()
    {
        // Arrange
        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        // Act
        JauntyConfig.DefaultEnumStorage = EnumStorage.Numeric;

        // Assert
        Assert.Equal(EnumStorage.Numeric, JauntyConfig.DefaultEnumStorage);
    }

    [Fact]
    public void DefaultEnumStorage_ResetRestores()
    {
        // Arrange
        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        // Act
        JauntyConfig.Reset();

        // Assert
        Assert.Equal(EnumStorage.Numeric, JauntyConfig.DefaultEnumStorage);
    }

    #endregion

    // Test entities
    public enum TestEnum
    {
        Pending = 0,
        Completed = 1,
        Cancelled = 2
    }

    public class TestEntity
    {
        public int Id { get; set; }

        [EnumStorage(EnumStorage.String)]
        public TestEnum StringStoredEnum { get; set; }

        public TestEnum DefaultEnum { get; set; }
    }
}
