using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace ATSTelemetry;

/// <summary>
/// Read-only access to the Windows SCS telemetry memory map.
///
/// The SCS SDK exposes a 32 KiB mapping. The Windows Ajazz backend (com.hsgaming.scsmon)
/// reads the same names, so this app does not need to modify that plugin.
/// Offset reference: scs-telemetry-common.hpp REV12.
/// </summary>
public sealed class TelemetryReader : IDisposable
{
    private const int MapLength = 32768;

    private const int O_SDK = 0;
    private const int O_PAUSED = 4;
    private const int O_GAME = 52;
    private const int O_TIME_ABS = 64;

    private const int O_SPEED = 948;
    private const int O_RPM = 952;
    private const int O_CRUISE = 988;
    private const int O_FUEL = 1000;
    private const int O_CONS = 1004;
    private const int O_RANGE = 1008;
    private const int O_ROUTED = 1060;
    private const int O_ROUTET = 1064;
    private const int O_LIMIT = 1068;
    private const int O_PLANNED = 100;

    private const int O_FCAP = 704;
    private const int O_RPMMAX = 740;
    private const int O_GEAR = 504;

    private const int O_TRUCKBRAND = 2364;
    private const int O_TRUCKID = 2428;
    private const int O_TRUCKNAME = 2492;
    private const int O_CARGOID = 2556;
    private const int O_CARGO = 2620;
    private const int O_CARGOMASS = 748;
    private const int O_CARGOLOADED = 1564;
    private const int O_SPECIALJOB = 1565;

    private const int O_POSX = 2200;
    private const int O_POSY = 2208;
    private const int O_POSZ = 2216;
    private const int O_HEADING = 2224;
    private const int O_CITYDST = 2748;
    private const int O_CITYSRC = 3004;

    private const int O_PARK = 1566;
    private const int O_MOTOR = 1567;
    // REV12: truck_ui.retarderBrake (unsigned int) at offset 108.
    private const int O_RETARDER = 108;
    private const int O_FWARN = 1570;
    private const int O_ENG = 1576;
    private const int O_WIPERS = 1577;
    private const int O_BL = 1580;
    private const int O_BR = 1581;
    private const int O_PARKING = 1582;
    private const int O_LO = 1583;
    private const int O_HI = 1584;
    private const int O_BEACON = 1585;
    private const int O_BRK = 1586;
    private const int O_REV = 1587;
    private const int O_HAZ = 1588;
    private const int O_CRZON = 1589;
    private const int O_DIFF = 1608;

    private const int O_AIR = 992;
    private const int O_OILP = 1016;
    private const int O_OILTEMP = 1020;
    private const int O_WATERTEMP = 1024;
    private const int O_ODOMETER = 1056;
    private const int O_BATT = 1028;
    private const int O_AIRWARN = 720;
    private const int O_AIREMERG = 724;
    private const int O_OILWARN = 728;
    private const int O_WATERWARN = 732;
    private const int O_BATTWARN = 736;

    private const int O_WENG = 1036;
    private const int O_WTRA = 1040;
    private const int O_WCAB = 1044;
    private const int O_WCHA = 1048;
    private const int O_WWHE = 1052;

    private const int O_TCD = 6152;
    private const int O_TCHA = 6156;
    private const int O_TWHE = 6160;
    private const int O_TBOD = 6164;
    private const int O_JCD = 1468;
    private const int O_TATT = 6080;
    private const int O_ONJOB = 4300;

    private const int O_JOBINCOME = 4000;
    private const int O_JOBCANCELLED = 4200;
    private const int O_JOBDELIVERED = 4208;
    private const int O_FINE = 4216;
    private const int O_TOLL = 4224;
    private const int O_FERRY = 4232;
    private const int O_TRAIN = 4240;

    private static readonly string[] MapNames =
    {
        @"Local\SCSTelemetry",
        "SCSTelemetry",
        @"Global\SCSTelemetry"
    };

