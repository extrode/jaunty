using System.Reflection;
using System.Reflection.Emit;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Extensions.Reflection;
using Extrode.Jaunty.Internals.Entity;

namespace Extrode.Jaunty.Tests.Unit.Extensions.Reflection;

public class MetadataBuilderMutantTests
{
    public class BaseShape
    {
        public int A { get; set; }
        public int X { get; set; }
    }

    public class DerivedShape : BaseShape
    {
        public new string A { get; set; } = "";
        public int B { get; set; }
    }

    public class IndexedShape
    {
        public int First { get; set; }
        public int this[int index] => index;
        public string this[string key] => key;
        public int Last { get; set; }
    }

    public class UnrelatedShape
    {
        public int A { get; set; }
    }

    private static PropertyInfo Prop(Type type, string name) =>
        type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!;

    private static PropertyInfo[] MostDerived(PropertyInfo[] input) =>
        (PropertyInfo[])typeof(MetadataBuilder)
            .GetMethod("MostDerivedPerName", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [input])!;

    private static bool IsMoreDerived(PropertyInfo candidate, PropertyInfo incumbent) =>
        (bool)typeof(MetadataBuilder)
            .GetMethod("IsMoreDerived", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [candidate, incumbent])!;

    [Fact]
    public void ADerivedPropertyReplacesTheBaseOneInPlace()
    {
        PropertyInfo[] input = [Prop(typeof(BaseShape), "X"), Prop(typeof(BaseShape), "A"), Prop(typeof(DerivedShape), "A")];

        PropertyInfo[] result = MostDerived(input);

        Assert.Equal([Prop(typeof(BaseShape), "X"), Prop(typeof(DerivedShape), "A")], result);
    }

    [Fact]
    public void ABasePropertyAfterTheDerivedOneIsDropped()
    {
        PropertyInfo[] input = [Prop(typeof(DerivedShape), "A"), Prop(typeof(BaseShape), "A")];

        Assert.Equal([Prop(typeof(DerivedShape), "A")], MostDerived(input));
    }

    [Fact]
    public void WhenNothingIsDeduped_TheSameArrayComesBackInOrder()
    {
        PropertyInfo[] input = [Prop(typeof(BaseShape), "A"), Prop(typeof(DerivedShape), "B")];

        Assert.Same(input, MostDerived(input));
    }

    [Fact]
    public void ANewNameNeverDisplacesAnEarlierPropertyOfABaseType()
    {
        PropertyInfo[] input =
        [
            Prop(typeof(BaseShape), "A"),
            Prop(typeof(BaseShape), "X"),
            Prop(typeof(DerivedShape), "B"),
            Prop(typeof(DerivedShape), "A"),
        ];

        Assert.Equal([Prop(typeof(DerivedShape), "A"), Prop(typeof(BaseShape), "X"), Prop(typeof(DerivedShape), "B")], MostDerived(input));
    }

    [Fact]
    public void IndexersPassThroughUntouched()
    {
        PropertyInfo[] input = typeof(IndexedShape).GetProperties(BindingFlags.Instance | BindingFlags.Public);

        PropertyInfo[] result = MostDerived(input);

        Assert.Same(input, result);
        Assert.Equal(2, result.Count(p => p.GetIndexParameters().Length > 0));
    }

    [Fact]
    public void IndexersSurviveADedupOfTheirNeighbours()
    {
        PropertyInfo[] input =
        [
            Prop(typeof(BaseShape), "A"),
            typeof(IndexedShape).GetProperties().Where(p => p.GetIndexParameters().Length > 0).First(),
            Prop(typeof(DerivedShape), "A"),
            typeof(IndexedShape).GetProperties().Where(p => p.GetIndexParameters().Length > 0).Last(),
        ];

        PropertyInfo[] result = MostDerived(input);

        Assert.Equal([Prop(typeof(DerivedShape), "A"), input[1], input[3]], result);
    }

    [Fact]
    public void IsMoreDerived_IsTrueOnlyForADerivedCandidate()
    {
        PropertyInfo baseA = Prop(typeof(BaseShape), "A");
        PropertyInfo derivedA = Prop(typeof(DerivedShape), "A");

        Assert.True(IsMoreDerived(derivedA, baseA));
        Assert.False(IsMoreDerived(baseA, derivedA));
        Assert.False(IsMoreDerived(baseA, baseA));
        Assert.False(IsMoreDerived(Prop(typeof(UnrelatedShape), "A"), baseA));
    }

