using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Comments.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        MaxCommentLength = 10000;
        AllowReplies = true;
        AllowReactions = true;
        AllowTimestampedComments = true;
    }

    /// <summary>
    /// Gets or sets the maximum number of characters allowed in a single comment.
    /// </summary>
    public int MaxCommentLength { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether threaded replies are allowed.
    /// </summary>
    public bool AllowReplies { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether reactions (likes/emojis) are allowed.
    /// </summary>
    public bool AllowReactions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether comments may reference a playback timestamp.
    /// </summary>
    public bool AllowTimestampedComments { get; set; }
}
