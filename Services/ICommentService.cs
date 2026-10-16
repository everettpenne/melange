using System;
using Jellyfin.Plugin.Comments.Models;

namespace Jellyfin.Plugin.Comments.Services;

/// <summary>
/// The public contract for the comment section, implemented by the internal
/// <see cref="CommentService"/>. Controllers depend on this interface so the
/// implementation and its data-access details stay internal to the plugin.
/// </summary>
public interface ICommentService
{
    /// <summary>
    /// Gets a page of top-level comments (with nested replies) for a media item.
    /// </summary>
    CommentListDto GetComments(Guid itemId, int startIndex, int limit, Guid requesterId);

    /// <summary>
    /// Gets the total comment count (including replies) for a media item.
    /// </summary>
    CommentCountDto GetCount(Guid itemId);

    /// <summary>
    /// Creates a comment or reply.
    /// </summary>
    CommentDto CreateComment(CreateCommentRequest request, Guid userId, string username);

    /// <summary>
    /// Updates a comment's text.
    /// </summary>
    CommentDto UpdateComment(Guid commentId, string text, Guid userId, bool isAdmin);

    /// <summary>
    /// Soft-deletes a comment.
    /// </summary>
    void DeleteComment(Guid commentId, Guid userId, bool isAdmin);

    /// <summary>
    /// Toggles a reaction on a comment and returns the updated reaction state.
    /// </summary>
    ReactionStateDto ToggleReaction(Guid commentId, string type, Guid userId);
}
