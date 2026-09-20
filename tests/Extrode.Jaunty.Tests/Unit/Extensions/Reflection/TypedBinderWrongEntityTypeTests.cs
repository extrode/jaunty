using System.Data;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Extensions.Reflection;
using Extrode.Jaunty.Tests.Unit.Read;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Extensions.Reflection;

/// <summary>
/// coverage-gaps-2026-09-20: <c>GetTypedUpdateBinder</c>/<c>GetTypedDeleteBinder</c>'s
/// <c>entityObj is not T</c> guard had no test reaching either — only a throwing type handler on
/// the insert path was covered elsewhere (<see cref="ThrowingTypeHandlerContractTests"/>), which
/// exercises a different branch of the same generated delegate.
/// </summary>
[Collection("Type Handler Operations")]
public class TypedBinderWrongEntityTypeTests
{
    public TypedBinderWrongEntityTypeTests() => JauntyReflectionExtensions.UseReflectionMapping();

    [Table("wrong_entity_widgets")]
    public class Widget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public class NotAWidget
    {
        public int Id { get; set; }
    }

    [Fact]
    public void InsertBinder_GivenAnObjectOfTheWrongType_ThrowsWithBothTypeNames()
    {
        Action<IDbCommand, object> bind = JauntyConfig.ReflectionInsertBinderResolver!(typeof(Widget));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => bind(new MockDbCommand("INSERT"), new NotAWidget()));

        Assert.Contains(nameof(Widget), ex.Message);
        Assert.Contains(nameof(NotAWidget), ex.Message);
    }

    [Fact]
    public void UpdateBinder_GivenAnObjectOfTheWrongType_ThrowsWithBothTypeNames()
    {
        Action<IDbCommand, object> bind = JauntyConfig.ReflectionUpdateBinderResolver!(typeof(Widget));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => bind(new MockDbCommand("UPDATE"), new NotAWidget()));

        Assert.Contains(nameof(Widget), ex.Message);
        Assert.Contains(nameof(NotAWidget), ex.Message);
    }

    [Fact]
    public void DeleteBinder_GivenAnObjectOfTheWrongType_ThrowsWithBothTypeNames()
    {
        Action<IDbCommand, object> bind = JauntyConfig.ReflectionDeleteBinderResolver!(typeof(Widget));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => bind(new MockDbCommand("DELETE"), new NotAWidget()));

        Assert.Contains(nameof(Widget), ex.Message);
        Assert.Contains(nameof(NotAWidget), ex.Message);
    }

    [Fact]
    public void UpdateBinder_GivenNull_ThrowsMentioningNull()
    {
        Action<IDbCommand, object> bind = JauntyConfig.ReflectionUpdateBinderResolver!(typeof(Widget));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => bind(new MockDbCommand("UPDATE"), null!));

        Assert.Contains("null", ex.Message);
    }

    [Fact]
    public void DeleteBinder_GivenNull_ThrowsMentioningNull()
    {
        Action<IDbCommand, object> bind = JauntyConfig.ReflectionDeleteBinderResolver!(typeof(Widget));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => bind(new MockDbCommand("DELETE"), null!));

        Assert.Contains("null", ex.Message);
    }
}
