using System;
using System.IO;

namespace UdlBook;

public sealed class UdlBookAppConfig
{
    public string? StartLayout { get; set; }
    public string? DefaultTheme { get; set; }

    public static UdlBookAppConfig Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new UdlBookAppConfig();
            }

            var config = new UdlBookAppConfig();
            var lines = File.ReadAllLines(path);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf(':');
                if (separatorIndex < 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();

                switch (key)
                {
                    case "StartLayout":
                        config.StartLayout = string.IsNullOrWhiteSpace(value) ? null : value;
                        break;
                    case "DefaultTheme":
                        config.DefaultTheme = string.IsNullOrWhiteSpace(value) ? null : value;
                        break;
                }
            }

            return config;
        }
        catch
        {
            // On any parsing error, fall back to defaults.
            return new UdlBookAppConfig();
        }
    }

    public void Save(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var writer = new StreamWriter(path, false);
            writer.WriteLine("# UdlBook application configuration");
            writer.WriteLine("# Simple YAML-style key/value pairs");
            writer.WriteLine($"StartLayout: {StartLayout ?? string.Empty}");
            writer.WriteLine($"DefaultTheme: {DefaultTheme ?? string.Empty}");
        }
        catch
        {
            // Swallow IO errors for now; config persistence is non-critical.
        }
    }
}
