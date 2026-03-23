using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Amium.Helpers;
using Amium.Host;
using Amium.Host.Logging;
using Amium.Items;

namespace DefinitionPage1;

public class qPage : BookPage
{
    private const string DemoProcessLogName = "Page1Demo";
    public string Title { get; private set; } = "Page1";

    public static Item DemoSinus = CreateDemoItem("AThread Sinus", "Page1/Sinus", "sin", 0f);
    public static Item DemoTask = CreateDemoItem("ATask Counter", "Page1/Task", "ticks", 0);
    public static Item DemoTimer = CreateDemoItem("ATimer Pulse", "Page1/Timer", "ticks", 0);
    public static Item DemoTimerHp = CreateDemoItem("ATimer HP", "Page1/TimerHp", "ticks", 0);

    private SinusSignal? _sinusSignal;
    private ATask? _taskDemo;
    private ATimer? _timerDemo;
    private ATimerHighPrecision? _timerHpDemo;
    private ProcessLog? _demoProcessLog;
    private DemoCanBus? _demoCanBus;
    private Item? _demoCanBusS1;

    private int _sinusTickCount;
    private int _taskTickCount;
    private int _timerTickCount;
    private int _timerHpTickCount;

    public qPage() : base("Page1")
    {
    }

    protected override void OnInitialize()
    {
        EnsureDemoProcessLog();
        EnsureAttachedCanBusSignal();
        Title = "Page1 initialized";
        _demoProcessLog?.Info("[Page1Demo] Initialize completed");
    }

    protected override void OnRun()
    {
        Core.LogInfo("[Page1] Run started");
        EnsureDemoProcessLog();
        _demoProcessLog?.Info("[Page1Demo] Run started");
        StopDemos();
        ResetCounters();
        RegisterSnapshots();

        StartSinusDemo();
        StartTaskDemo();
        StartTimerDemo();
        StartHighPrecisionTimerDemo();

        Core.LogInfo("[Page1] Run completed");
        _demoProcessLog?.Info("[Page1Demo] Run completed");
    }

    protected override void OnDestroy()
    {
        Core.LogInfo("[Page1] Destroy started");
        _demoProcessLog?.Info("[Page1Demo] Destroy started");
        StopDemos();
        _demoCanBusS1 = null;
        Title = "Page1 destroyed";
        Core.LogInfo("[Page1] Destroy completed");
        _demoProcessLog?.Info("[Page1Demo] Destroy completed");
    }

    private void EnsureDemoProcessLog()
    {
        if (_demoProcessLog is not null)
        {
            PublishProcessLog(DemoProcessLogName, _demoProcessLog, DemoProcessLogName);
            return;
        }

        var logDirectory = Path.Combine(HostLogger.LogDirectory, "page1-demo");
        Directory.CreateDirectory(logDirectory);

        _demoProcessLog = new ProcessLog();
        _demoProcessLog.InitializeLog(logDirectory);
        PublishProcessLog(DemoProcessLogName, _demoProcessLog, DemoProcessLogName);
        _demoProcessLog.Info($"[{DemoProcessLogName}] ProcessLog initialized at {logDirectory}");
    }

    private static Item CreateDemoItem(string text, string path, string unit, object initialValue)
    {
        var item = new Item(name: text, path: path);
        item.Params["Text"].Value = text;
        item.Params["Unit"].Value = unit;
        item.Value = initialValue;
        return item;
    }

    private void RegisterSnapshots()
    {
        EnsureAttachedCanBusSignal();
        UiPublisher.Publish(DemoSinus);
        UiPublisher.Publish(DemoTask);
        UiPublisher.Publish(DemoTimer);
        UiPublisher.Publish(DemoTimerHp);
        if (_demoCanBusS1 is not null)
        {
            PublishItem(_demoCanBusS1);
        }
        Core.LogInfo("[Page1] Demo snapshots registered");
        _demoProcessLog?.Info("[Page1Demo] Demo snapshots registered");
    }

    private void StartSinusDemo()
    {
        _sinusSignal = new SinusSignal(20);
        _sinusSignal.OnNewValue += value =>
        {
            _sinusTickCount++;
            HostRegistries.Data.UpdateValue(DemoSinus.Path!, value);
            _demoCanBus?.UpdateValue("S1", (float)Math.Round((value + 1f) * 500f, 1));
            if (_sinusTickCount % 25 == 0)
            {
                Core.LogDebug($"[Page1] Sinus tick={_sinusTickCount} value={value:0.000}");
                _demoProcessLog?.Debug($"[Page1Demo] Sinus tick={_sinusTickCount} value={value:0.000}");
            }
        };

        Core.LogInfo("[Page1] Sinus demo started");
        _demoProcessLog?.Info("[Page1Demo] Sinus demo started");
    }

