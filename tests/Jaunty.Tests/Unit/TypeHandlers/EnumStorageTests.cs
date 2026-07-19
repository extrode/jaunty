using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;

namespace Jaunty.Tests.Unit.TypeHandlers;

/// <summary>
/// Unit tests for enum storage strategy configuration and attribute handling.
/// </summary>
/// <remarks>
/// Shares the "Type Handler Operations" collection with
/// <see cref="Jaunty.Tests.TypeHandlers.TypeHandlerRegistryTests"/>,
/// <see cref="Jaunty.Tests.Integration.TypeHandlers.TypeHandlerRoundTripTests"/>, and
/// <see cref="Jaunty.Tests.ParameterBinderTests"/> — all mutate the same process-wide
/// <see cref="JauntyConfig.DefaultEnumStorage"/>/type-handler registry static state and must run
/// serialized against each other.
/// </remarks>
[Collection("Type Handler Operations")]
public class EnumStorageTests : IDisposable
{
    public void Dispose()
    {
        // Only restore the specific state this test class mutates.
        // Do NOT call JauntyConfig.Reset() — it wipes ReflectionMapperResolver,
        // causing cross-test mapper failures when running in parallel.
        JauntyConfig.DefaultEnumStorage = EnumStorage.Numeric;
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

        // Reset() also nulls JauntyConfig.InterceptorPipeline, a process-wide static shared with
        // the "Logging Extensions" collection (running concurrently as a different xunit
        // collection) - capture and clear it atomically (AUD-R7) so an interceptor registered by
        // that collection between a separate capture-then-Reset() pair can't be silently dropped.
        var interceptorsBeforeReset = JauntyConfig.CaptureAndClearInterceptors();

        // Act — Reset() is the API under test here; restore reflection mapping afterwards
        // so other concurrently-running test collections are not affected.
        JauntyConfig.Reset();
        JauntyReflectionExtensions.UseReflectionMapping();
        if (interceptorsBeforeReset is { Length: > 0 })
            JauntyConfig.AddInterceptors(interceptorsBeforeReset);

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
