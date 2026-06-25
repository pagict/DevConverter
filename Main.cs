using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.DevConvert;

public sealed class Main : IPlugin, IPluginI18n
{
    private const string PluginName = "DevConvert";
    private static readonly string IconPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "Images", "devconvert.png");

    public string Name => PluginName;
    public string Description => "Developer conversions: timestamp/date, endian, number base, IPv4.";

    public void Init(PluginInitContext context)
    {
    }

    public string GetTranslatedPluginTitle() => Name;

    public string GetTranslatedPluginDescription() => Description;

    public List<Result> Query(Query query)
    {
        var input = (query.Search ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return HelpResults();
        }

        try
        {
            var results = Convert(input).ToList();
            return results.Count == 0 ? ErrorResult("Unknown command", "Try: ts/date/h2n/n2h/d2b/b2d/ip2int/ip2hex/hex2ip/int2ip") : results;
        }
        catch (Exception ex)
        {
            return ErrorResult("Convert failed", ex.Message);
        }
    }

    private static IEnumerable<Result> Convert(string input)
    {
        var parts = SplitFirstToken(input);
        var cmd = parts.Command.ToLowerInvariant();
        var arg = parts.Rest.Trim();

        switch (cmd)
        {
            case "help":
            case "?":
                return HelpResults();

            case "ts":
            case "unix":
            case "unix_ts_2_date_str":
                return UnixToDate(arg, milliseconds: null);

            case "tsms":
                return UnixToDate(arg, milliseconds: true);

            case "date":
            case "date_str_2_unix_ts":
                return DateToUnix(arg, assumeUtc: false);

            case "dateutc":
                return DateToUnix(arg, assumeUtc: true);

            case "h2n":
            case "host2net":
            case "n2h":
            case "net2host":
                return SwapEndianAuto(arg, cmd);

            case "swap16":
                return SwapEndianFixed(arg, 16);
            case "swap32":
                return SwapEndianFixed(arg, 32);
            case "swap64":
                return SwapEndianFixed(arg, 64);

            case "d2b":
            case "dec2bin":
                return BaseConvert(arg, 10, 2);
            case "b2d":
            case "bin2dec":
                return BaseConvert(arg, 2, 10);
            case "d2h":
            case "dec2hex":
                return BaseConvert(arg, 10, 16);
            case "h2d":
            case "hex2dec":
                return BaseConvert(arg, 16, 10);
            case "h2b":
            case "hex2bin":
                return BaseConvert(arg, 16, 2);
            case "b2h":
            case "bin2hex":
                return BaseConvert(arg, 2, 16);

            case "ip":
            case "ipv4":
                return Ipv4Auto(arg);
            case "ip2int":
            case "ipv4_2_int":
                return Ipv4ToInt(arg);
            case "ip2hex":
            case "ipv4_2_hex":
                return Ipv4ToHex(arg);
            case "int2ip":
            case "int_2_ipv4":
                return IntToIpv4(arg);
            case "hex2ip":
            case "hex_2_ipv4":
                return HexToIpv4(arg);

            default:
                return AutoConvert(input);
        }
    }

    private static IEnumerable<Result> UnixToDate(string arg, bool? milliseconds)
    {
        if (!long.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw))
            return ErrorResult("Invalid timestamp", "Example: dev ts 1719302400 or dev tsms 1719302400000");

        var isMs = milliseconds ?? Math.Abs(raw) >= 100_000_000_000L;
        var dto = isMs ? DateTimeOffset.FromUnixTimeMilliseconds(raw) : DateTimeOffset.FromUnixTimeSeconds(raw);
        var local = dto.ToLocalTime();
        var utc = dto.ToUniversalTime();
        var value = local.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);

        return new[]
        {
            MakeResult(value, $"UTC: {utc:yyyy-MM-dd HH:mm:ss}Z | Unix seconds: {utc.ToUnixTimeSeconds()} | Unix ms: {utc.ToUnixTimeMilliseconds()}", value),
            MakeResult(utc.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture), $"Local: {value}", utc.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture))
        };
    }

    private static IEnumerable<Result> DateToUnix(string arg, bool assumeUtc)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return ErrorResult("Missing date string", "Example: dev date 2026-06-25 17:30:00");

        var styles = assumeUtc ? DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal : DateTimeStyles.AssumeLocal;
        if (!DateTimeOffset.TryParse(arg, CultureInfo.CurrentCulture, styles, out var dto) &&
            !DateTimeOffset.TryParse(arg, CultureInfo.InvariantCulture, styles, out dto))
        {
            return ErrorResult("Invalid date string", "Example: dev date 2026-06-25 17:30:00 or dev dateutc 2026-06-25T09:30:00Z");
        }

        var unix = dto.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var ms = dto.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        return new[]
        {
            MakeResult(unix, $"Unix seconds | Local: {dto.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz} | UTC: {dto.ToUniversalTime():yyyy-MM-dd HH:mm:ss}Z", unix),
            MakeResult(ms, "Unix milliseconds", ms)
        };
    }

    private static IEnumerable<Result> SwapEndianAuto(string arg, string cmd)
    {
        var value = ParseUnsigned(arg, 64);
        var bits = value <= 0xFFFFUL ? 16 : value <= 0xFFFFFFFFUL ? 32 : 64;
        return SwapEndianFixed(arg, bits, cmd);
    }

    private static IEnumerable<Result> SwapEndianFixed(string arg, int bits, string? label = null)
    {
        var value = ParseUnsigned(arg, bits);
        ulong swapped = bits switch
        {
            16 => SwapBytes(value, 2),
            32 => SwapBytes(value, 4),
            64 => SwapBytes(value, 8),
            _ => throw new ArgumentOutOfRangeException(nameof(bits))
        };

        var title = FormatHex(swapped, bits);
        var subtitle = $"{label ?? "swap"} uint{bits}: {FormatHex(value, bits)} -> {title} | dec: {swapped}";
        return new[] { MakeResult(title, subtitle, title) };
    }

    private static IEnumerable<Result> BaseConvert(string arg, int fromBase, int toBase)
    {
        var value = ParseUnsigned(arg, 64, fromBase);
        var title = FormatBase(value, toBase);
        var subtitle = $"dec: {value} | hex: {FormatHex(value, MinimalBits(value))} | bin: 0b{Convert.ToString((long)value, 2)}";
        return new[] { MakeResult(title, subtitle, title) };
    }

    private static IEnumerable<Result> Ipv4Auto(string arg)
    {
        if (IPAddress.TryParse(arg, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return Ipv4Results(ip);
        }

        return NumberToIpv4Results(arg);
    }

    private static IEnumerable<Result> Ipv4ToInt(string arg)
    {
        var ip = ParseIpv4(arg);
        var n = Ipv4ToUInt32(ip);
        return new[] { MakeResult(n.ToString(CultureInfo.InvariantCulture), $"{ip} -> int", n.ToString(CultureInfo.InvariantCulture)) };
    }

    private static IEnumerable<Result> Ipv4ToHex(string arg)
    {
        var ip = ParseIpv4(arg);
        var n = Ipv4ToUInt32(ip);
        var hex = "0x" + n.ToString("X8", CultureInfo.InvariantCulture);
        return new[] { MakeResult(hex, $"{ip} -> hex", hex) };
    }

    private static IEnumerable<Result> IntToIpv4(string arg) => NumberToIpv4Results(arg, forceBase: 10);

    private static IEnumerable<Result> HexToIpv4(string arg) => NumberToIpv4Results(arg, forceBase: 16);

    private static IEnumerable<Result> NumberToIpv4Results(string arg, int? forceBase = null)
    {
        var n = ParseUnsigned(arg, 32, forceBase);
        var ip = UInt32ToIpv4((uint)n);
        return new[] { MakeResult(ip.ToString(), $"int: {n} | hex: 0x{n:X8}", ip.ToString()) };
    }

    private static IEnumerable<Result> Ipv4Results(IPAddress ip)
    {
        var n = Ipv4ToUInt32(ip);
        return new[]
        {
            MakeResult(n.ToString(CultureInfo.InvariantCulture), $"{ip} -> int", n.ToString(CultureInfo.InvariantCulture)),
            MakeResult("0x" + n.ToString("X8", CultureInfo.InvariantCulture), $"{ip} -> hex", "0x" + n.ToString("X8", CultureInfo.InvariantCulture))
        };
    }

    private static IEnumerable<Result> AutoConvert(string input)
    {
        var results = new List<Result>();

        if (IPAddress.TryParse(input, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            results.AddRange(Ipv4Results(ip));

        if (TryParseUnsigned(input, out var n))
        {
            results.Add(MakeResult(FormatHex(n, MinimalBits(n)), $"dec: {n}", FormatHex(n, MinimalBits(n))));
            results.Add(MakeResult("0b" + Convert.ToString((long)n, 2), $"dec: {n}", "0b" + Convert.ToString((long)n, 2)));
            if (n <= uint.MaxValue)
                results.AddRange(NumberToIpv4Results(n.ToString(CultureInfo.InvariantCulture)));
        }

        if (long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ts) && Math.Abs(ts) is >= 1_000_000_000 and <= 9_999_999_999_999)
            results.AddRange(UnixToDate(input, null));

        return results;
    }

    private static (string Command, string Rest) SplitFirstToken(string s)
    {
        var i = s.IndexOf(' ');
        return i < 0 ? (s, string.Empty) : (s[..i], s[(i + 1)..]);
    }

    private static IPAddress ParseIpv4(string s)
    {
        if (IPAddress.TryParse(s.Trim(), out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            return ip;
        throw new ArgumentException("Invalid IPv4 address. Example: 192.168.1.1");
    }

    private static uint Ipv4ToUInt32(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static IPAddress UInt32ToIpv4(uint n)
    {
        return new IPAddress(new[]
        {
            (byte)((n >> 24) & 0xff),
            (byte)((n >> 16) & 0xff),
            (byte)((n >> 8) & 0xff),
            (byte)(n & 0xff)
        });
    }

    private static ulong SwapBytes(ulong value, int bytes)
    {
        ulong result = 0;
        for (var i = 0; i < bytes; i++)
        {
            result = (result << 8) | (value & 0xFFUL);
            value >>= 8;
        }
        return result;
    }

    private static ulong ParseUnsigned(string s, int bits, int? forceBase = null)
    {
        if (!TryParseUnsigned(s, out var value, forceBase))
            throw new ArgumentException("Invalid number. Supported: decimal, 0xHEX, 0bBIN.");

        if (bits < 64 && value > ((1UL << bits) - 1))
            throw new ArgumentOutOfRangeException(nameof(s), $"Value exceeds uint{bits} range.");
        return value;
    }

    private static bool TryParseUnsigned(string s, out ulong value, int? forceBase = null)
    {
        s = s.Trim().Replace("_", "");
        var numberBase = forceBase ?? 10;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            numberBase = 16;
            s = s[2..];
        }
        else if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
        {
            numberBase = 2;
            s = s[2..];
        }
        else if (forceBase == 16 && s.StartsWith("#"))
        {
            s = s[1..];
        }

        try
        {
            value = numberBase switch
            {
                2 => Convert.ToUInt64(s, 2),
                10 => ulong.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture),
                16 => ulong.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                _ => throw new ArgumentOutOfRangeException()
            };
            return true;
        }
        catch
        {
            value = 0;
            return false;
        }
    }

    private static int MinimalBits(ulong value) => value <= 0xFFFF ? 16 : value <= 0xFFFFFFFF ? 32 : 64;

    private static string FormatBase(ulong value, int toBase) => toBase switch
    {
        2 => "0b" + Convert.ToString((long)value, 2),
        10 => value.ToString(CultureInfo.InvariantCulture),
        16 => FormatHex(value, MinimalBits(value)),
        _ => throw new ArgumentOutOfRangeException(nameof(toBase))
    };

    private static string FormatHex(ulong value, int bits)
    {
        var width = bits switch { 16 => 4, 32 => 8, 64 => 16, _ => 0 };
        return "0x" + value.ToString("X" + width, CultureInfo.InvariantCulture);
    }

    private static Result MakeResult(string title, string subtitle, string copyText)
    {
        return new Result
        {
            Title = title,
            SubTitle = subtitle + " | Enter: copy",
            IcoPath = IconPath,
            QueryTextDisplay = copyText,
            Score = 100,
            Action = _ =>
            {
                Clipboard.SetText(copyText);
                return true;
            }
        };
    }

    private static List<Result> HelpResults() => new()
    {
        MakeResult("dev ts 1719302400", "Unix timestamp -> local/UTC date", "dev ts 1719302400"),
        MakeResult("dev date 2026-06-25 17:30:00", "Local date string -> Unix seconds/ms", "dev date 2026-06-25 17:30:00"),
        MakeResult("dev h2n 0x12345678", "host/network byte order, auto uint16/32/64", "dev h2n 0x12345678"),
        MakeResult("dev d2b 42 / b2d 101010 / h2d 0x2A", "base conversions", "dev d2b 42"),
        MakeResult("dev ip 192.168.1.1 / ip2int / ip2hex / int2ip / hex2ip", "IPv4 decimal-dot <-> int/hex", "dev ip 192.168.1.1")
    };

    private static List<Result> ErrorResult(string title, string subtitle) => new()
    {
        new Result
        {
            Title = title,
            SubTitle = subtitle,
            IcoPath = IconPath,
            Score = 1
        }
    };
}
