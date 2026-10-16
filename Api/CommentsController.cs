using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.Comments.Models;
using Jellyfin.Plugin.Comments.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Comments.Api;

/// <summary>
/// REST API for the per-media comment section. All endpoints require an authenticated
/// Jellyfin user.
/// </summary>
[ApiController]
[Authorize]
[Route("Comments")]
public class CommentsController : ControllerBase
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    private readonly ICommentService _service;
    private readonly IAuthorizationContext _authorizationContext;
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommentsController"/> class.
    /// </summary>
    public CommentsController(
        ICommentService service,
        IAuthorizationContext authorizationContext,
        IUserManager userManager)
    {
        _service = service;
        _authorizationContext = authorizationContext;
        _userManager = userManager;
    }

    /// <summary>
    /// Gets a page of top-level comments (with nested replies) for a media item.
    /// </summary>
    /// <param name="itemId">The media item id.</param>
    /// <param name="startIndex">Zero-based index of the first top-level comment to return.</param>
    /// <param name="limit">Number of top-level comments to return (1-200).</param>
    /// <returns>The comment list.</returns>
    [HttpGet("{itemId:guid}")]
    public async Task<ActionResult<CommentListDto>> GetComments(
        [FromRoute] Guid itemId,
        [FromQuery] int? startIndex,
        [FromQuery] int? limit)
    {
        var safeStart = Math.Max(0, startIndex ?? 0);
        var safeLimit = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);

        try
        {
            var requester = await GetRequesterAsync().ConfigureAwait(false);
            return Ok(_service.GetComments(itemId, safeStart, safeLimit, requester.UserId));
        }
        catch (CommentException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// Gets the total number of comments (including replies) for a media item.
    /// </summary>
    /// <param name="itemId">The media item id.</param>
    /// <returns>The comment count.</returns>
    [HttpGet("{itemId:guid}/Count")]
    public async Task<ActionResult<CommentCountDto>> GetCommentCount([FromRoute] Guid itemId)
    {
        try
        {
            await GetRequesterAsync().ConfigureAwait(false);
            return Ok(_service.GetCount(itemId));
        }
        catch (CommentException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// Creates a new comment (or reply).
    /// </summary>
    /// <param name="request">The comment to create.</param>
    /// <returns>The created comment.</returns>
    [HttpPost]
    public async Task<ActionResult<CommentDto>> CreateComment([FromBody] CreateCommentRequest request)
    {
        try
        {
            var requester = await GetRequesterAsync().ConfigureAwait(false);
            var dto = _service.CreateComment(request, requester.UserId, requester.Username);
            return StatusCode(201, dto);
        }
        catch (CommentException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// Edits an existing comment. Only the author (or an administrator) may do this.
    /// </summary>
    /// <param name="commentId">The comment id.</param>
    /// <param name="request">The new text.</param>
    /// <returns>The updated comment.</returns>
    [HttpPut("{commentId:guid}")]
    public async Task<ActionResult<CommentDto>> UpdateComment([FromRoute] Guid commentId, [FromBody] UpdateCommentRequest request)
    {
        try
        {
            var requester = await GetRequesterAsync().ConfigureAwait(false);
            var dto = _service.UpdateComment(commentId, request.Text, requester.UserId, requester.IsAdmin);
            return Ok(dto);
        }
        catch (CommentException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// Deletes a comment (soft delete). Only the author (or an administrator) may do this.
    /// </summary>
    /// <param name="commentId">The comment id.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{commentId:guid}")]
    public async Task<ActionResult> DeleteComment([FromRoute] Guid commentId)
    {
        try
        {
            var requester = await GetRequesterAsync().ConfigureAwait(false);
            _service.DeleteComment(commentId, requester.UserId, requester.IsAdmin);
            return NoContent();
        }
        catch (CommentException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// Toggles a reaction on a comment. Adding the same reaction again removes it.
    /// </summary>
    /// <param name="commentId">The comment id.</param>
    /// <param name="request">The reaction type (e.g. "like" or an emoji).</param>
    /// <returns>The comment's updated reaction state.</returns>
    [HttpPut("{commentId:guid}/Reaction")]
    public async Task<ActionResult<ReactionStateDto>> ToggleReaction([FromRoute] Guid commentId, [FromBody] ReactionRequest request)
    {
        try
        {
            var requester = await GetRequesterAsync().ConfigureAwait(false);
            var dto = _service.ToggleReaction(commentId, request.Type, requester.UserId);
            return Ok(dto);
        }
        catch (CommentException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    private async Task<(Guid UserId, string Username, bool IsAdmin)> GetRequesterAsync()
    {
        var authInfo = await _authorizationContext.GetAuthorizationInfo(HttpContext).ConfigureAwait(false);
        if (!authInfo.IsAuthenticated || authInfo.UserId == Guid.Empty)
        {
            throw new CommentException(401, "A signed-in Jellyfin user is required.");
        }

        var user = authInfo.User ?? _userManager.GetUserById(authInfo.UserId);
        if (user is null)
        {
            throw new CommentException(401, "A signed-in Jellyfin user is required.");
        }

        var isAdmin = user.Permissions.Any(p => p.Kind == PermissionKind.IsAdministrator && p.Value);
        return (user.Id, user.Username, isAdmin);
    }
}
