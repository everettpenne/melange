using Jellyfin.Plugin.Comments.Data;
using Jellyfin.Plugin.Comments.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Comments;

/// <summary>
/// Registers the plugin's services with Jellyfin's dependency injection container.
/// This type must have a parameterless constructor.
/// </summary>
public class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<CommentRepository>();
        serviceCollection.AddSingleton<ICommentService, CommentService>();
    }
}
