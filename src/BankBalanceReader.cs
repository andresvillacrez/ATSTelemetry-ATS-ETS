using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ATSTelemetry;

/// <summary>
/// Values recovered from the active savegame. Null means the save did not
/// contain that information (or the file was still being written).
/// </summary>
public sealed record SaveGameData(
    long? BankBalance,
    string? CompanyName,
    string? CompanyLogo,
    long? DriverIncome,
    int? HiredDrivers);

/// <summary>
/// Reads the player's bank balance, company name and hired-driver income from
/// the newest ATS or ETS2 savegame and profile.
///
/// REV12 telemetry does not expose these values. This class finds the newest
/// game.sii (Steam Cloud, local profiles and OneDrive), decrypts the ScsC
/// container, inflates the BSII stream and reads bank.money_account plus the
/// profit logs of the drivers hired by the player. The company name is read
/// from the profile.sii next to that savegame.
/// </summary>
public sealed class BankBalanceReader : IDisposable
{
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();
    private SaveGameData? _cached;
    private bool _cachedAts = true;
    private bool _ats = true;
    private string? _lastPath;
    private DateTime _lastWriteUtc;

    public BankBalanceReader()
    {
        _ = Task.Run(RefreshLoopAsync);
    }

    /// <summary>
    /// Selects the game whose savegame should be read. ATS and ETS2 use
    /// different profile folders and Steam app ids.
    /// </summary>
    public void SetGame(bool ats)
    {
        lock (_lock)
        {
            if (_ats == ats)
            {
                return;
            }

            _ats = ats;
            _cached = null;
            _lastPath = null;
            _lastWriteUtc = default;
        }
    }

    public SaveGameData? CurrentFor(bool ats)
    {
        lock (_lock)
        {
            return _cachedAts == ats ? _cached : null;
        }
    }

