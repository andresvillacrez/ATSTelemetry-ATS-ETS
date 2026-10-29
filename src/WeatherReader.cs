using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ATSTelemetry;

/// <summary>
/// Lee el estado del clima directamente de la memoria del juego.
///
/// El SDK de SCS (REV12) no publica el clima ni la lluvia. El juego mantiene
/// un objeto de clima con la humedad de la carretera en vivo; se localiza a
/// través de un puntero estático del módulo y se valida su estructura antes
/// de usar el valor. Si el juego cambia de versión y la estructura ya no
/// coincide, el lector devuelve null y el panel conserva el clima manual.
///
/// Offsets verificados:
///   ATS  1.61.3.18  puntero 0x3051D40, humedad +0x5E8
///   ETS2 1.61.1.9   puntero 0x3046780, humedad +0x5E8
/// </summary>
public sealed class WeatherReader : IDisposable
{
    private const uint ProcessVmRead = 0x0010;

    private const int AtsWeatherPointer = 0x3051D40;
    private const int Ets2WeatherPointer = 0x3046780;

    // Bloque: current_weather (int), rain_wetness (float), timer (float)
    // y seis centinelas 0x7FFFFFFF. Se lee desde +0x5E4.
    private const int WeatherBlockOffset = 0x5E4;
    private const int WeatherBlockLength = 40;

    private const int CacheMilliseconds = 900;

    private readonly object _gate = new();

    private IntPtr _processHandle;
    private int _processId;
    private long _moduleBase;
    private long _objectAddress;
    private bool _ats;
    private long _lastReadTicks;
    private WeatherCondition? _lastCondition;
    private float? _lastWetness;

    /// <summary>
    /// Última humedad leída (0 = seco, 1 = completamente mojado).
    /// Es informativa; el panel usa <see cref="Read"/>.
    /// </summary>
    public float? Wetness
    {
        get
        {
            lock (_gate)
            {
                return _lastWetness;
            }
        }
    }

    /// <summary>
    /// Devuelve el clima actual del juego, o null si no se puede leer.
    /// </summary>
    /// <param name="ats">true para ATS; false para ETS2.</param>
    public WeatherCondition? Read(bool ats)
    {
        lock (_gate)
        {
            var now = Environment.TickCount64;
            if (now - _lastReadTicks < CacheMilliseconds)
            {
                return _lastCondition;
            }

            _lastReadTicks = now;

            try
            {
                if (!EnsureProcess(ats) || (_objectAddress == 0 && !ResolveObject()))
                {
                    _lastCondition = null;
                    _lastWetness = null;
                    return null;
                }

                var block = new byte[WeatherBlockLength];
                if (!ReadProcessMemory(
                        _processHandle,
                        (IntPtr)(_objectAddress + WeatherBlockOffset),
                        block,
                        block.Length,
                        out var read)
                    || read.ToInt64() < WeatherBlockLength)
                {
                    // El juego pudo reiniciarse o cerrar la partida.
                    ResetProcess();
                    _lastCondition = null;
                    _lastWetness = null;
                    return null;
                }

                var currentWeather = BitConverter.ToInt32(block, 0);
                var wetness = BitConverter.ToSingle(block, 4);
                var timer = BitConverter.ToSingle(block, 8);

                var sentinelsOk = true;
                for (var i = 0; i < 6; i++)
                {
                    if (BitConverter.ToInt32(block, 12 + i * 4) != 0x7FFFFFFF)
                    {
                        sentinelsOk = false;
                        break;
                    }
                }

                if (!sentinelsOk
                    || currentWeather is < 0 or > 16
                    || !float.IsFinite(wetness)
                    || wetness is < -0.01f or > 1.25f
                    || !float.IsFinite(timer))
                {
                    // El puntero ya no apunta a un objeto de clima válido.
                    _objectAddress = 0;
                    _lastCondition = null;
                    _lastWetness = null;
                    return null;
                }

                _lastWetness = wetness;
                _lastCondition = Classify(wetness);
                return _lastCondition;
            }
            catch
            {
                ResetProcess();
                _lastCondition = null;
                _lastWetness = null;
                return null;
            }
        }
    }

    private bool EnsureProcess(bool ats)
    {
        if (_processHandle != IntPtr.Zero && _ats == ats && IsProcessAlive(_processId))
        {
            return true;
        }

        ResetProcess();

        var processName = ats ? "amtrucks" : "eurotrucks2";
        Process? selected = null;

        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                var hasWindow = process.MainWindowHandle != IntPtr.Zero;
                var selectedHasWindow = selected != null && selected.MainWindowHandle != IntPtr.Zero;

                if (selected == null || (!selectedHasWindow && hasWindow))
                {
                    selected?.Dispose();
                    selected = process;
                    continue;
                }
            }
            catch
            {
                // Proceso terminando o sin permisos.
            }

            process.Dispose();
        }

        if (selected == null)
        {
            return false;
        }

        _processId = selected.Id;
        _ats = ats;

        try
        {
            _processHandle = OpenProcess(ProcessVmRead, false, _processId);
            if (_processHandle == IntPtr.Zero)
            {
                return false;
            }

            _moduleBase = selected.MainModule?.BaseAddress.ToInt64() ?? 0;
            if (_moduleBase == 0)
            {
                ResetProcess();
                return false;
            }

            return true;
        }
        catch
        {
            ResetProcess();
            return false;
        }
        finally
        {
            selected.Dispose();
        }
    }

    private bool ResolveObject()
    {
        var pointerOffset = _ats ? AtsWeatherPointer : Ets2WeatherPointer;
        var buffer = new byte[8];

        if (!ReadProcessMemory(
                _processHandle,
                (IntPtr)(_moduleBase + pointerOffset),
                buffer,
                buffer.Length,
                out var read)
            || read.ToInt64() < buffer.Length)
        {
            return false;
        }

        var pointer = BitConverter.ToInt64(buffer, 0);
        if (pointer <= 0x10000
            || pointer >= 0x00007FFFFFFFFFFF
            || (pointer & 7) != 0)
        {
            return false;
        }

        _objectAddress = pointer;
        return true;
    }

    private static WeatherCondition Classify(float wetness)
    {
        if (wetness >= 0.80f)
        {
            return WeatherCondition.Storm;
        }

        if (wetness >= 0.15f)
        {
            return WeatherCondition.Rain;
        }

        if (wetness >= 0.02f)
        {
            return WeatherCondition.Cloudy;
        }

        return WeatherCondition.Clear;
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private void ResetProcess()
    {
        if (_processHandle != IntPtr.Zero)
        {
            try
            {
                CloseHandle(_processHandle);
            }
            catch
            {
                // La app debe seguir funcionando aunque falle el cierre.
            }
        }

        _processHandle = IntPtr.Zero;
        _processId = 0;
        _moduleBase = 0;
        _objectAddress = 0;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            ResetProcess();
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr process,
        IntPtr address,
        byte[] buffer,
        int size,
        out IntPtr bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
