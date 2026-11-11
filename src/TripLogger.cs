using System.IO;
using System.Text;

namespace ATSTelemetry;

public static class TripLogger
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ATSTelemetry");
    private static readonly string CsvPath = Path.Combine(LogDir, "trips.csv");

    public static string FilePath => CsvPath;

    public static void EnsureHeader()
    {
        if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
        if (!File.Exists(CsvPath))
        {
            File.WriteAllText(CsvPath,
                "Inicio,DuracionMin,Origen,Destino,DistanciaKm,Ingreso,CombustibleL,ConsumoL100\n",
                new UTF8Encoding(false));
        }
    }

    public static void Append(string origin, string destination, double distanceKm,
        double income, double fuelLiters, double consumptionL100, TimeSpan duration)
    {
        EnsureHeader();
        var line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:yyyy-MM-dd HH:mm},{1:F1},{2},{3},{4:F1},{5:F2},{6:F1},{7:F2}\n",
            DateTime.Now, duration.TotalMinutes,
            Csv(origin), Csv(destination), distanceKm, income, fuelLiters, consumptionL100);
        File.AppendAllText(CsvPath, line, new UTF8Encoding(false));
    }

    public static string ReadAll()
    {
        EnsureHeader();
        return File.ReadAllText(CsvPath);
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}