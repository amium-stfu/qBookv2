using System.Text.Json.Nodes;

namespace Amium.Host.Python.Client;

public sealed class PythonClientOptions
{
    public string Name { get; init; } = "UnnamedPythonClient";
    public string ClientType { get; init; } = "generic";

    /// <summary>
    /// Path to the Python script that implements the client side of the bridge.
    /// </summary>
    public string ScriptPath { get; init; } = string.Empty;

    /// <summary>
    /// Optional working directory for the Python process. If null or empty,
    /// the directory of <see cref="ScriptPath"/> is used.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Executable used to start Python. Defaults to "python" and must be on PATH.
    /// </summary>
    public string PythonExecutable { get; init; } = "python";

    /// <summary>
    /// Bridge protocol version the host expects. Used during the handshake.
    /// </summary>
    public string BridgeVersion { get; init; } = "1.0";

    /// <summary>
    /// Capabilities the host allows for this client instance (e.g. "values", "functions", "host_log").
    /// They are announced to the Python side in the init payload.
    /// </summary>
    public IReadOnlyCollection<string> AllowedCapabilities { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Arbitrary configuration object that is passed to the Python side as JSON in the init payload.
    /// </summary>
    public JsonNode? Configuration { get; init; }

    /// <summary>
    /// Optional metadata describing the runtime scope (e.g. project id, layout id, etc.).
    /// </summary>
    public JsonNode? RuntimeScopeMetadata { get; init; }

    /// <summary>
    /// Optional registry root for values published by this client.
    /// If omitted, the host falls back to PythonClients.<ClientName>.
    /// </summary>
    public string? RegistryRootPath { get; init; }

    /// <summary>
    /// Maximum time the host waits for a complete handshake (hello -> init -> ready).
    /// </summary>
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum time the host waits for a graceful stop before escalating to a hard kill.
    /// </summary>
    public TimeSpan SoftStopTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Optional per-invocation timeout for function calls, if the caller does not specify its own.
    /// </summary>
    public TimeSpan DefaultInvokeTimeout { get; init; } = TimeSpan.FromSeconds(30);
}