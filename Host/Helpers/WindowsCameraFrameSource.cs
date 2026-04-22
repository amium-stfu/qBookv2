using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using System.Drawing;
using System.Drawing.Imaging;

namespace Amium.Host.Helpers;

[SupportedOSPlatform("windows")]
public sealed class WindowsCameraFrameSource : ICameraFrameSource, IDisposable
{
    private readonly int _deviceIndex;
    private readonly object _sync = new();
    private VideoCaptureDevice? _device;
    private byte[]? _currentFrame;
    private bool _disposed;
    private readonly List<string> _supportedResolutions = new();
    private string? _currentResolutionLabel;

    public WindowsCameraFrameSource(string name, int deviceIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Camera name must not be empty.", nameof(name));
        }

        Name = name.Trim();
        _deviceIndex = deviceIndex;

        StartDevice();
    }

    public string Name { get; }

    public object? CurrentFrame
    {
        get
        {
            lock (_sync)
            {
                return _currentFrame;
            }
        }
    }

    public IReadOnlyCollection<string> SupportedResolutions
    {
        get
        {
            lock (_sync)
            {
                return _supportedResolutions.ToArray();
            }
        }
    }

    public void SetResolution(string? resolutionLabel)
    {
        if (_disposed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(resolutionLabel))
        {
            return;
        }

        lock (_sync)
        {
            _currentResolutionLabel = resolutionLabel.Trim();
        }

        try
        {
            RestartDeviceWithResolution();
        }
        catch
        {
            // Resolution-Wechsel ist optional; Fehler hier ignorieren
        }
    }

    public event EventHandler? FrameAvailable;

    private void StartDevice()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (devices.Count == 0 || _deviceIndex < 0 || _deviceIndex >= devices.Count)
            {
                return;
            }

            var info = devices[_deviceIndex];
            var device = new VideoCaptureDevice(info.MonikerString);

            // Alle unterstützten Auflösungen sammeln
            var capabilities = device.VideoCapabilities ?? Array.Empty<VideoCapabilities>();
            var resolutionLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            VideoCapabilities? preferred = null;

            foreach (var cap in capabilities)
            {
                var label = $"{cap.FrameSize.Width}x{cap.FrameSize.Height}";
                resolutionLabels.Add(label);
            }

            lock (_sync)
            {
                _supportedResolutions.Clear();
                _supportedResolutions.AddRange(resolutionLabels.OrderBy(v => v, StringComparer.OrdinalIgnoreCase));
            }

            // Aktuelle oder bevorzugte Auflösung wählen
            preferred = SelectPreferredCapabilities(capabilities);
            if (preferred is not null)
            {
                device.VideoResolution = preferred;
            }

            device.NewFrame += OnNewFrame;
            device.Start();

            _device = device;
        }
        catch
        {
            // Kamera ist optional; Fehler hier sind nicht fatal
        }
    }

    private VideoCapabilities? SelectPreferredCapabilities(VideoCapabilities[] capabilities)
    {
        if (capabilities.Length == 0)
        {
            return null;
        }

        string? desiredLabel;
        lock (_sync)
        {
            desiredLabel = _currentResolutionLabel;
        }

        if (!string.IsNullOrWhiteSpace(desiredLabel))
        {
            var match = capabilities.FirstOrDefault(cap =>
                string.Equals($"{cap.FrameSize.Width}x{cap.FrameSize.Height}", desiredLabel, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        // Fallback: 1280x720, sonst erste Capability
        var hd = capabilities.FirstOrDefault(cap => cap.FrameSize.Width == 1280 && cap.FrameSize.Height == 720);
        return hd ?? capabilities[0];
    }

    private void RestartDeviceWithResolution()
    {
        VideoCaptureDevice? oldDevice;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            oldDevice = _device;
            _device = null;
        }

        try
        {
            if (oldDevice is not null)
            {
                oldDevice.NewFrame -= OnNewFrame;
                if (oldDevice.IsRunning)
                {
                    oldDevice.SignalToStop();
                    oldDevice.WaitForStop();
                }
            }
        }
        catch
        {
            // ignore cleanup errors on restart
        }

        // Neues Device mit der (ggf. geänderten) gewünschten Resolution starten.
        StartDevice();
    }

    private void OnNewFrame(object sender, NewFrameEventArgs eventArgs)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            using Bitmap frame = (Bitmap)eventArgs.Frame.Clone();
            using var ms = new MemoryStream();
            frame.Save(ms, ImageFormat.Png);
            var bytes = ms.ToArray();

            if (bytes.Length == 0)
            {
                return;
            }

            lock (_sync)
            {
                _currentFrame = bytes;
            }

            FrameAvailable?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Einzelne Frame-Fehler ignorieren
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (_device is not null)
            {
                _device.NewFrame -= OnNewFrame;
                if (_device.IsRunning)
                {
                    _device.SignalToStop();
                    _device.WaitForStop();
                }
                _device = null;
            }
        }
        catch
        {
            // ignore cleanup errors
        }
    }
}
