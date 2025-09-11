using System;
using System.Collections.Generic;
using Agent.Data.Tools;
using Agent.Plugins.Tools;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

internal static class Registries
{
    // ─── Tools ─────────────────────────────────────────────────────
    public static readonly IReadOnlyDictionary<string, Type> ToolTypes =
        new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            //  discriminator      concrete C# type
            ["KustoTool"] = typeof(KustoToolDefinition),
            ["LinkTool"] = typeof(LinkToolDefinition),

            // … add more
        };

    // ─── Connectors ────────────────────────────────────────────────
    public static readonly IReadOnlyDictionary<string, Type> ConnectorTypes =
        new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["Kusto"] = typeof(KustoConnector),

            // … add more
        };
}

internal sealed class TypeDiscriminatorNodeDeserializer<TBase> : INodeDeserializer
{
    private readonly string _discKey;
    private readonly IReadOnlyDictionary<string, Type> _typeMap;

    public TypeDiscriminatorNodeDeserializer(
        string discriminatorKey,
        IReadOnlyDictionary<string, Type> typeMap)
    {
        _discKey = discriminatorKey;
        _typeMap = typeMap;
    }

    public bool Deserialize(
        IParser parser,
        Type expectedType,
        Func<IParser, Type, object?> nestedObjectDeserializer,
        out object? value,
        ObjectDeserializer rootDeserializer)
    {
        // Defer if this node isn't our TBase
        if (expectedType != typeof(TBase))
        {
            value = null;
            return false;
        }

        // ── Buffer mapping ───────────────────────────────────────────────────
        var events = new List<ParsingEvent> { (ParsingEvent)parser.Current! };
        int depth = 1;
        string? disc = null;

        while (depth > 0 && parser.MoveNext())
        {
            var ev = (ParsingEvent)parser.Current!;
            events.Add(ev);

            if (ev is MappingStart) depth++;
            else if (ev is MappingEnd) depth--;

            if (ev is Scalar key &&
                key.Value.Equals(_discKey, StringComparison.OrdinalIgnoreCase) &&
                parser.MoveNext())
            {
                var val = (Scalar)parser.Current!;
                events.Add(val);
                disc ??= val.Value;
            }
        }

        if (disc is null)
        { value = null; return false; }

        //throw new InvalidOperationException(
        //        $"Missing '{_discKey}' in {typeof(TBase).Name} node.");

        if (!_typeMap.TryGetValue(disc, out var concrete))
        { value = null; return false; }

        //throw new InvalidOperationException(
        //        $"Unknown {typeof(TBase).Name} subtype '{disc}'.");

        // ── Replay ───────────────────────────────────────────────────────────
        var valres = nestedObjectDeserializer(new ReplayParser(events), concrete);
        if (valres != null)
            value = (TBase)valres;
        else
            value = null;
        return true;
    }
}

internal static class YamlBuilderExtensions
{
    public static DeserializerBuilder WithPolymorphic<TBase>(
        this DeserializerBuilder b,
        string discriminatorKey,
        IReadOnlyDictionary<string, Type> map)
        => b.WithNodeDeserializer(
               new TypeDiscriminatorNodeDeserializer<TBase>(discriminatorKey, map),
               s => s.OnTop());
}

internal sealed class ReplayParser : IParser
{
    private readonly IEnumerator<ParsingEvent> _events;

    public ReplayParser(IEnumerable<ParsingEvent> events)
    {
        _events = events.GetEnumerator();
        MoveNext();                            // prime
    }

    public ParsingEvent Current { get; private set; } = default!;

    public bool MoveNext()
    {
        if (_events.MoveNext())
        {
            Current = _events.Current!;
            return true;
        }
        return false;
    }
}