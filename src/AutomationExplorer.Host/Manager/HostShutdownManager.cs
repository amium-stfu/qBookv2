using System;
using System.Threading;

namespace Amium.Host
{
    public static class HostShutdownManager
    {
        private static int _applicationShutdownStarted;

        public static void StopAllRuntimeScopes(string reason)
        {
            RuntimeResourceScope.ShutdownAll(reason);
        }

        public static void ShutdownApplication(string reason)
        {
            if (Interlocked.Exchange(ref _applicationShutdownStarted, 1) != 0)
            {
                Core.LogDebug($"[HostShutdown] Application shutdown already in progress ({reason}).");
                return;
            }

            Core.LogInfo($"[HostShutdown] Application shutdown requested ({reason}).");

            try
            {
                Core.ShutdownAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Core.LogWarn($"[HostShutdown] Core shutdown failed: {ex.Message}");
            }

            try
            {
                StopAllRuntimeScopes(reason);
            }
            catch (Exception ex)
            {
                Core.LogWarn($"[HostShutdown] Runtime scope shutdown failed: {ex.Message}");
            }
        }
    }
}