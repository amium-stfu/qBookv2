using System;
using Amium.Helpers;
using Amium.Host;
using Amium.Items;

namespace DefinitionSimulation;

public class qPage : BookPage
{
    private const string TrendPath = "Simulation/Signals/Trend";
    private const string ReadPath = "Simulation/Signals/Read";
    private const string SetpointPath = "Simulation/Signals/Setpoint";
    private const string TauPath = "Simulation/Signals/Tau";
    private const string NoisePath = "Simulation/Signals/Noise";

    private readonly Item _trendSource = CreateDemoItem("Trend", "Runtime/Simulation/Trend", "value", 0f);
    private readonly Item _readSource = CreateDemoItem("Read", "Runtime/Simulation/Read", "value", 0f);
    private readonly Item _setpointSource = CreateDemoItem("Setpoint", "Runtime/Simulation/Setpoint", "value", 40f);
    private readonly Item _tauSource = CreateDemoItem("Tau", "Runtime/Simulation/Tau", "s", 0.8f);
    private readonly Item _noiseSource = CreateDemoItem("Noise", "Runtime/Simulation/Noise", "amp", 0.05f);

    private Item? _trendAttached;
    private Item? _readAttached;
    private Item? _setpointAttached;
    private Item? _tauAttached;
    private Item? _noiseAttached;

    private TrendSignal? _trendSignal;
    private ReadSetSimulation? _readSetSimulation;

    private float _setpoint = 40f;
    private float _tau = 0.8f;
    private float _noise = 0.05f;
    private bool _isRunning;

    public qPage() : base("Simulation")
    {
    }

    protected override void OnInitialize()
    {
        _trendAttached ??= Attach(_trendSource, "Signals/Trend");
        _readAttached ??= Attach(_readSource, "Signals/Read");
        _setpointAttached ??= Attach(_setpointSource, "Signals/Setpoint");
        _tauAttached ??= Attach(_tauSource, "Signals/Tau");
        _noiseAttached ??= Attach(_noiseSource, "Signals/Noise");

        PublishCommands();
        PublishSignalItems();
        PublishParameters();
    }

    protected override void OnRun()
    {
        StartSimulation();
        PublishSignalValues();
    }

    protected override void OnDestroy()
    {
        StopSimulation();
    }

    private void PublishCommands()
    {
        UiPublisher.Publish(AttachCommand("Start", ExecuteStart, "Start all simulations"));
        UiPublisher.Publish(AttachCommand("Stop", ExecuteStop, "Stop all simulations"));
        UiPublisher.Publish(AttachCommand("SetpointUp", ExecuteSetpointUp, "Increase setpoint"));
        UiPublisher.Publish(AttachCommand("SetpointDown", ExecuteSetpointDown, "Decrease setpoint"));
        UiPublisher.Publish(AttachCommand("TauFast", ExecuteTauFast, "Reduce tau for faster dynamics"));
        UiPublisher.Publish(AttachCommand("TauSlow", ExecuteTauSlow, "Increase tau for slower dynamics"));
        UiPublisher.Publish(AttachCommand("NoiseUp", ExecuteNoiseUp, "Increase noise amplitude"));
        UiPublisher.Publish(AttachCommand("NoiseDown", ExecuteNoiseDown, "Decrease noise amplitude"));
        UiPublisher.Publish(AttachCommand("Peak", ExecutePeak, "Inject random peak"));
    }

    private void ExecuteStart()
    {
        StartSimulation();
    }

    private void ExecuteStop()
    {
        StopSimulation();
    }

    private void ExecuteSetpointUp()
    {
        _setpoint += 5f;
        ApplySimulationParameters();
    }

    private void ExecuteSetpointDown()
    {
        _setpoint -= 5f;
        ApplySimulationParameters();
    }

    private void ExecuteTauFast()
    {
        _tau = Math.Max(0.05f, _tau / 1.5f);
        ApplySimulationParameters();
    }

    private void ExecuteTauSlow()
    {
        _tau = Math.Min(10f, _tau * 1.5f);
        ApplySimulationParameters();
    }

    private void ExecuteNoiseUp()
    {
        _noise = Math.Min(2f, _noise + 0.05f);
        ApplySimulationParameters();
    }

    private void ExecuteNoiseDown()
    {
        _noise = Math.Max(0f, _noise - 0.05f);
        ApplySimulationParameters();
    }

    private void ExecutePeak()
    {
        _readSetSimulation?.AddPeak(-2, 2);
    }

    private void StartSimulation()
    {
        if (_isRunning)
        {
            return;
        }

        _trendSignal = new TrendSignal(150);
        _trendSignal.SetBaseLevel(60);
        _trendSignal.SetTrend(-0.015);
        _trendSignal.SetNoise(1.2);
        _trendSignal.OnNewValue += OnTrendValue;

        _readSetSimulation = new ReadSetSimulation
        {
            UpdateRateMs = 120,
            Set = _setpoint,
            Tau = _tau,
            NoiseStrength = _noise,
            NoiseFrequency = 4
        };
        _readSetSimulation.OnNewValue += OnReadValue;

        _trendSignal.Start();

        _isRunning = true;
        PublishParameters();
    }

    private void StopSimulation()
    {
        if (!_isRunning)
        {
            return;
        }

        if (_trendSignal is not null)
        {
            _trendSignal.OnNewValue -= OnTrendValue;
            _trendSignal.Stop();
            _trendSignal = null;
        }

        if (_readSetSimulation is not null)
        {
            _readSetSimulation.OnNewValue -= OnReadValue;
            _readSetSimulation.Stop();
            _readSetSimulation = null;
        }

        _isRunning = false;
    }

    private void ApplySimulationParameters()
    {
        if (_readSetSimulation is not null)
        {
            _readSetSimulation.Set = _setpoint;
            _readSetSimulation.Tau = _tau;
            _readSetSimulation.NoiseStrength = _noise;
        }

        PublishParameters();
    }

    private void PublishSignalItems()
    {
        if (_trendAttached is not null)
        {
            UiPublisher.Publish(_trendAttached);
        }

        if (_readAttached is not null)
        {
            UiPublisher.Publish(_readAttached);
        }
    }

    private void PublishSignalValues()
    {
        HostRegistries.Data.UpdateValue(TrendPath, _trendSource.Value);
        HostRegistries.Data.UpdateValue(ReadPath, _readSource.Value);
    }

    private void PublishParameters()
    {
        _setpointSource.Value = _setpoint;
        _tauSource.Value = _tau;
        _noiseSource.Value = _noise;
        HostRegistries.Data.UpdateValue(SetpointPath, _setpoint);
        HostRegistries.Data.UpdateValue(TauPath, _tau);
        HostRegistries.Data.UpdateValue(NoisePath, _noise);
    }

    private void OnTrendValue(float value)
    {
        _trendSource.Value = value;
        HostRegistries.Data.UpdateValue(TrendPath, value);
    }

    private void OnReadValue(float value)
    {
        _readSource.Value = value;
        HostRegistries.Data.UpdateValue(ReadPath, value);
    }

    private static Item CreateDemoItem(string text, string path, string unit, object initialValue)
    {
        var item = new Item(name: text, path: path);
        item.Params["Text"].Value = text;
        item.Params["Unit"].Value = unit;
        item.Value = initialValue;
        return item;
    }
}
