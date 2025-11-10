//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Web.Json;

[JsonConverter(typeof(SettableJsonConverter))]
public readonly struct Settable<T>
{
    public Settable()
    {
        Value = default;
        IsSet = false;
    }

    public Settable(T? value)
    {
        Value = value;
        IsSet = true;
    }

    public bool IsSet { get; }

    public T? Value { get; }

    public readonly void ApplyTo(Action<T?> apply)
    {
        if (IsSet)
        {
            apply(Value);
        }
    }

    public static implicit operator Settable<T>(T? value) => new(value);

    public static implicit operator T?(Settable<T> value) => value.Value;

    public override readonly string? ToString() => Value != null ? Value.ToString() : string.Empty;
}
