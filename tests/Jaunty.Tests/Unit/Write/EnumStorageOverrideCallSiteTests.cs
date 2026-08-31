using System.Reflection;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Parameters;

using Xunit;

namespace Jaunty.Tests.Unit.Write;

/// <summary>
/// AUD-R35-011. AUD-R35-001 put the <c>[EnumStorage]</c> override on <see cref="ColumnMetadata"/>
/// so the source-generated path could carry it - <c>ColumnMetadata.Property</c> is null there, and
/// <c>GetEnumStorage(null)</c> falls through to <c>JauntyConfig.DefaultEnumStorage</c> - and then
/// swept only <c>Upsert</c> and <c>GetCore</c>. The multi-row <c>BulkInsert</c>/<c>BulkInsertAsync</c>
/// bind sites and both <c>DeleteCore</c> key binds still passed <c>Property</c> alone, so with the
/// generator referenced a string-stored enum was written as its <em>number</em> for &gt;1 row on
/// SQL Server / PostgreSQL / MySQL and as its <em>name</em> for one row or on SQLite.
/// <para>
/// These pin the mechanism and the call-site shape. They do not execute the multi-row path itself:
/// it is unreachable without a live SQL Server / PostgreSQL / MySQL <em>and</em> source-generated
/// metadata, and the only suite with the generator (Jaunty.SourceGenerator.Tests) is SQLite-only,
/// which BulkInsert explicitly excludes from the multi-row path. That gap is noted in
/// <c>work/todo.md</c>.
/// </para>
/// </summary>
public class EnumStorageOverrideCallSiteTests
{
    private enum Ticket { Open = 0, Closed = 1 }

    private sealed class Row
    {
        public Ticket State { get; set; }
    }

    // The generated-path shape: the constructor that takes getters and setters, which never
    // populates Property.
    private static ColumnMetadata GeneratedColumn(EnumStorage? storage) => new(
        propertyName: nameof(Row.State),
        propertyType: typeof(Ticket),
        columnName: "state",
        isPrimaryKey: false,
        isIdentity: false,
        isComputed: false,
        getter: static o => ((Row)o).State,
        setter: static (o, v) => ((Row)o).State = (Ticket)v!,
        enumStorageOverride: storage);

    [Fact]
    public void TheGeneratedShapeHasNoPropertyToReflectOver()
    {
        Assert.Null(GeneratedColumn(EnumStorage.String).Property);
        Assert.Equal(EnumStorage.String, GeneratedColumn(EnumStorage.String).EnumStorageOverride);
    }

    [Fact]
    public void PassingTheOverrideWritesTheName()
    {
        ColumnMetadata column = GeneratedColumn(EnumStorage.String);

        object? bound = ParameterBinder.ApplyTypeHandlerIfNeeded(Ticket.Closed, column.Property, column.EnumStorageOverride);

        Assert.Equal("Closed", bound);
    }

    [Fact]
    public void PassingOnlyThePropertyFallsBackToTheGlobalDefault()
    {
        ColumnMetadata column = GeneratedColumn(EnumStorage.String);
        EnumStorage original = JauntyConfig.DefaultEnumStorage;
        try
        {
            JauntyConfig.DefaultEnumStorage = EnumStorage.Numeric;

            object? bound = ParameterBinder.ApplyTypeHandlerIfNeeded(Ticket.Closed, column.Property);

            Assert.Equal(Ticket.Closed, bound);
        }
        finally
        {
            JauntyConfig.DefaultEnumStorage = original;
        }
    }

    [Fact]
    public void ANumericOverrideStillWritesTheEnum()
    {
        ColumnMetadata column = GeneratedColumn(EnumStorage.Numeric);
        EnumStorage original = JauntyConfig.DefaultEnumStorage;
        try
        {
            JauntyConfig.DefaultEnumStorage = EnumStorage.String;

            object? bound = ParameterBinder.ApplyTypeHandlerIfNeeded(Ticket.Closed, column.Property, column.EnumStorageOverride);

            Assert.Equal(Ticket.Closed, bound);
        }
        finally
        {
            JauntyConfig.DefaultEnumStorage = original;
        }
    }

    [Fact]
    public void NoOverrideStillHonoursTheGlobalDefault()
    {
        ColumnMetadata column = GeneratedColumn(null);
        EnumStorage original = JauntyConfig.DefaultEnumStorage;
        try
        {
            JauntyConfig.DefaultEnumStorage = EnumStorage.String;

            object? bound = ParameterBinder.ApplyTypeHandlerIfNeeded(Ticket.Closed, column.Property, column.EnumStorageOverride);

            Assert.Equal("Closed", bound);
        }
        finally
        {
            JauntyConfig.DefaultEnumStorage = original;
        }
    }

    /// <summary>
    /// The sweep itself: every <c>ApplyTypeHandlerIfNeeded</c> overload that can be handed a
    /// <see cref="ColumnMetadata"/> must take the override too, or the generated path loses it.
    /// A three-parameter signature with a defaulted third argument is what makes that easy to
    /// forget, so this pins the parameter rather than the call.
    /// </summary>
    [Fact]
    public void TheBinderStillAcceptsAnExplicitOverride()
    {
        MethodInfo method = typeof(ParameterBinder).GetMethod(
            "ApplyTypeHandlerIfNeeded",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        ParameterInfo[] parameters = method.GetParameters();

        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(EnumStorage?), parameters[2].ParameterType);
    }
}
