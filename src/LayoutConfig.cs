using System;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace ATSTelemetry;

public sealed class LayoutConfig
{
    public float CompanyX { get; set; } = 64;
    public float CompanyY { get; set; } = 116;
    public float CompanyW { get; set; } = 400;
    public float CompanyH { get; set; } = 380;

    public float CargoX { get; set; } = 64;
    public float CargoY { get; set; } = 512;
    public float CargoW { get; set; } = 400;
    public float CargoH { get; set; } = 424;

    public float DamageX { get; set; } = 1456;
    public float DamageY { get; set; } = 116;
    public float DamageW { get; set; } = 400;
    public float DamageH { get; set; } = 380;

    public float GpsX { get; set; } = 1456;
    public float GpsY { get; set; } = 512;
    public float GpsW { get; set; } = 400;
    public float GpsH { get; set; } = 424;

    public float FuelX { get; set; } = 486;
    public float FuelY { get; set; } = 794;
    public float FuelW { get; set; } = 240;
    public float FuelH { get; set; } = 142;

    public float AlertsX { get; set; } = 742;
    public float AlertsY { get; set; } = 794;
    public float AlertsW { get; set; } = 438;
    public float AlertsH { get; set; } = 142;

    public float WeatherX { get; set; } = 1196;
    public float WeatherY { get; set; } = 794;
    public float WeatherW { get; set; } = 237;
    public float WeatherH { get; set; } = 142;

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ATSTelemetry", "layout.json");

    public static LayoutConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<LayoutConfig>(json) ?? new LayoutConfig();
            }
        }
        catch { }
        return new LayoutConfig();
    }

    public static void Save(LayoutConfig cfg)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }

    public RectangleF CompanyRect => new(CompanyX, CompanyY, CompanyW, CompanyH);
    public RectangleF CargoRect => new(CargoX, CargoY, CargoW, CargoH);
    public RectangleF DamageRect => new(DamageX, DamageY, DamageW, DamageH);
    public RectangleF GpsRect => new(GpsX, GpsY, GpsW, GpsH);
    public RectangleF FuelRect => new(FuelX, FuelY, FuelW, FuelH);
    public RectangleF AlertsRect => new(AlertsX, AlertsY, AlertsW, AlertsH);
    public RectangleF WeatherRect => new(WeatherX, WeatherY, WeatherW, WeatherH);
}