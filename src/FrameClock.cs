using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ATSTelemetry;

/// <summary>
/// Paced frame clock. Runs on its own thread and requests a frame at a fixed
/// rate using the multimedia timer resolution (1 ms) plus a short spin wait.
/// </summary>
public sealed class FrameClock : IDisposable
{
    private readonly Action _tick;
    private readonly double _frameMilliseconds;
    private readonly Thread _thread;
    private volatile bool _running;
    private bool _periodRaised;

    public FrameClock(int framesPerSecond, Action tick)
    {
        if (framesPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        }

        _tick = tick;
        _frameMilliseconds = 1000.0 / framesPerSecond;
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "TelemetryFrameClock",
            Priority = ThreadPriority.AboveNormal
        };
    }

    public void Start()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        _periodRaised = timeBeginPeriod(1) == 0;
        _thread.Start();
    }

    private void Loop()
    {
        var clock = Stopwatch.StartNew();
        var next = 0.0;

        while (_running)
        {
            next += _frameMilliseconds;
            var now = clock.Elapsed.TotalMilliseconds;

            if (now - next > _frameMilliseconds * 4)
            {
                next = now + _frameMilliseconds;
            }

            var sleep = next - now;
            if (sleep > 2.0)
            {
                Thread.Sleep((int)(sleep - 1.0));
            }

            while (_running)
            {
                now = clock.Elapsed.TotalMilliseconds;
                if (now >= next)
                {
                    break;
                }

                if (next - now > 1.0)
                {
                    Thread.Sleep(1);
                }
                else
                {
                    Thread.SpinWait(200);
                }
            }

            if (!_running)
            {
                break;
            }

            try
            {
                _tick();
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        _running = false;

        if (_thread.IsAlive)
        {
            _thread.Join(250);
        }

        if (_periodRaised)
        {
            timeEndPeriod(1);
            _periodRaised = false;
        }
    }

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint period);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint period);
}
