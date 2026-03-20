using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using UiEditor.Host;

namespace UiEditor.Helpers
{
    public class TrendSignal
    {
        private static int _nextInstanceId;
        private readonly int _intervalMs;
        private readonly string _instanceName;
        private ATask? _loopTask;
        private int _isRunning;
        private readonly Random _random = new Random();
        private readonly Random _random2 = new Random();
        private readonly Random _random3 = new Random();
        private double _baseLevel = 100.0; // Startniveau
        private double _trend = -0.01; // Abfallender Trend pro Tick
        private double _noiseAmplitude = 1.5;

        public float Value;

        /// <summary>
        /// Create a TrendSimualtion using a Task-based loop. Call Start() to begin generating values.
        /// Callbacks run on thread-pool threads; marshal to the UI thread if required.
        /// </summary>
        public TrendSignal(double intervalMs = 1)
        {
            _intervalMs = Math.Max(1, (int)intervalMs);
            _instanceName = $"{nameof(TrendSignal)}-{Interlocked.Increment(ref _nextInstanceId)}";
        }

        public void Start()
        {
            // Ensure only one running loop
            if (Interlocked.Exchange(ref _isRunning, 1) == 1)
                return;

            _loopTask = new ATask(_instanceName, async token =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        GenerateNextValue();
                        await Task.Delay(_intervalMs, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { }
                finally
                {
                    Interlocked.Exchange(ref _isRunning, 0);
                }
            });
        }

        public void Stop()
        {
            if (_loopTask == null)
                return;

            try { _loopTask.Stop(); } catch { }
            _loopTask = null;
            Interlocked.Exchange(ref _isRunning, 0);
        }

        public void SetBaseLevel(double newLevel) => _baseLevel = newLevel;
        public void SetTrend(double newTrend) => _trend = newTrend;
        public void SetNoise(double noiseAmplitude) => _noiseAmplitude = noiseAmplitude;


        int counter = 0;
        int counterSet = 0;

        public event Action<float>? OnNewValue;
        private void GenerateNextValue()
        {
            if (counter == 0)
                counterSet = (int)_random2.NextInt64(10, 100);
            // Rauschen erzeugen
            double noise = (_random.NextDouble() - 0.5) * 2.0 * _noiseAmplitude;

            // Trend wirkt nur in eine Richtung, z.?B. fallend
            _baseLevel += _trend;

            double value = _baseLevel + noise;
            Value = (float)value;
            OnNewValue?.Invoke(Value);
            counter++;

            if (counterSet <= counter)
            {
                counter = 0;
                SetTrend((double)_random3.NextInt64(-30, 30) / 1000);
                //Debug.WriteLine("Trend: " + _trend);

            }
        }
    }
    public class SinusSignal
    {
        private static int _nextInstanceId;
        private readonly string _instanceName;

        AThread? IdleThread;

        bool IsRunning = false;

        DateTime Starttime;

        int Interval;


        public float Value;

        public SinusSignal(int interval = 10)
        {
            Interval = interval;
            _instanceName = $"{nameof(SinusSignal)}-{Interlocked.Increment(ref _nextInstanceId)}";
            Core.LogDebug($"[SinusSignal] {_instanceName} constructed. Interval={Interval}");
            // Thread is created and started in Start()
            Start();
            
        }

        public void Start()
        {
            Core.LogDebug($"[SinusSignal] {_instanceName} Start requested. ExistingThread={IdleThread is not null} IsRunning={IsRunning}");
            Value = 0f;
            
            IsRunning = true;
            Starttime = DateTime.Now;
            IdleThread = new AThread(_instanceName, Idle);
            IdleThread.Start();
            Core.LogDebug($"[SinusSignal] {_instanceName} thread start issued at {Starttime:O}");
        }

        public void Stop()
        {
            Core.LogDebug($"[SinusSignal] {_instanceName} Stop requested. ThreadExists={IdleThread is not null} IsRunning={IsRunning}");
            IsRunning = false;
            IdleThread?.Stop();
            IdleThread = null;
            Core.LogDebug($"[SinusSignal] {_instanceName} stopped. FinalValue={Value}");
        }


        public event Action<float>? OnNewValue;
        void Idle(CancellationToken token)
        {
            Core.LogDebug($"[SinusSignal] {_instanceName} Idle entered. ThreadId={Environment.CurrentManagedThreadId}");
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int iteration = 0;

            try
            {
                while (IsRunning && !token.IsCancellationRequested)
                {
                    double elapsedSeconds = (DateTime.Now - Starttime).TotalSeconds;
                    Value = (float)Math.Sin(elapsedSeconds);

                    if (iteration == 0 || iteration % 1000 == 0)
                    {
                        Core.LogDebug($"[SinusSignal] {_instanceName} tick={iteration} value={Value:F4} elapsed={elapsedSeconds:F3}s cancel={token.IsCancellationRequested}");
                    }

                    // notify listeners
                    OnNewValue?.Invoke(Value);
                    while (stopwatch.ElapsedMilliseconds < Interval)
                    {
                        Thread.SpinWait(1);
                    }
                    stopwatch.Restart();
                    iteration++;
                }
            }
            catch (OperationCanceledException)
            {
                Core.LogDebug($"[SinusSignal] {_instanceName} Idle cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                Core.LogError($"[SinusSignal] {_instanceName} Idle failed.", ex);
                throw;
            }
            finally
            {
                Core.LogDebug($"[SinusSignal] {_instanceName} Idle exited. Iterations={iteration} IsRunning={IsRunning} CancelRequested={token.IsCancellationRequested}");
            }
        }

    }
    public class ReadSetSimulation
    {
        private static int _nextInstanceId;
        private readonly string _instanceName;
        // Set-/Ist-Werte (Tau wirkt als Zeitkonstante in Sekunden)
        private float _tau = 0.1f;
        public float Tau
        {
            get { lock (_lock) return _tau; }
            set
            {
                lock (_lock)
                {
                    if (float.IsNaN(value) || value <= 0f) value = 1e-3f;
                    _tau = value;
                }
            }
        }

