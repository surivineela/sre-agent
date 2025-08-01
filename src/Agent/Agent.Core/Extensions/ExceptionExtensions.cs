#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static class ExceptionExtensions
{
    /// <summary>
    /// Checks if the exception is not an OperationCanceledException for the specified cancellation token.
    /// Useful when using a catch-all block to allow cancellation exceptions to propagate while handling other exceptions.
    /// Example:
    /// <code>
    /// catch (Exception ex) when (ex.IsNotTokenCancellation(stoppingToken))
    /// </code>
    /// </summary>
    /// <param name="exception"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static bool IsNotTokenCancellation(this Exception exception, CancellationToken cancellationToken)
    {
        return !(exception is OperationCanceledException oce && oce.CancellationToken == cancellationToken);
    }
}