    private void StartTaskDemo()
    {
        _taskDemo = new ATask("Page1-ATask-Demo", async token =>
        {
            Core.LogInfo("[Page1] ATask demo loop entered");
            while (!token.IsCancellationRequested)
            {
                _taskTickCount++;
                HostRegistries.Data.UpdateValue(DemoTask.Path!, _taskTickCount);
                if (_taskTickCount % 8 == 0)
                {
                    Core.LogInfo($"[Page1] ATask heartbeat tick={_taskTickCount}");
                    _demoProcessLog?.Info($"[Page1Demo] ATask heartbeat tick={_taskTickCount}");
                }

                await Task.Delay(250, token).ConfigureAwait(false);
            }
        });

        Core.LogInfo("[Page1] ATask demo started");
        _demoProcessLog?.Info("[Page1Demo] ATask demo started");
    }

    private void StartTimerDemo()
    {
        _timerDemo = new ATimer("Page1-ATimer-Demo", 500);
        _timerDemo.Tick += () =>
        {
            _timerTickCount++;
            HostRegistries.Data.UpdateValue(DemoTimer.Path!, _timerTickCount);
            if (_timerTickCount % 6 == 0)
            {
                Core.LogInfo($"[Page1] ATimer heartbeat tick={_timerTickCount}");
                _demoProcessLog?.Info($"[Page1Demo] ATimer heartbeat tick={_timerTickCount}");
            }
        };
        _timerDemo.Start();
        Core.LogInfo("[Page1] ATimer demo started");
        _demoProcessLog?.Info("[Page1Demo] ATimer demo started");
    }

    private void StartHighPrecisionTimerDemo()
    {
        _timerHpDemo = new ATimerHighPrecision("Page1-ATimerHighPrecision-Demo", 100);
        _timerHpDemo.Tick += () =>
        {
            _timerHpTickCount++;
            HostRegistries.Data.UpdateValue(DemoTimerHp.Path!, _timerHpTickCount);
            if (_timerHpTickCount % 20 == 0)
            {
                Core.LogDebug($"[Page1] ATimerHighPrecision tick={_timerHpTickCount}");
                _demoProcessLog?.Debug($"[Page1Demo] ATimerHighPrecision tick={_timerHpTickCount}");
            }
        };
        _timerHpDemo.Start();
        Core.LogInfo("[Page1] ATimerHighPrecision demo started");
        _demoProcessLog?.Info("[Page1Demo] ATimerHighPrecision demo started");
    }

    private void StopDemos()
    {
        if (_sinusSignal is not null)
        {
            _sinusSignal.Stop();
            _sinusSignal = null;
            _demoProcessLog?.Info("[Page1Demo] Sinus demo stopped");
        }

        if (_taskDemo is not null)
        {
            _taskDemo.Stop();
            _taskDemo = null;
            _demoProcessLog?.Info("[Page1Demo] ATask demo stopped");
        }

        if (_timerDemo is not null)
        {
            _timerDemo.Dispose();
            _timerDemo = null;
            _demoProcessLog?.Info("[Page1Demo] ATimer demo stopped");
        }

        if (_timerHpDemo is not null)
        {
            _timerHpDemo.Dispose();
            _timerHpDemo = null;
            _demoProcessLog?.Info("[Page1Demo] ATimerHighPrecision demo stopped");
        }
    }

    private void ResetCounters()
    {
        _sinusTickCount = 0;
        _taskTickCount = 0;
        _timerTickCount = 0;
        _timerHpTickCount = 0;

        DemoSinus.Value = 0f;
        DemoTask.Value = 0;
        DemoTimer.Value = 0;
        DemoTimerHp.Value = 0;
        _demoCanBus?.UpdateValue("S1", 0f);
        _demoProcessLog?.Info("[Page1Demo] Counters reset");
    }

    private void EnsureAttachedCanBusSignal()
    {
        _demoCanBus ??= new DemoCanBus();
        _demoCanBusS1 ??= Attach(_demoCanBus.Items["S1"], "CanBus/S1");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Test()
    {
        Core.LogDebug("[Page1] Test() called");
    }

    private sealed class DemoCanBus
    {
        public DemoCanBus()
        {
            var s1 = Items["S1"];
            s1.Params["Text"].Value = "CAN Signal S1";
            s1.Params["Unit"].Value = "raw";
            s1.Params["Kind"].Value = "CanSignal";
            s1.Value = 0f;
        }

        public ItemDictionary Items { get; } = new("Runtime/CanBus");

        public void UpdateValue(string name, object value)
        {
            var item = Items[name];
            item.Value = value;
        }
    }
}
