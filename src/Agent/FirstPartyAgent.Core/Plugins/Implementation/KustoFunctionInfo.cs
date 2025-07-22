// File: KustoFunction.cs
public class KustoFunctionInfo
{
    public string Name { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public string DocString { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;

    public string ToFunctionSignature() =>
        string.IsNullOrWhiteSpace(Parameters)
        ? $"{Name}()"
        : $"{Name}({Parameters})";
}
