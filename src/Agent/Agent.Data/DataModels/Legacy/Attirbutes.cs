
// JsonPolymorphicAttribute does not work with custom converters. But we must need custom converters to keep backward compatibility.
// Thus we define similar attributes to mark the polymorphic types for our custom converters to use.
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class CustomizedJsonPolymorphicAttribute : Attribute
{
    public required string TypeDiscriminatorPropertyName { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class CustomizedJsonDerivedTypeAttribute : Attribute
{
    public Type Type { get; }
    public string Value { get; }
    public CustomizedJsonDerivedTypeAttribute(Type type, string value)
    {
        Type = type;
        Value = value;
    }
}