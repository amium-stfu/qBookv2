using System;
using UiEditor.Items;

namespace UiEditor.Host;

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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