    private readonly bool _demoMode;
    private readonly DateTime _demoStart = DateTime.UtcNow;
    private readonly BankBalanceReader _bankReader = new();
    private readonly WeatherReader _weatherReader = new();

    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _view;
    private DateTime _lastOpenAttempt = DateTime.MinValue;

    public TelemetryReader(bool demoMode)
    {
        _demoMode = demoMode;
    }

    /// <summary>
    /// Clima manual. Se usa cuando <see cref="WeatherAuto"/> es false o
    /// cuando el lector no puede acceder a la memoria del juego.
    /// </summary>
    public WeatherCondition Weather { get; set; } = WeatherCondition.Clear;

    /// <summary>
    /// Si es true, el panel toma el clima de la humedad de la carretera que
    /// mantiene el juego en memoria. Si la lectura falla, usa <see cref="Weather"/>.
    /// </summary>
    public bool WeatherAuto { get; set; } = true;

    public Snap Snapshot()
    {
        if (_demoMode)
        {
            return ApplyWeather(DemoSnapshot());
        }

        if (_view == null && (DateTime.UtcNow - _lastOpenAttempt).TotalSeconds >= 1.0)
        {
            _lastOpenAttempt = DateTime.UtcNow;
            TryOpen();
        }

        var snap = ApplyWeather(GetSnap());
        if (snap.Active)
        {
            _bankReader.SetGame(snap.Ats);
            if (_bankReader.CurrentFor(snap.Ats) is { } save)
            {
                snap.BankBalance = save.BankBalance;
                snap.CompanyName = save.CompanyName;
                snap.CompanyLogo = save.CompanyLogo;
                snap.DriverIncome = save.DriverIncome;
                snap.HiredDrivers = save.HiredDrivers;
            }
        }

        if (!snap.Active && _view != null)
        {
            CloseMap();
        }

        return snap;
    }

    private Snap ApplyWeather(Snap snap)
    {
        if (WeatherAuto && snap.Active && !snap.Demo)
        {
            snap.Weather = _weatherReader.Read(snap.Ats) ?? Weather;
        }
        else
        {
            snap.Weather = Weather;
        }

        return snap;
    }

    private bool TryOpen()
    {
        foreach (var name in MapNames)
        {
            try
            {
                var mmf = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
                var view = mmf.CreateViewAccessor(0, MapLength, MemoryMappedFileAccess.Read);

                if (view.ReadByte(O_SDK) != 0)
                {
                    _mmf = mmf;
                    _view = view;
                    return true;
                }

                view.Dispose();
                mmf.Dispose();
            }
            catch
            {
                // The game may not be running, or that name may not exist.
            }
        }

        return false;
    }

    private void CloseMap()
    {
        try { _view?.Dispose(); } catch { }
        try { _mmf?.Dispose(); } catch { }
        _view = null;
        _mmf = null;
    }

