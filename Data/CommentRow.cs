using System;

namespace Jellyfin.Plugin.Comments.Data;

/// <summary>
/// A raw comment row read from the plugin's database.
/// </summary>
internal sealed record CommentRow(
    Guid Id,
    Guid ItemId,
    Guid UserId,
    string Username,
    Guid? ParentId,
    string Text,
    long? TimestampTicks,
    string CreatedAt,
    string? UpdatedAt,
    bool IsDeleted);

/// <summary>
/// An aggregated reaction count for a comment.
/// </summary>
internal sealed record ReactionCount(string Type, int Count);
