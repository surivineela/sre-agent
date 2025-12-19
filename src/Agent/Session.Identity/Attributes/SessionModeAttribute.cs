using System;

namespace Session.Identity.Attributes;

/// <summary>
/// Specifies which session modes a controller should be available in.
/// </summary>
[Flags]
public enum SessionMode
{
    /// <summary>
    /// Identity Provider mode - runs token and certificate services.
    /// </summary>
    IdentityProvider = 1,

    /// <summary>
    /// Proxy mode - runs shell and MCP proxy services.
    /// </summary>
    Proxy = 2,
}

/// <summary>
/// Attribute to specify which session mode(s) a controller should be included in.
/// Controllers without this attribute will be included in all modes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class SessionModeAttribute : Attribute
{
    public SessionMode Mode { get; }

    public SessionModeAttribute(SessionMode mode)
    {
        Mode = mode;
    }
}
