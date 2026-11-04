using System;

namespace PieceOfCake.Services;

public sealed class HueException : Exception
{
    public HueException(string message, bool unauthorized = false, Exception? inner = null)
        : base(message, inner)
    {
        Unauthorized = unauthorized;
    }

    /// <summary>True when the stored key or certificate is no longer accepted and pairing must be redone.</summary>
    public bool Unauthorized { get; }
}