    private async Task RefreshLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                Refresh();
            }
            catch
            {
                // A save can be in the middle of being written. Try again later.
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(4), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void Refresh()
    {
        bool ats;
        lock (_lock)
        {
            ats = _ats;
        }

        var file = FindNewestSave(ats);
        if (file == null)
        {
            return;
        }

        lock (_lock)
        {
            if (_ats != ats)
            {
                return;
            }

            if (string.Equals(_lastPath, file.FullName, StringComparison.OrdinalIgnoreCase)
                && _lastWriteUtc == file.LastWriteTimeUtc)
            {
                return;
            }
        }

        var data = SiiBankParser.ReadSave(file.FullName, FindProfile(file.FullName));
        if (data == null)
        {
            return;
        }

        lock (_lock)
        {
            if (_ats != ats)
            {
                return;
            }

            _cached = data;
            _cachedAts = ats;
            _lastPath = file.FullName;
            _lastWriteUtc = file.LastWriteTimeUtc;
        }
    }

    private static string? FindProfile(string savePath)
    {
        var directory = new FileInfo(savePath).Directory;
        for (var depth = 0; depth < 4 && directory != null; depth++)
        {
            var candidate = Path.Combine(directory.FullName, "profile.sii");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static FileInfo? FindNewestSave(bool ats)
    {
        FileInfo? newest = null;

        foreach (var root in FindRoots(ats))
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(root, "game.sii", SearchOption.AllDirectories))
                {
                    if (path.Contains(".bak", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var info = new FileInfo(path);
                    if (!info.Exists)
                    {
                        continue;
                    }

                    if (newest == null || info.LastWriteTimeUtc > newest.LastWriteTimeUtc)
                    {
                        newest = info;
                    }
                }
            }
            catch
            {
                // Ignore unreadable or disappearing profile folders.
            }
        }

        return newest;
    }

    private static IEnumerable<string> FindRoots(bool ats)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var gameFolder = ats ? "American Truck Simulator" : "Euro Truck Simulator 2";
        var steamAppId = ats ? "270880" : "227300";

        void AddRoot(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                roots.Add(path);
            }
        }

        void AddProfileBase(string basePath)
        {
            if (!Directory.Exists(basePath))
            {
                return;
            }

            try
            {
                foreach (var entry in Directory.EnumerateDirectories(basePath))
                {
                    var name = Path.GetFileName(entry);
                    if (name.Equals("profiles", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("steam_profiles", StringComparison.OrdinalIgnoreCase)
                        || (name.StartsWith("steam_profiles", StringComparison.OrdinalIgnoreCase)
                            && !name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)))
                    {
                        roots.Add(entry);
                    }
                }
            }
            catch
            {
                // Ignore inaccessible Documents folders.
            }
        }

        AddProfileBase(Path.Combine(documents, gameFolder));
        AddProfileBase(Path.Combine(home, "OneDrive", "Documents", gameFolder));
        AddProfileBase(Path.Combine(home, "OneDrive", "Documentos", gameFolder));

        foreach (var steam in FindSteamRoots())
        {
            var userdata = Path.Combine(steam, "userdata");
            if (!Directory.Exists(userdata))
            {
                continue;
            }

            try
            {
                foreach (var user in Directory.EnumerateDirectories(userdata))
                {
                    AddRoot(Path.Combine(user, steamAppId, "remote", "profiles"));
                }
            }
            catch
            {
                // Ignore Steam folders that are not readable.
            }
        }

        return roots;
    }

    private static IEnumerable<string> FindSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static string? ReadRegistry(string hive, string key, string value)
        {
            try
            {
                using var baseKey = hive == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
                using var subKey = baseKey.OpenSubKey(key);
                return subKey?.GetValue(value) as string;
            }
            catch
            {
                return null;
            }
        }

        var fromCurrentUser = ReadRegistry("HKCU", @"Software\Valve\Steam", "SteamPath");
        if (!string.IsNullOrWhiteSpace(fromCurrentUser))
        {
            roots.Add(fromCurrentUser);
        }

        foreach (var key in new[]
                 {
                     @"SOFTWARE\WOW6432Node\Valve\Steam",
                     @"SOFTWARE\Valve\Steam"
                 })
        {
            var install = ReadRegistry("HKLM", key, "InstallPath");
            if (!string.IsNullOrWhiteSpace(install))
            {
                roots.Add(install);
            }
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            roots.Add(Path.Combine(programFilesX86, "Steam"));
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            roots.Add(Path.Combine(programFiles, "Steam"));
        }

        return roots.Where(Directory.Exists);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}

internal static class SiiBankParser
{
    // SCS uses the same AES key for the encrypted save container.
    private static readonly byte[] Key =
    {
        0x2a, 0x5f, 0xcb, 0x17, 0x91, 0xd2, 0x2f, 0xb6,
        0x02, 0x45, 0xb3, 0xd8, 0x36, 0x9e, 0xd0, 0xb2,
        0xc2, 0x73, 0x71, 0x56, 0x3f, 0xbf, 0x1f, 0x3c,
        0x9e, 0xdf, 0x6b, 0x11, 0x82, 0x5a, 0x5d, 0x0a
    };

    private const uint ScsCSignature = 0x4373_6353; // "ScsC"
    private const uint BsiiSignature = 0x4949_5342; // "BSII"
    private const uint SiiNSignature = 0x4e69_6953; // "SiiN"

    public static SaveGameData? ReadSave(string savePath, string? profilePath = null)
    {
        try
        {
            var data = ReadSave(File.ReadAllBytes(savePath));
            if (data == null)
            {
                return null;
            }

            var (name, logo) = ProfileInfo(profilePath);
            return data with { CompanyName = name, CompanyLogo = logo };
        }
        catch
        {
            return null;
        }
    }

