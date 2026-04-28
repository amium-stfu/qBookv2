using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace HornetStudio.Host
{
    public static class TokenManager
    {
        private static readonly object Sync = new();
        private static readonly Dictionary<CancellationTokenSource, RuntimeResourceScope> Owners = new();

        public static CancellationTokenSource CreateSource()
        {
            var cts = new CancellationTokenSource();
            var scope = RuntimeResourceScope.Current;
            lock (Sync)
            {
                Owners[cts] = scope;
            }

            scope.RegisterTokenSource(cts);
            return cts;
        }

        // Registriert einen neuen TokenSource, gibt den Token zurÃ¼ck
        public static CancellationToken CreateToken()
        {
            return CreateSource().Token;
        }

        // Optional: Gibt alle TokenSources zurÃ¼ck (fÃ¼r gezieltes Canceln)
        public static IReadOnlyList<CancellationTokenSource> Sources
        {
            get => RuntimeResourceScope.Current.TokenSourcesSnapshot();
        }

        public static void Deregister(CancellationTokenSource cts)
        {
            RuntimeResourceScope? scope;
            lock (Sync)
            {
                Owners.TryGetValue(cts, out scope);
                Owners.Remove(cts);
            }

            scope?.DeregisterTokenSource(cts);
        }

        // Bricht alle Token ab, disposed sie und leert die Liste
        public static void CancelAll()
        {
            RuntimeResourceScope.Current.CancelAllTokens();
        }
    }
}
