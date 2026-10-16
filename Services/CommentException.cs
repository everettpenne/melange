using System;

namespace Jellyfin.Plugin.Comments.Services;

/// <summary>
/// Thrown for comment business-rule failures. Carries an HTTP status code so the
/// controller can translate it into a clean response without leaking internals.
/// </summary>
internal sealed class CommentException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommentException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="message">A user-facing message.</param>
    public CommentException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public int StatusCode { get; }
}