    private Snap GetSnap()
    {
        var snap = new Snap();
        var view = _view;
        if (view == null)
        {
            return snap;
        }

        try
        {
            if (view.ReadByte(O_SDK) == 0)
            {
                return snap;
            }

            snap.Active = true;
            snap.Paused = view.ReadByte(O_PAUSED) != 0;
            snap.Ats = view.ReadUInt32(O_GAME) == 2;
            snap.GameTime = view.ReadUInt32(O_TIME_ABS);

            snap.Speed = view.ReadSingle(O_SPEED);
            snap.Rpm = view.ReadSingle(O_RPM);
            snap.Gear = view.ReadInt32(O_GEAR);
            snap.Fuel = view.ReadSingle(O_FUEL);
            snap.FuelCap = view.ReadSingle(O_FCAP);
            snap.Range = view.ReadSingle(O_RANGE);
            snap.Cons = view.ReadSingle(O_CONS);
            snap.RpmMax = view.ReadSingle(O_RPMMAX);
            snap.Cruise = view.ReadSingle(O_CRUISE);
            snap.CruiseOn = view.ReadByte(O_CRZON) != 0;
            snap.Limit = view.ReadSingle(O_LIMIT);

            snap.TruckBrand = ReadString(view, O_TRUCKBRAND);
            snap.TruckId = ReadString(view, O_TRUCKID);
            snap.TruckName = ReadString(view, O_TRUCKNAME);
            snap.CargoId = ReadString(view, O_CARGOID);
            snap.CargoName = ReadString(view, O_CARGO);
            snap.CargoMass = view.ReadSingle(O_CARGOMASS);
            snap.CargoLoaded = view.ReadByte(O_CARGOLOADED) != 0;
            snap.SpecialJob = view.ReadByte(O_SPECIALJOB) != 0;

            snap.PosX = view.ReadDouble(O_POSX);
            snap.PosY = view.ReadDouble(O_POSY);
            snap.PosZ = view.ReadDouble(O_POSZ);
            snap.Heading = view.ReadDouble(O_HEADING);
            snap.CityDst = ReadString(view, O_CITYDST);
            snap.CitySrc = ReadString(view, O_CITYSRC);

            snap.LowB = view.ReadByte(O_LO) != 0;
            snap.HighB = view.ReadByte(O_HI) != 0;
            snap.BlinkL = view.ReadByte(O_BL) != 0;
            snap.BlinkR = view.ReadByte(O_BR) != 0;
            snap.Brake = view.ReadByte(O_BRK) != 0;
            snap.Park = view.ReadByte(O_PARK) != 0;
            snap.Motor = view.ReadByte(O_MOTOR) != 0;
            snap.Retarder = view.ReadUInt32(O_RETARDER) > 0;
            snap.Engine = view.ReadByte(O_ENG) != 0;
            snap.Parking = view.ReadByte(O_PARKING) != 0;
            snap.Hazard = view.ReadByte(O_HAZ) != 0;
            snap.FuelWarn = view.ReadByte(O_FWARN) != 0;
            snap.Beacon = view.ReadByte(O_BEACON) != 0;
            snap.DiffLock = view.ReadByte(O_DIFF) != 0;
            snap.Wipers = view.ReadByte(O_WIPERS);

            snap.AirPressure = view.ReadSingle(O_AIR);
            snap.OilPressure = view.ReadSingle(O_OILP);
            snap.OilTemp = view.ReadSingle(O_OILTEMP);
            snap.WaterTemp = view.ReadSingle(O_WATERTEMP);
            snap.Odometer = view.ReadSingle(O_ODOMETER);
            snap.BatteryVoltage = view.ReadSingle(O_BATT);
            snap.AirWarn = view.ReadSingle(O_AIRWARN);
            snap.AirEmergency = view.ReadSingle(O_AIREMERG);
            snap.OilWarn = view.ReadSingle(O_OILWARN);
            snap.WaterWarn = view.ReadSingle(O_WATERWARN);
            snap.BatteryWarn = view.ReadSingle(O_BATTWARN);

            snap.OnJob = view.ReadByte(O_ONJOB) != 0;
            snap.JobIncome = view.ReadUInt64(O_JOBINCOME);
            snap.JobDeliveredRevenue = view.ReadInt64(O_JOBDELIVERED);
            snap.JobCancelledPenalty = view.ReadInt64(O_JOBCANCELLED);
            snap.FineAmount = view.ReadInt64(O_FINE);
            snap.TollgatePay = view.ReadInt64(O_TOLL);
            snap.FerryPay = view.ReadInt64(O_FERRY);
            snap.TrainPay = view.ReadInt64(O_TRAIN);
            snap.Planned = view.ReadUInt32(O_PLANNED);
            snap.RouteD = view.ReadSingle(O_ROUTED);
            snap.RouteT = view.ReadSingle(O_ROUTET);

            snap.EngineDmg = view.ReadSingle(O_WENG) * 100f;
            var maxTruck = Math.Max(
                Math.Max(view.ReadSingle(O_WENG), view.ReadSingle(O_WTRA)),
                Math.Max(
                    Math.Max(view.ReadSingle(O_WCAB), view.ReadSingle(O_WCHA)),
                    view.ReadSingle(O_WWHE)));
            snap.TruckDmg = maxTruck * 100f;

            var hasTrailer = view.ReadByte(O_TATT) != 0;
            if (hasTrailer)
            {
                var maxTrailer = Math.Max(
                    Math.Max(view.ReadSingle(O_TCHA), view.ReadSingle(O_TWHE)),
                    view.ReadSingle(O_TBOD));
                snap.TrailerDmg = maxTrailer * 100f;
                snap.CargoDmg = Math.Max(view.ReadSingle(O_TCD), view.ReadSingle(O_JCD)) * 100f;
            }
        }
        catch
        {
            snap.Active = false;
        }

        return snap;
    }