    public static SaveGameData? ReadSave(byte[] bytes)
    {
        if (bytes.Length < 4)
        {
            return null;
        }

        var signature = ReadUInt32(bytes, 0);
        return signature switch
        {
            ScsCSignature => DecryptScsC(bytes) is { } decoded ? ParseBsii(decoded) : null,
            BsiiSignature => ParseBsii(bytes),
            SiiNSignature => ParsePlainText(bytes),
            _ => null
        };
    }

    public static string? CompanyName(string? profilePath)
    {
        return ProfileInfo(profilePath).Name;
    }

    public static (string? Name, string? Logo) ProfileInfo(string? profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            return (null, null);
        }

        try
        {
            var bytes = File.ReadAllBytes(profilePath);
            if (bytes.Length < 4)
            {
                return (null, null);
            }

            var signature = ReadUInt32(bytes, 0);
            var decoded = signature switch
            {
                SiiNSignature => bytes,
                ScsCSignature => DecryptScsC(bytes),
                _ => null
            };

            if (decoded == null)
            {
                return (null, null);
            }

            var text = Encoding.UTF8.GetString(decoded);
            var name = MatchProfileValue(text, "company_name");
            var logo = MatchProfileValue(text, "logo");
            return (name, logo);
        }
        catch
        {
            return (null, null);
        }
    }

    private static string? MatchProfileValue(string text, string key)
    {
        var match = Regex.Match(
            text,
            $@"\b{Regex.Escape(key)}\s*:\s*(?:""([^""]*)""|([^\s}}]+))");
        if (!match.Success)
        {
            return null;
        }

        var value = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static byte[]? DecryptScsC(byte[] encrypted)
    {
        if (encrypted.Length <= 56)
        {
            return null;
        }

        try
        {
            var iv = new byte[16];
            Buffer.BlockCopy(encrypted, 36, iv, 0, iv.Length);

            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var decrypted = decryptor.TransformFinalBlock(encrypted, 56, encrypted.Length - 56);
            return Inflate(decrypted);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? Inflate(byte[] compressed)
    {
        if (compressed.Length <= 6)
        {
            return null;
        }

        try
        {
            using var input = new MemoryStream(compressed);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            // Some save versions contain a raw DEFLATE stream.
        }

        try
        {
            using var input = new MemoryStream(compressed, 2, compressed.Length - 6);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static SaveGameData? ParsePlainText(byte[] bytes)
    {
        try
        {
            var text = Encoding.UTF8.GetString(bytes);
            var match = Regex.Match(text, @"money_account\s*:\s*(-?\d+)");
            if (!match.Success || !long.TryParse(match.Groups[1].Value, out var value))
            {
                return null;
            }

            return new SaveGameData(value, null, null, null, null);
        }
        catch
        {
            return null;
        }
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return (uint)(bytes[offset]
            | (bytes[offset + 1] << 8)
            | (bytes[offset + 2] << 16)
            | (bytes[offset + 3] << 24));
    }

    private static SaveGameData? ParseBsii(byte[] bytes)
    {
        var reader = new Reader(bytes);
        var templates = new Dictionary<uint, Template>();
        long? balance = null;
        List<string>? hiredDrivers = null;
        var driverLogs = new Dictionary<string, string>(StringComparer.Ordinal);
        var logEntries = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var entryRevenues = new Dictionary<string, long>(StringComparer.Ordinal);

        try
        {
            if (reader.ReadUInt32() != BsiiSignature)
            {
                return null;
            }

            var version = reader.ReadUInt32();
            if (version is < 1 or > 3)
            {
                return null;
            }

            while (reader.Remaining >= 4)
            {
                var blockType = reader.ReadUInt32();

                if (blockType == 0)
                {
                    var valid = reader.ReadByte() != 0;
                    if (!valid)
                    {
                        continue;
                    }

                    var structureId = reader.ReadUInt32();
                    var name = reader.ReadString();
                    var fields = new List<Field>();

                    while (true)
                    {
                        var type = reader.ReadUInt32();
                        if (type == 0)
                        {
                            break;
                        }

                        var fieldName = reader.ReadString();
                        if (type == 55)
                        {
                            // Ordinal string declarations carry their value list
                            // in the template, before any instance data.
                            var count = reader.ReadUInt32();
                            for (var i = 0u; i < count; i++)
                            {
                                reader.Skip(4);
                                _ = reader.ReadString();
                            }
                        }

                        fields.Add(new Field(fieldName, type));
                    }

                    templates.TryAdd(structureId, new Template(name, fields));
                    continue;
                }

                if (!templates.TryGetValue(blockType, out var template))
                {
                    return null;
                }

                var instanceId = reader.ReadIdentifier();

                foreach (var field in template.Fields)
                {
                    if (template.Name == "bank" && field.Name == "money_account"
                        && field.Type is 49 or 51)
                    {
                        balance = unchecked((long)reader.ReadUInt64());
                    }
                    else if (template.Name == "player" && field.Name == "drivers"
                             && field.Type is 58 or 60 or 62)
                    {
                        hiredDrivers = reader.ReadIdentifiers();
                    }
                    else if (template.Name == "driver_ai" && field.Name == "profit_log"
                             && field.Type is 57 or 59 or 61)
                    {
                        driverLogs[instanceId] = reader.ReadIdentifier();
                    }
                    else if (template.Name == "profit_log" && field.Name == "stats_data"
                             && field.Type is 58 or 60 or 62)
                    {
                        logEntries[instanceId] = reader.ReadIdentifiers();
                    }
                    else if (template.Name == "profit_log_entry" && field.Name == "revenue"
                             && field.Type is 49 or 51)
                    {
                        entryRevenues[instanceId] = unchecked((long)reader.ReadUInt64());
                    }
                    else
                    {
                        reader.SkipField(field.Type, version);
                    }
                }
            }
        }
        catch
        {
            return null;
        }

        long? driverIncome = null;
        int? hiredCount = null;
        if (hiredDrivers != null)
        {
            long total = 0;
            var matchedDrivers = 0;
            foreach (var driverId in hiredDrivers.Distinct(StringComparer.Ordinal))
            {
                if (!driverLogs.TryGetValue(driverId, out var logId)
                    || !logEntries.TryGetValue(logId, out var entries))
                {
                    continue;
                }

                matchedDrivers++;
                foreach (var entryId in entries)
                {
                    if (entryRevenues.TryGetValue(entryId, out var revenue))
                    {
                        total += revenue;
                    }
                }
            }

            driverIncome = total;
            hiredCount = matchedDrivers;
        }

        return new SaveGameData(balance, null, null, driverIncome, hiredCount);
    }

    private readonly record struct Field(string Name, uint Type);

    private sealed record Template(string Name, List<Field> Fields);

    private sealed class Reader
    {
        private readonly byte[] _bytes;
        private int _offset;

        public Reader(byte[] bytes)
        {
            _bytes = bytes;
        }

        public int Remaining => _bytes.Length - _offset;

        public byte ReadByte()
        {
            Ensure(1);
            return _bytes[_offset++];
        }

        public uint ReadUInt32()
        {
            Ensure(4);
            var value = (uint)(_bytes[_offset]
                | (_bytes[_offset + 1] << 8)
                | (_bytes[_offset + 2] << 16)
                | (_bytes[_offset + 3] << 24));
            _offset += 4;
            return value;
        }

        public ulong ReadUInt64()
        {
            var low = ReadUInt32();
            var high = ReadUInt32();
            return low | ((ulong)high << 32);
        }

        public string ReadString()
        {
            var length = ReadUInt32();
            if (length > int.MaxValue)
            {
                throw new InvalidDataException();
            }

            Ensure((int)length);
            var text = Encoding.UTF8.GetString(_bytes, _offset, (int)length);
            _offset += (int)length;
            return text;
        }

        public void Skip(int count)
        {
            Ensure(count);
            _offset += count;
        }

        public string ReadIdentifier()
        {
            var parts = ReadByte();
            var count = parts == 0xFF ? 8 : parts * 8;
            if (count == 0)
            {
                return string.Empty;
            }

            Ensure(count);
            var identifier = Convert.ToHexString(_bytes.AsSpan(_offset, count));
            _offset += count;
            return identifier;
        }

        public List<string> ReadIdentifiers()
        {
            var count = ReadUInt32();
            if (count > 1_000_000)
            {
                throw new InvalidDataException();
            }

            var identifiers = new List<string>((int)count);
            for (var i = 0u; i < count; i++)
            {
                identifiers.Add(ReadIdentifier());
            }

            return identifiers;
        }

        public void SkipIdentifier()
        {
            var parts = ReadByte();
            if (parts == 0xFF)
            {
                Skip(8);
            }
            else
            {
                Skip(parts * 8);
            }
        }

        public void SkipField(uint type, uint version)
        {
            switch (type)
            {
                case 1:
                    _ = ReadString();
                    break;
                case 2:
                {
                    var count = ReadUInt32();
                    for (var i = 0u; i < count; i++) _ = ReadString();
                    break;
                }
                case 3:
                    Skip(8);
                    break;
                case 4:
                    Skip(checked((int)ReadUInt32()) * 8);
                    break;
                case 5:
                    Skip(4);
                    break;
                case 6:
                    Skip(checked((int)ReadUInt32()) * 4);
                    break;
                case 7:
                    Skip(8);
                    break;
                case 8:
                    Skip(checked((int)ReadUInt32()) * 8);
                    break;
                case 9:
                    Skip(12);
                    break;
                case 10:
                    Skip(checked((int)ReadUInt32()) * 12);
                    break;
                case 17:
                    Skip(12);
                    break;
                case 18:
                    Skip(checked((int)ReadUInt32()) * 12);
                    break;
                case 23:
                    Skip(16);
                    break;
                case 24:
                    Skip(checked((int)ReadUInt32()) * 16);
                    break;
                case 25:
                    Skip(version == 1 ? 28 : 32);
                    break;
                case 26:
                    Skip(checked((int)ReadUInt32()) * (version == 1 ? 28 : 32));
                    break;
                case 37:
                    Skip(4);
                    break;
                case 38:
                    Skip(checked((int)ReadUInt32()) * 4);
                    break;
                case 39:
                    Skip(4);
                    break;
                case 40:
                    Skip(checked((int)ReadUInt32()) * 4);
                    break;
                case 41:
                    Skip(2);
                    break;
                case 42:
                    Skip(checked((int)ReadUInt32()) * 2);
                    break;
                case 43:
                    Skip(2);
                    break;
                case 44:
                    Skip(checked((int)ReadUInt32()) * 2);
                    break;
                case 47:
                    Skip(4);
                    break;
                case 49:
                    Skip(8);
                    break;
                case 50:
                    Skip(checked((int)ReadUInt32()) * 8);
                    break;
                case 51:
                    Skip(8);
                    break;
                case 52:
                    Skip(checked((int)ReadUInt32()) * 8);
                    break;
                case 53:
                    Skip(1);
                    break;
                case 54:
                    Skip(checked((int)ReadUInt32()));
                    break;
                case 55:
                    Skip(4);
                    break;
                case 57:
                case 59:
                case 61:
                    SkipIdentifier();
                    break;
                case 58:
                case 60:
                case 62:
                {
                    var count = ReadUInt32();
                    for (var i = 0u; i < count; i++) SkipIdentifier();
                    break;
                }
                default:
                    throw new InvalidDataException($"Unsupported SII field type {type}");
            }
        }

        private void Ensure(int count)
        {
            if (count < 0 || _offset + count > _bytes.Length)
            {
                throw new InvalidDataException();
            }
        }
    }
}
