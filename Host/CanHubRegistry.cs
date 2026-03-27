using System.Collections.Concurrent;

namespace Amium.Host
{
    internal static class CanHubRegistry
    {
        private static readonly ConcurrentDictionary<int, CanHub> Hubs = new();

        public static CanHub GetOrCreate(int port)
        {
            return Hubs.GetOrAdd(port, static p => new CanHub(p));
        }
    }
}
