using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Agent.Core;

public static class ArgumentValidation
{
    [DebuggerStepThrough]
    public static string ThrowIfNullOrEmpty(
        [NotNull] this string? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (string.IsNullOrEmpty(argument))
        {
            throw new ArgumentException($"The argument is null or empty.", paramName);
        }

        return argument;
    }

    public static T ThrowIfLessThan<T>(
        this T value,
        T other,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            value: value,
            other: other,
            paramName: paramName);

        return value;
    }

    [DebuggerStepThrough]
    public static string ThrowIfNullOrWhiteSpace(
        [NotNull] this string? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new ArgumentException($"The argument is null or only consists of whitespace characters.", paramName);
        }

        return argument;
    }

    [DebuggerStepThrough]
    public static CancellationToken ThrowIfDefault(
        this CancellationToken argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument == default)
        {
            throw new ArgumentException($"{argument} cannot be the default CancellationToken.", paramName);
        }

        return argument;
    }

    [DebuggerStepThrough]
    public static T ThrowIfNull<T>(
        [NotNull] this T? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(argument, paramName);
        return argument;
    }

    [DebuggerStepThrough]
    public static T ThrowIfNull<T>(
        [NotNull] this T? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(argument, paramName);
        return argument.Value;
    }

    [DebuggerStepThrough]
    public static string ThrowIfHasUriChars(
        this string argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument.Contains('/'))
        {
            throw new ArgumentException($"The argument contains uri characters: '/'.", paramName);
        }

        return argument;
    }
}
