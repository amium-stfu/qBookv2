using System;
using Amium.Host.Logging;
using Amium.Items;

namespace Amium.Host;

public abstract class BookPage : IDisposable
{
    private readonly UiPageContext _context;
    private bool _initialized;
    private bool _running;
    private bool _disposed;

    protected BookPage(string pageName, string? bookName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageName);

        PageName = pageName.Trim();
        BookName = string.IsNullOrWhiteSpace(bookName) ? null : bookName.Trim();
        _context = new UiPageContext(PageName, BookName);
    }

    protected string PageName { get; }
    protected string? BookName { get; }

    /// <summary>
    /// Attaches a source item to the current page context and returns the page-bound item instance.
    /// </summary>
    /// <param name="source">The source item that should be attached to this page.</param>
    /// <param name="alias">An optional page-local path segment used instead of the source item name.</param>
    /// <returns>The attached item instance bound to the current page context.</returns>
    protected Item Attach(Item source, string? alias = null)
        => _context.Attach(source, alias);

    /// <summary>
    /// Creates a page-scoped command using the convention &lt;Page&gt;/Commands/&lt;name&gt;.
    /// </summary>
    /// <param name="name">The page-local command name.</param>
    /// <param name="action">The command callback.</param>
    /// <param name="description">An optional description for editor selection and documentation.</param>
    /// <returns>A host command with the generated page-scoped command path.</returns>
    protected HostCommand CreateCommand(string name, Action action, string? description = null)
        => _context.CreateCommand(name, action, description);

    /// <summary>
    /// Creates a page-scoped command using the legacy attach naming.
    /// </summary>
    protected HostCommand AttachCommand(string name, Action action, string? description = null)
        => CreateCommand(name, action, description);

    /// <summary>
    /// Publishes an item to the UI. Raw source items are attached to this page automatically, while already page-bound items are published as-is.
    /// </summary>
    protected Item PublishItem(Item item, string? alias = null, bool pruneMissingMembers = false)
    {
        ArgumentNullException.ThrowIfNull(item);

        var pageItem = IsPageScoped(item.Path) && string.IsNullOrWhiteSpace(alias)
            ? item
            : Attach(item, alias);

        return UiPublisher.Publish(pageItem, pruneMissingMembers);
    }

    /// <summary>
    /// Publishes a page-scoped command to the command registry.
    /// </summary>
    protected HostCommand PublishCommand(string name, Action action, string? description = null)
    {
        var command = CreateCommand(name, action, description);
        UiPublisher.Publish(command);
        return command;
    }

    /// <summary>
    /// Publishes an already created command.
    /// </summary>
    protected HostCommand PublishCommand(HostCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        UiPublisher.Publish(command);
        return command;
    }

    /// <summary>
    /// Publishes a process log below the current page using the convention &lt;Page&gt;/Logs/&lt;name&gt;.
    /// </summary>
    protected Item PublishProcessLog(string name, ProcessLog log, string? title = null, bool pruneMissingMembers = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(log);

        return UiPublisher.Publish(BuildPagePath($"Logs/{name}"), log, title, pruneMissingMembers);
    }

    /// <summary>
    /// Publishes a camera source to the camera registry.
    /// </summary>
    protected ICameraFrameSource PublishCamera(ICameraFrameSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        UiPublisher.Publish(source);
        return source;
    }

    /// <summary>
    /// Initializes the page and invokes the initialization hook exactly once.
    /// </summary>
    public void Initialize()
    {
        ThrowIfDisposed();
        if (_initialized)
        {
            return;
        }

        OnInitialize();
        _initialized = true;
    }

    /// <summary>
    /// Starts the page lifecycle, ensuring initialization has completed beforehand.
    /// </summary>
    public void Run()
    {
        ThrowIfDisposed();
        if (!_initialized)
        {
            Initialize();
        }

        if (_running)
        {
            return;
        }

        OnRun();
        _running = true;
    }

    /// <summary>
    /// Stops the page lifecycle and releases the page context and attached resources.
    /// </summary>
    public void Destroy()
    {
        if (_disposed)
        {
            return;
        }

        if (_running)
        {
            OnDestroy();
            _running = false;
        }
        else if (_initialized)
        {
            OnDestroy();
        }

        _context.Dispose();
        _disposed = true;
        _initialized = false;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the page by delegating to <see cref="Destroy"/>.
    /// </summary>
    public void Dispose()
        => Destroy();

    protected abstract void OnInitialize();
    protected abstract void OnRun();
    protected abstract void OnDestroy();

    protected string BuildPagePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return $"{_context.PagePath}/{NormalizePath(relativePath)}";
    }

    private bool IsPageScoped(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalizedPath = NormalizePath(path);
        var normalizedPagePath = NormalizePath(_context.PagePath);
        return string.Equals(normalizedPath, normalizedPagePath, StringComparison.Ordinal)
            || normalizedPath.StartsWith($"{normalizedPagePath}/", StringComparison.Ordinal);
    }

    private static string NormalizePath(string value)
        => value.Replace('\\', '/').Trim('/');

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