        // Update-Periode (ms) f�r den Simulations-Task
        public int UpdateRateMs { get; set; } = 100;

        // Rauschparameter
        private float _noiseStrength = 0.0f;
        public float NoiseStrength
        {
            get { lock (_lock) return _noiseStrength; }
            set { lock (_lock) _noiseStrength = value < 0f ? 0f : value; }
        }

        // Noise-Frequenz: alle N Updates neuer Noise-Wert
        private int _noiseFrequency = 1;
        public int NoiseFrequency
        {
            get { lock (_lock) return _noiseFrequency; }
            set { _noiseFrequency = value < 1 ? 1 : value; }
        }

        // Aktueller Noise-Wert
        public float Noise { get; private set; } = 0f;

        // Peak-Injektion (einmalig addiert beim n�chsten Sample)
        private int _noisePeak = 0;

        // Z�hler f�r diskrete Noise-Erneuerung
        private int _noiseCounter = 0;

        // Task / Timing
        private readonly object _lock = new();
        private ATask? _task;
        private readonly Stopwatch _sw = new();
        private float _lastTime;

        // Letzte g�ltige Value zur Robustheit
        private float _lastGoodValue = 0f;

        public float TauValue
        {
            get { lock (_lock) return _tau; }
            set
            {
                lock (_lock)
                {
                    if (float.IsNaN(value) || value <= 0f) value = 1e-3f;
                    _tau = value;
                }
            }
        }

        public float NoiseVal
        {
            get { lock (_lock) return _noiseStrength; }
            set { lock (_lock) _noiseStrength = value < 0f ? 0f : value; }
        }

        public int NoiseFreq
        {
            get { lock (_lock) return _noiseFrequency; }
            set { lock (_lock) _noiseFrequency = value < 1 ? 1 : value; }
        }

        public float Value;

        public ReadSetSimulation()
        {
            _instanceName = $"{nameof(ReadSetSimulation)}-{Interlocked.Increment(ref _nextInstanceId)}";
            Value = 0f;

            _tau = 0.1f;
            _noiseStrength = 0.000f;
            _noiseFrequency = 0;

            _sw.Start();
            _lastTime = (float)_sw.Elapsed.TotalSeconds;

            _task = new ATask(_instanceName, RunLoop);
        }

        public event Action<float>? OnNewValue;
        private async Task RunLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                float now = (float)_sw.Elapsed.TotalSeconds;
                float dt = now - _lastTime;
                _lastTime = now;

                if (dt < 0f || dt > 5f) dt = 0f; // Schutz gegen Spr�nge

                lock (_lock)
                {
                    StepNoise();
                    StepDynamics(dt);

                }
                OnNewValue?.Invoke(Value);

                try { await Task.Delay(UpdateRateMs, token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            }
        }

        public float Set { get; set; } = 0.0f;
        private void StepDynamics(float dt)
        {
            // Eingaben pr�fen
            float set = Set;
            float tau;
            lock (_lock) tau = _tau;
            if (tau <= 1e-9f) tau = 1e-3f;

            if (float.IsNaN(set) || float.IsInfinity(set))
                set = _lastGoodValue;

            // 1. Ordnung Low-Pass Ann�herung
            // alpha in (0..1), f�r kleine dt/tau � dt / tau
            double alphaD = 1.0 - Math.Exp(- (double)dt / (double)tau);
            float alpha = (float)alphaD;
            if (alpha < 0f) alpha = 0f;
            else if (alpha > 1f) alpha = 1f;

            float newVal = Value + alpha * (set - Value) + Noise;

            if (float.IsNaN(newVal) || float.IsInfinity(newVal))
            {
                Value = _lastGoodValue;
                Noise = 0f;
            }
            else
            {
                Value = newVal;
                _lastGoodValue = Value;
            }
        }

        private static readonly ThreadLocal<Random> _rnd = new(() => new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId)));

        private void StepNoise()
        {
            int nf;
            float ns;
            lock (_lock)
            {
                nf = _noiseFrequency;
                ns = _noiseStrength;
            }
            if (nf < 1) nf = 1;

            _noiseCounter++;
            if (_noiseCounter >= nf)
            {
                _noiseCounter = 0;
                var r = _rnd.Value!;
                double baseNoise = (r.NextDouble() * 2.0 - 1.0) * ns + _noisePeak;
                Noise = (float)baseNoise;
                _noisePeak = 0;
            }
            else
            {
                Noise = _noisePeak;
                _noisePeak = 0;
            }
        }

        public void AddPeak(int min = -500, int max = 500)
        {
            if (min > max) (min, max) = (max, min);
            var r = _rnd.Value!;
            lock (_lock)
            {
                _noisePeak = r.Next(min, max + 1);
            }
        }

        public void Stop()
        {
            try { _task?.Stop(); } catch { }
            _task = null;
        }
    }
}