    private static string? ReadString(MemoryMappedViewAccessor view, int offset, int maxLength = 64)
    {
        var buffer = new byte[maxLength];
        var length = 0;

        for (; length < maxLength; length++)
        {
            var value = view.ReadByte(offset + length);
            if (value == 0)
            {
                break;
            }
            buffer[length] = value;
        }

        if (length == 0)
        {
            return null;
        }

        var text = Encoding.UTF8.GetString(buffer, 0, length).Trim();
        return text.Length == 0 ? null : text;
    }

    private Snap DemoSnapshot()
    {
        var t = (DateTime.UtcNow - _demoStart).TotalSeconds;

        return new Snap
        {
            Active = true,
            Demo = true,
            Paused = false,
            Ats = true,
            Engine = true,
            Motor = true,
            Retarder = true,
            RpmMax = 2100,
            FuelCap = 600,
            GameTime = (uint)((6 * 60 + (int)(t * 30)) % (24 * 60)),

            Rpm = (float)Math.Clamp(1450 + (Math.Sin(t * 0.9) * 0.5 + Math.Sin(t * 2.1) * 0.08) * 520, 550, 2050),
            TruckBrand = "Kenworth",
            TruckId = "vehicle.kenworth.t800_gtm",
            TruckName = "T800",
            CargoId = "bulldozer",
            CargoName = "Buldócer",
            CargoMass = 18_189.1f,
            CargoLoaded = true,
            SpecialJob = false,
            Fuel = (float)Math.Max(90, 320 - t * 0.03),
            Range = (float)Math.Max(120, 740 - t * 0.06),
            Speed = 22.2f,
            Gear = 10,
            Cons = 0.00028f,
            Cruise = 20.8f,
            Limit = 27.7f,
            CruiseOn = true,
            LowB = true,
            HighB = true,
            Parking = true,
            BlinkL = true,
            BlinkR = false,
            Hazard = false,
            Brake = false,
            Park = true,
            Beacon = true,
            Wipers = 1,
            DiffLock = true,
            TruckDmg = 12,
            EngineDmg = 18,
            TrailerDmg = 45,
            CargoDmg = 80,
            AirPressure = 118,
            OilPressure = 60,
            OilTemp = 103,
            WaterTemp = 92,
            Odometer = 4524.6f + (float)(t * 0.01),
            BatteryVoltage = 26.8f,
            AirWarn = 59.16f,
            AirEmergency = 29.58f,
            OilWarn = 14,
            WaterWarn = 95,
            BatteryWarn = 23.76f,
            OnJob = true,
            JobIncome = 94_144,
            JobDeliveredRevenue = 173_664,
            JobCancelledPenalty = 0,
            FineAmount = 0,
            TollgatePay = 1_250,
            FerryPay = 0,
            TrainPay = 0,
            BankBalance = 5_341_147,
            CompanyName = "FLACO INC",
            CompanyLogo = "logo_4",
            DriverIncome = 4_384_955,
            HiredDrivers = 33,
            Planned = 350,
            PosX = 1234.5 + t * 0.8,
            PosY = 0,
            PosZ = -5678.9 - t * 0.4,
            Heading = (0.12 + t * 0.012) % 1.0,
            CityDst = "Los Angeles",
            CitySrc = "Phoenix",
            RouteD = 123_000,
            RouteT = 5700
        };
    }

    public void Dispose()
    {
        CloseMap();
        _weatherReader.Dispose();
        _bankReader.Dispose();
    }
}
