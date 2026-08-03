using System;

using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities.ForeignAttributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ColumnAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NotMappedAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IgnoreAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class KeyAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DatabaseGeneratedAttribute(int option) : Attribute
    {
        public int Option { get; } = option;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EnumStorageAttribute(int storage) : Attribute
    {
        public int Storage { get; } = storage;
    }

    /// <summary>
    /// Derives from the DataAnnotations attribute the generator recognizes, so it must be honoured -
    /// <c>PropertyInfo.GetCustomAttribute&lt;T&gt;</c> on the reflection side matches a subclass.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DerivedColumnAttribute(string name)
        : System.ComponentModel.DataAnnotations.Schema.ColumnAttribute(name);
}

namespace Jaunty.SourceGenerator.Tests.Entities
{
    public enum GenForeignGrade
    {
        Low = 0,
        High = 1
    }

    // AUD-R35-070. Every mapping attribute below is a foreign one that happens to share a simple
    // class name with a recognized attribute. The generator matched on the simple name, so each of
    // these silently changed the generated mapping while reflection ignored it - the same
    // silent-column divergence class AUD-R34-031 fixed for [Table] alone.
    [Table("gen_foreign_attribute_entities")]
    public partial class GenForeignAttributeEntity : IMapped<GenForeignAttributeEntity>
    {
        [Key]
        [Column("entity_id")]
        public int EntityId { get; set; }

        [ForeignAttributes.Column("renamed_by_a_stranger")]
        public string Kept { get; set; } = string.Empty;

        [ForeignAttributes.NotMapped]
        public string StillMappedDespiteNotMapped { get; set; } = string.Empty;

        [ForeignAttributes.Ignore]
        public string StillMappedDespiteIgnore { get; set; } = string.Empty;

        [ForeignAttributes.Key]
        public string NotAKey { get; set; } = string.Empty;

        [ForeignAttributes.DatabaseGenerated(2)]
        public int NotComputed { get; set; }

        [ForeignAttributes.EnumStorage(1)]
        public GenForeignGrade Grade { get; set; }

        [ForeignAttributes.DerivedColumn("derived_column")]
        public string Derived { get; set; } = string.Empty;
    }

    // The recognized set, for the controls: DataAnnotations' [Key], [Column] and [NotMapped] still
    // bind, and Jaunty's [EnumStorage] still reaches the generated metadata.
    [Table("gen_recognized_attribute_entities")]
    public partial class GenRecognizedAttributeEntity : IMapped<GenRecognizedAttributeEntity>
    {
        [System.ComponentModel.DataAnnotations.Key]
        [System.ComponentModel.DataAnnotations.Schema.Column("code")]
        public string Code { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string Dropped { get; set; } = string.Empty;

        [EnumStorage(global::Jaunty.Attributes.EnumStorage.String)]
        public GenForeignGrade Grade { get; set; }
    }
}
