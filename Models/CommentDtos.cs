using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Plugin.Comments.Models;

/// <summary>
/// A single comment, with nested replies and reaction data.
/// </summary>
public class CommentDto
{
    /// <summary>
    /// Gets or sets the comment id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the id of the media item this comment belongs to.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the author's user id. <see cref="Guid.Empty"/> when deleted.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the author's username as it was when the comment was posted.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the id of the parent comment, or <c>null</c> for a top-level comment.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Gets or sets the comment text. <c>[deleted]</c> when the comment was removed.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the playback position this comment refers to, in ticks (1 second = 10,000,000 ticks), if any.
    /// </summary>
    public long? TimestampTicks { get; set; }

    /// <summary>
    /// Gets or sets the playback position in whole seconds, if any. A convenience derived from <see cref="TimestampTicks"/>.
    /// </summary>
    public long? TimestampSeconds { get; set; }

    /// <summary>
    /// Gets or sets the creation time in ISO-8601 UTC.
    /// </summary>
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last-edit time in ISO-8601 UTC, or <c>null</c> if never edited.
    /// </summary>
    public string? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the comment was edited after posting.
    /// </summary>
    public bool IsEdited { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the comment was soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the aggregated reaction counts for this comment.
    /// </summary>
    public List<ReactionCountDto> Reactions { get; set; } = [];

    /// <summary>
    /// Gets or sets the reaction types the requesting user has placed on this comment.
    /// </summary>
    public List<string> MyReactions { get; set; } = [];

    /// <summary>
    /// Gets or sets the nested replies to this comment.
    /// </summary>
    public List<CommentDto> Replies { get; set; } = [];
}

/// <summary>
/// An aggregated reaction count.
/// </summary>
public class ReactionCountDto
{
    /// <summary>
    /// Gets or sets the reaction type (e.g. "like" or an emoji).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how many users placed this reaction.
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// A page of top-level comments for a media item.
/// </summary>
public class CommentListDto
{
    /// <summary>
    /// Gets or sets the media item id.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the total number of non-deleted comments (including replies).
    /// </summary>
    public int TotalComments { get; set; }

    /// <summary>
    /// Gets or sets the requested start index.
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// Gets or sets the requested page size.
    /// </summary>
    public int Limit { get; set; }

    /// <summary>
    /// Gets or sets the page of top-level comments (replies are nested on each comment).
    /// </summary>
    public List<CommentDto> Comments { get; set; } = [];
}

/// <summary>
/// A simple comment count for a media item.
/// </summary>
public class CommentCountDto
{
    /// <summary>
    /// Gets or sets the media item id.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the total number of non-deleted comments (including replies).
    /// </summary>
    public int TotalComments { get; set; }
}

/// <summary>
/// Request body for creating a comment.
/// </summary>
public class CreateCommentRequest
{
    /// <summary>
    /// Gets or sets the id of the media item being commented on.
    /// </summary>
    [Required]
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the comment text.
    /// </summary>
    [Required]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parent comment id for a reply, if any.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Gets or sets the playback position in ticks (1 second = 10,000,000 ticks), if any.
    /// </summary>
    public long? TimestampTicks { get; set; }
}

/// <summary>
/// Request body for editing a comment.
/// </summary>
public class UpdateCommentRequest
{
    /// <summary>
    /// Gets or sets the new comment text.
    /// </summary>
    [Required]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Request body for toggling a reaction.
/// </summary>
public class ReactionRequest
{
    /// <summary>
    /// Gets or sets the reaction type (e.g. "like" or an emoji).
    /// </summary>
    [Required]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// The reaction state of a single comment after a toggle.
/// </summary>
public class ReactionStateDto
{
    /// <summary>
    /// Gets or sets the comment id.
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Gets or sets the aggregated reaction counts.
    /// </summary>
    public List<ReactionCountDto> Reactions { get; set; } = [];

    /// <summary>
    /// Gets or sets the reaction types the requesting user has placed on this comment.
    /// </summary>
    public List<string> MyReactions { get; set; } = [];
}
