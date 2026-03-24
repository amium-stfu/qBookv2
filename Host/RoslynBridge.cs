namespace Amium.Host;

internal static class RoslynBridge
{
    private static bool _initialized;
    private static readonly object Sync = new();

    internal static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            BookProjectLoader.ReferencePathResolver = projectRoot =>
            {
                HostPluginCatalog.EnsureLoaded();
                return HostPluginCatalog.GetProjectReferencePaths(projectRoot);
            };

            BookRoslynCompiler.AdditionalReferenceResolver = () =>
            {
                HostPluginCatalog.EnsureLoaded();
                return HostPluginCatalog.GetProjectReferencePaths(AppContext.BaseDirectory);
            };

            BookRoslynCompiler.InfoLogger = Core.LogInfo;
            BookRoslynCompiler.DebugLogger = message => Core.LogDebug(message);
            _initialized = true;
        }
    }
}