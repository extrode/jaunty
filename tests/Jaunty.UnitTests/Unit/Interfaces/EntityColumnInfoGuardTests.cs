using Jaunty.Interfaces;

using Xunit;

namespace Jaunty.Tests.Unit.Interfaces;

/// <summary>
/// AUD-R35-171. <see cref="EntityColumnInfo"/> is the public transport shape a hand-written
/// <see cref="IEntityMetadataSource"/> fills in, and its constructor validated none of its five
/// non-nullable reference parameters. A null one was copied straight into <c>ColumnMetadata</c> and
/// surfaced as a <see cref="NullReferenceException"/> from inside SQL generation or parameter
/// binding, arbitrarily far from the mistake.
/// </summary>
public class EntityColumnInfoGuardTests
{
    private static readonly Func<object, object?> Getter = _ => null;

    private static readonly Action<object, object?> Setter = (_, _) => { };

    private static EntityColumnInfo Create(
        string columnName = "id",
        string propertyName = "Id",
        Type? propertyType = null,
        Func<object, object?>? getter = null,
        Action<object, object?>? setter = null) =>
        new(columnName, propertyName, false, false, false, propertyType!, getter!, setter!);

    [Fact]
    public void ANullColumnName_IsRejected() =>
        Assert.Equal("columnName",
            Assert.Throws<ArgumentNullException>(() => Create(columnName: null!, propertyType: typeof(int), getter: Getter, setter: Setter)).ParamName);

    [Fact]
    public void ANullPropertyName_IsRejected() =>
        Assert.Equal("propertyName",
            Assert.Throws<ArgumentNullException>(() => Create(propertyName: null!, propertyType: typeof(int), getter: Getter, setter: Setter)).ParamName);

    [Fact]
    public void ANullPropertyType_IsRejected() =>
        Assert.Equal("propertyType",
            Assert.Throws<ArgumentNullException>(() => Create(propertyType: null, getter: Getter, setter: Setter)).ParamName);

    [Fact]
    public void ANullGetter_IsRejected() =>
        Assert.Equal("getter",
            Assert.Throws<ArgumentNullException>(() => Create(propertyType: typeof(int), getter: null, setter: Setter)).ParamName);

    [Fact]
    public void ANullSetter_IsRejected() =>
        Assert.Equal("setter",
            Assert.Throws<ArgumentNullException>(() => Create(propertyType: typeof(int), getter: Getter, setter: null)).ParamName);

    [Fact]
    public void AWellFormedColumn_IsBuiltUnchanged()
    {
        var column = new EntityColumnInfo("order_id", "OrderId", true, true, false, typeof(int), Getter, Setter);

        Assert.Equal("order_id", column.ColumnName);
        Assert.Equal("OrderId", column.PropertyName);
        Assert.True(column.IsPrimaryKey);
        Assert.True(column.IsIdentity);
        Assert.False(column.IsComputed);
        Assert.Equal(typeof(int), column.PropertyType);
        Assert.Same(Getter, column.Getter);
        Assert.Same(Setter, column.Setter);
        Assert.Null(column.EnumStorageOverride);
    }
}
