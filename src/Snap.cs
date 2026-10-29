namespace ATSTelemetry;

public enum WeatherCondition
{
    Clear,
    Cloudy,
    Rain,
    Storm
}

/// <summary>
/// One frame of telemetry. The fields mirror the macOS Snap model so the
/// Windows renderer can show exactly the same information.
/// </summary>
public sealed class Snap
{
    public bool Active { get; set; }
    public bool Paused { get; set; }
    public bool Ats { get; set; }
    public bool Demo { get; set; }

    public float? Speed { get; set; }
    public float? Rpm { get; set; }
    public float? Fuel { get; set; }
    public float? FuelCap { get; set; }
    public float? Range { get; set; }
    public float? Cons { get; set; }
    public float? Cruise { get; set; }
    public float? Limit { get; set; }
    public float? RpmMax { get; set; }

    public string? TruckBrand { get; set; }
    public string? TruckId { get; set; }
    public string? TruckName { get; set; }
    public string? CargoId { get; set; }
    public string? CargoName { get; set; }
    public float? CargoMass { get; set; }
    public bool CargoLoaded { get; set; }
    public bool SpecialJob { get; set; }

    public float? RouteD { get; set; }
    public float? RouteT { get; set; }
    public uint? Planned { get; set; }
    public int? Gear { get; set; }
    public uint? GameTime { get; set; }
    public WeatherCondition Weather { get; set; } = WeatherCondition.Clear;

    public double? PosX { get; set; }
    public double? PosY { get; set; }
    public double? PosZ { get; set; }
    public double? Heading { get; set; }
    public string? CityDst { get; set; }
    public string? CitySrc { get; set; }

    public bool Park { get; set; }
    public bool Motor { get; set; }
    public bool Retarder { get; set; }
    public bool BlinkL { get; set; }
    public bool BlinkR { get; set; }
    public bool LowB { get; set; }
    public bool HighB { get; set; }
    public bool Brake { get; set; }
    public bool Engine { get; set; }
    public bool CruiseOn { get; set; }
    public bool Parking { get; set; }
    public bool Hazard { get; set; }
    public bool FuelWarn { get; set; }
    public bool OnJob { get; set; }
    public bool Beacon { get; set; }
    public bool DiffLock { get; set; }
    public int Wipers { get; set; }

    public float? TruckDmg { get; set; }
    public float? EngineDmg { get; set; }
    public float? TrailerDmg { get; set; }
    public float? CargoDmg { get; set; }

    public float? AirPressure { get; set; }
    public float? OilPressure { get; set; }
    public float? OilTemp { get; set; }
    public float? WaterTemp { get; set; }
    public float? Odometer { get; set; }
    public float? BatteryVoltage { get; set; }
    public float? AirWarn { get; set; }
    public float? AirEmergency { get; set; }
    public float? OilWarn { get; set; }
    public float? WaterWarn { get; set; }
    public float? BatteryWarn { get; set; }

    public ulong? JobIncome { get; set; }
    public long? JobDeliveredRevenue { get; set; }
    public long? JobCancelledPenalty { get; set; }
    public long? FineAmount { get; set; }
    public long? TollgatePay { get; set; }
    public long? FerryPay { get; set; }
    public long? TrainPay { get; set; }

    /// <summary>
    /// REV12 telemetry does not expose the bank account, the company name or
    /// the hired-driver income. These values come from the newest savegame and
    /// profile of the active game (ATS or ETS2), refreshed by BankBalanceReader.
    /// </summary>
    public long? BankBalance { get; set; }

    public string? CompanyName { get; set; }

    /// <summary>
    /// Identificador del logotipo elegido en el perfil del juego (por ejemplo,
    /// "logo_4"). Se usa para cargar la imagen real de Assets\CompanyLogos.
    /// </summary>
    public string? CompanyLogo { get; set; }

    public long? DriverIncome { get; set; }

    public int? HiredDrivers { get; set; }
}
