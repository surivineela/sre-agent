//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Web.Json;

[JsonConverter(typeof(SettableJsonConverter))]
public struct Settable<T>
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

    public void ApplyTo(Action<T?> apply)
    {
        if (IsSet)
        {
            apply(Value);
        }
    }

    public static implicit operator Settable<T>(T? value) => new Settable<T>(value);

    public static implicit operator T?(Settable<T> value) => value.Value;

    public override string? ToString() => Value != null ? Value.ToString() : string.Empty;
}
