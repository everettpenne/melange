using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Comments.Configuration;
using Jellyfin.Plugin.Comments.Data;
using Jellyfin.Plugin.Comments.Models;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Comments.Services;

/// <summary>
/// Business logic for the comment section: validation, permissions, tree building and
/// reaction handling. Persistence is delegated to <see cref="CommentRepository"/>.
/// </summary>
internal sealed class CommentService : ICommentService
{
    private static readonly TimeSpan _ticksPerSecond = TimeSpan.FromSeconds(1);

    private readonly CommentRepository _repository;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<CommentService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommentService"/> class.
    /// </summary>
    public CommentService(
        CommentRepository repository,
        ILibraryManager libraryManager,
        ILogger<CommentService> logger)
    {
        _repository = repository;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public CommentListDto GetComments(Guid itemId, int startIndex, int limit, Guid requesterId)
    {
        var rows = _repository.GetCommentsForItem(itemId);
        var reactionCounts = _repository.GetReactionCounts(itemId);
        var userReactions = _repository.GetUserReactions(itemId, requesterId);

        var dtos = rows.Select(r => ToDto(r, reactionCounts, userReactions)).ToList();
        var byId = dtos.ToDictionary(d => d.Id);

        var topLevel = new List<CommentDto>();
        foreach (var dto in dtos)
        {
            if (dto.ParentId is Guid parentId && byId.TryGetValue(parentId, out var parent))
            {
                parent.Replies.Add(dto);
            }
            else
            {
                topLevel.Add(dto);
            }
        }

        var totalComments = rows.Count(r => !r.IsDeleted);
        var page = topLevel.Skip(startIndex).Take(limit).ToList();

        return new CommentListDto
        {
            ItemId = itemId,
            TotalComments = totalComments,
            StartIndex = startIndex,
            Limit = limit,
            Comments = page
        };
    }

    public CommentCountDto GetCount(Guid itemId)
    {
        return new CommentCountDto
        {
            ItemId = itemId,
            TotalComments = _repository.GetCommentCount(itemId)
        };
    }

    public CommentDto CreateComment(CreateCommentRequest request, Guid userId, string username)
    {
        var config = GetConfig();
        var text = ValidateText(request.Text, config);

        if (_libraryManager.GetItemById(request.ItemId) is null)
        {
            throw new CommentException(404, "Media item not found.");
        }

        if (request.ParentId is Guid parentId)
        {
            if (!config.AllowReplies)
            {
                throw new CommentException(403, "Replies are disabled.");
            }

            var parent = _repository.GetComment(parentId) ?? throw new CommentException(404, "Parent comment not found.");
            if (parent.IsDeleted)
            {
                throw new CommentException(400, "Cannot reply to a deleted comment.");
            }

            if (parent.ItemId != request.ItemId)
            {
                throw new CommentException(400, "Parent comment belongs to a different media item.");
            }
        }

        if (request.TimestampTicks is long ticks)
        {
            if (!config.AllowTimestampedComments)
            {
                throw new CommentException(403, "Timestamped comments are disabled.");
            }

            if (ticks < 0)
            {
                throw new CommentException(400, "TimestampTicks cannot be negative.");
            }
        }

        _logger.LogDebug("User {UserId} posted a comment on item {ItemId}", userId, request.ItemId);
        var row = _repository.CreateComment(request.ItemId, userId, username, request.ParentId, text, request.TimestampTicks);
        return ToShallowDto(row);
    }

    public CommentDto UpdateComment(Guid commentId, string text, Guid userId, bool isAdmin)
    {
        var config = GetConfig();
        var trimmed = ValidateText(text, config);

        var existing = _repository.GetComment(commentId) ?? throw new CommentException(404, "Comment not found.");
        if (existing.IsDeleted)
        {
            throw new CommentException(400, "Cannot edit a deleted comment.");
        }

        if (existing.UserId != userId && !isAdmin)
        {
            throw new CommentException(403, "You can only edit your own comments.");
        }

        _repository.UpdateCommentText(commentId, trimmed);
        var updated = _repository.GetComment(commentId) ?? throw new CommentException(404, "Comment not found.");
        return ToShallowDto(updated);
    }

    public void DeleteComment(Guid commentId, Guid userId, bool isAdmin)
    {
        var existing = _repository.GetComment(commentId) ?? throw new CommentException(404, "Comment not found.");
        if (existing.UserId != userId && !isAdmin)
        {
            throw new CommentException(403, "You can only delete your own comments.");
        }

        _repository.SoftDeleteComment(commentId);
    }

    public ReactionStateDto ToggleReaction(Guid commentId, string type, Guid userId)
    {
        var config = GetConfig();
        if (!config.AllowReactions)
        {
            throw new CommentException(403, "Reactions are disabled.");
        }

        var normalized = type.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new CommentException(400, "Reaction type is required.");
        }

        if (normalized.Length > 64)
        {
            throw new CommentException(400, "Reaction type is too long (max 64 characters).");
        }

        var existing = _repository.GetComment(commentId) ?? throw new CommentException(404, "Comment not found.");
        if (existing.IsDeleted)
        {
            throw new CommentException(400, "Cannot react to a deleted comment.");
        }

        var counts = _repository.ToggleReaction(commentId, userId, normalized);
        var mine = _repository.GetUserReactionsForComment(commentId, userId);

        return new ReactionStateDto
        {
            CommentId = commentId,
            Reactions = counts.Select(c => new ReactionCountDto { Type = c.Type, Count = c.Count }).ToList(),
            MyReactions = mine
        };
    }

    private static string ValidateText(string text, PluginConfiguration config)
    {
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new CommentException(400, "Comment text is required.");
        }

        if (trimmed.Length > config.MaxCommentLength)
        {
            throw new CommentException(400, $"Comment text exceeds the maximum length of {config.MaxCommentLength} characters.");
        }

        return trimmed;
    }

    private static PluginConfiguration GetConfig() => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    private static CommentDto ToShallowDto(CommentRow row)
        => ToDto(row, new Dictionary<Guid, List<ReactionCount>>(), new Dictionary<Guid, List<string>>());

    private static CommentDto ToDto(
        CommentRow row,
        IReadOnlyDictionary<Guid, List<ReactionCount>> reactionCounts,
        IReadOnlyDictionary<Guid, List<string>> userReactions)
    {
        reactionCounts.TryGetValue(row.Id, out var counts);
        userReactions.TryGetValue(row.Id, out var mine);

        return new CommentDto
        {
            Id = row.Id,
            ItemId = row.ItemId,
            UserId = row.IsDeleted ? Guid.Empty : row.UserId,
            Username = row.IsDeleted ? "[deleted]" : row.Username,
            ParentId = row.ParentId,
            Text = row.IsDeleted ? "[deleted]" : row.Text,
            TimestampTicks = row.TimestampTicks,
            TimestampSeconds = row.TimestampTicks.HasValue ? row.TimestampTicks.Value / _ticksPerSecond.Ticks : null,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
            IsEdited = row.UpdatedAt is not null && !row.IsDeleted,
            IsDeleted = row.IsDeleted,
            Reactions = counts?.Select(c => new ReactionCountDto { Type = c.Type, Count = c.Count }).ToList() ?? [],
            MyReactions = mine ?? []
        };
    }
}