    private static Type EmitLookAlike(ModuleBuilder module, string fullName, bool positional, params (string Name, Type Type)[] properties)
    {
        TypeBuilder builder = module.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Class, typeof(Attribute));
        builder.DefineDefaultConstructor(MethodAttributes.Public);
        FieldBuilder? first = null;
        foreach ((string name, Type type) in properties)
        {
            FieldBuilder field = builder.DefineField("_" + name, type, FieldAttributes.Private);
            first ??= field;
            PropertyBuilder property = builder.DefineProperty(name, System.Reflection.PropertyAttributes.None, type, null);
            MethodBuilder getter = builder.DefineMethod("get_" + name, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, type, Type.EmptyTypes);
            ILGenerator get = getter.GetILGenerator();
            get.Emit(OpCodes.Ldarg_0);
            get.Emit(OpCodes.Ldfld, field);
            get.Emit(OpCodes.Ret);
            MethodBuilder setter = builder.DefineMethod("set_" + name, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, [type]);
            ILGenerator set = setter.GetILGenerator();
            set.Emit(OpCodes.Ldarg_0);
            set.Emit(OpCodes.Ldarg_1);
            set.Emit(OpCodes.Stfld, field);
            set.Emit(OpCodes.Ret);
            property.SetGetMethod(getter);
            property.SetSetMethod(setter);
        }
        if (positional)
        {
            ConstructorBuilder ctor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [first!.FieldType]);
            ILGenerator il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)!);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, first);
            il.Emit(OpCodes.Ret);
        }
        return builder.CreateType();
    }

    private static CustomAttributeBuilder Attribute(Type attribute, bool positional, object? positionalValue, params (string Name, object Value)[] named)
    {
        PropertyInfo[] props = named.Select(n => attribute.GetProperty(n.Name)!).ToArray();
        ConstructorInfo ctor = positional
            ? attribute.GetConstructors().Single(c => c.GetParameters().Length == 1)
            : attribute.GetConstructor(Type.EmptyTypes)!;
        return new CustomAttributeBuilder(ctor, positional ? [positionalValue!] : [], props, named.Select(n => n.Value).ToArray());
    }

    private static EntityMetadata BuildLookAlikeEntity(bool positional)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("LookAlikes_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("main");

        Type table = EmitLookAlike(module, "System.ComponentModel.DataAnnotations.Schema.TableAttribute", positional, ("Name", typeof(string)), ("Schema", typeof(string)));
        Type column = EmitLookAlike(module, "System.ComponentModel.DataAnnotations.Schema.ColumnAttribute", positional, ("Name", typeof(string)));
        Type generated = EmitLookAlike(module, "System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedAttribute", positional, ("DatabaseGeneratedOption", typeof(int)));

        TypeBuilder entity = module.DefineType("LookAlikeEntity", TypeAttributes.Public | TypeAttributes.Class);
        entity.DefineDefaultConstructor(MethodAttributes.Public);
        entity.SetCustomAttribute(positional
            ? Attribute(table, true, "named_table", ("Name", "wrong_table"), ("Schema", "named_schema"))
            : Attribute(table, false, null, ("Name", "named_table"), ("Schema", "named_schema")));

        void AddProperty(string name, Type type, CustomAttributeBuilder? attribute)
        {
            FieldBuilder field = entity.DefineField("_" + name, type, FieldAttributes.Private);
            PropertyBuilder property = entity.DefineProperty(name, System.Reflection.PropertyAttributes.None, type, null);
            MethodBuilder getter = entity.DefineMethod("get_" + name, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, type, Type.EmptyTypes);
            ILGenerator get = getter.GetILGenerator();
            get.Emit(OpCodes.Ldarg_0);
            get.Emit(OpCodes.Ldfld, field);
            get.Emit(OpCodes.Ret);
            MethodBuilder setter = entity.DefineMethod("set_" + name, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, [type]);
            ILGenerator set = setter.GetILGenerator();
            set.Emit(OpCodes.Ldarg_0);
            set.Emit(OpCodes.Ldarg_1);
            set.Emit(OpCodes.Stfld, field);
            set.Emit(OpCodes.Ret);
            property.SetGetMethod(getter);
            property.SetSetMethod(setter);
            if (attribute is not null) property.SetCustomAttribute(attribute);
        }

        CustomAttributeBuilder Generated(int option) => positional
            ? Attribute(generated, true, option, ("DatabaseGeneratedOption", option == 1 ? 2 : 1))
            : Attribute(generated, false, null, ("DatabaseGeneratedOption", option));

        AddProperty("Id", typeof(int), Generated(1));
        AddProperty("Label", typeof(string), positional ? Attribute(column, true, "named_label", ("Name", "wrong_label")) : Attribute(column, false, null, ("Name", "named_label")));
        AddProperty("Stamp", typeof(int), Generated(2));

        Type built = entity.CreateType();
        return (EntityMetadata)typeof(MetadataBuilder).GetMethod("Build")!.MakeGenericMethod(built).Invoke(null, null)!;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LookAlikeAttributes_AreReadByConstructorOrNamedArgument(bool positional)
    {
        EntityMetadata metadata = BuildLookAlikeEntity(positional);

        Assert.Equal("named_table", metadata.TableName);
        Assert.Equal("named_schema", metadata.SchemaName);
        Assert.Equal("named_label", metadata.Columns.Single(c => c.PropertyName == "Label").ColumnName);
        Assert.True(metadata.Columns.Single(c => c.PropertyName == "Id").IsIdentity);
        Assert.True(metadata.Columns.Single(c => c.PropertyName == "Stamp").IsComputed);
    }
}
