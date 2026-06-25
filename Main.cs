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
        var arg = parts.Remainder.Trim();

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

    private static List<Result> HelpResults()
    {
        return new List<Result>
        {
            MakeResult("DevConvert commands", "ts/date/tsms/dateutc, h2n/n2h/swap16/swap32/swap64, d2b/b2d/d2h/h2d, ip/ip2int/ip2hex/int2ip/hex2ip", string.Empty),
            MakeResult("Examples", "dev ts 1719302400 | dev date 2026-06-25 17:30:00 | dev h2n 0x12345678 | dev ip 192.168.1.1", string.Empty)
        };
    }

    private static IEnumerable<Result> UnixToDate(string arg, bool? milliseconds)
    {
        if (!long.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return ErrorResult("Invalid timestamp", arg);

        DateTimeOffset dto;
        var isMs = milliseconds ?? Math.Abs(value) > 99_999_999_999;
        dto = isMs ? DateTimeOffset.FromUnixTimeMilliseconds(value) : DateTimeOffset.FromUnixTimeSeconds(value);

        var local = dto.ToLocalTime();
        return new[]
        {
            MakeResult(local.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture), "Local time", local.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)),
            MakeResult(dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture), "UTC", dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture)),
            MakeResult(dto.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), "Unix seconds", dto.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            MakeResult(dto.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), "Unix milliseconds", dto.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture))
        };
    }

    private static IEnumerable<Result> DateToUnix(string arg, bool assumeUtc)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return ErrorResult("Invalid date", "Try: dev date 2026-06-25 17:30:00");

        DateTimeOffset dto;
        if (DateTimeOffset.TryParse(arg, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedOffset))
        {
            dto = assumeUtc ? parsedOffset.ToUniversalTime() : parsedOffset;
        }
        else if (DateTime.TryParse(arg, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            dto = assumeUtc
                ? new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc))
                : new DateTimeOffset(parsed);
        }
        else
        {
            return ErrorResult("Invalid date", arg);
        }

        return new[]
        {
            MakeResult(dto.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), "Unix seconds", dto.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            MakeResult(dto.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), "Unix milliseconds", dto.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)),
            MakeResult(dto.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture), "Local time", dto.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)),
            MakeResult(dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture), "UTC", dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture))
        };
    }

    private static IEnumerable<Result> SwapEndianAuto(string arg, string cmd)
    {
        if (!TryParseUnsigned(arg, out var value))
            return ErrorResult("Invalid integer", arg);

        var bits = MinimalBits(value);
        return SwapEndianFixedValue(value, bits, cmd);
    }

    private static IEnumerable<Result> SwapEndianFixed(string arg, int bits)
    {
        if (!TryParseUnsigned(arg, out var value))
            return ErrorResult("Invalid integer", arg);
        return SwapEndianFixedValue(value, bits, "swap" + bits);
    }

    private static IEnumerable<Result> SwapEndianFixedValue(ulong value, int bits, string cmd)
    {
        var swapped = bits switch
        {
            16 => (ulong)IPAddress.HostToNetworkOrder((short)value) & 0xFFFFUL,
            32 => (ulong)IPAddress.HostToNetworkOrder((int)value) & 0xFFFFFFFFUL,
            64 => (ulong)IPAddress.HostToNetworkOrder((long)value),
            _ => value
        };

        return new[]
        {
            MakeResult(FormatHex(swapped, bits), $"{cmd} uint{bits}: {FormatHex(value, bits)} -> {FormatHex(swapped, bits)}", FormatHex(swapped, bits)),
            MakeResult(swapped.ToString(CultureInfo.InvariantCulture), $"decimal uint{bits}", swapped.ToString(CultureInfo.InvariantCulture))
        };
    }

    private static IEnumerable<Result> BaseConvert(string arg, int fromBase, int toBase)
    {
        if (!TryParseBase(arg, fromBase, out var value))
            return ErrorResult("Invalid number", arg);

        var text = toBase switch
        {
            2 => "0b" + System.Convert.ToString((long)value, 2),
            10 => value.ToString(CultureInfo.InvariantCulture),
            16 => FormatHex(value, MinimalBits(value)),
            _ => value.ToString(CultureInfo.InvariantCulture)
        };

        return new[] { MakeResult(text, $"base {fromBase} -> base {toBase}", text) };
    }

    private static IEnumerable<Result> AutoConvert(string input)
    {
        var results = new List<Result>();

        if (TryParseIpv4(input, out var ip))
            results.AddRange(Ipv4Results(ip));

        if (TryParseUnsigned(input, out var n))
        {
            results.Add(MakeResult(FormatHex(n, MinimalBits(n)), $"dec: {n}", FormatHex(n, MinimalBits(n))));
            results.Add(MakeResult("0b" + System.Convert.ToString((long)n, 2), $"dec: {n}", "0b" + System.Convert.ToString((long)n, 2)));
            if (n <= uint.MaxValue)
                results.AddRange(NumberToIpv4Results(n.ToString(CultureInfo.InvariantCulture)));
        }

        if (long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ts) && Math.Abs(ts) is >= 1_000_000_000 and <= 9_999_999_999_999)
            results.AddRange(UnixToDate(input, null));

        return results;
    }

    private static (string Command, string Remainder) SplitFirstToken(string s)
    {
        var i = s.IndexOf(' ');
        return i < 0 ? (s, string.Empty) : (s[..i], s[(i + 1)..]);
    }

    private static IPAddress ParseIpv4(string s)
    {
        if (IPAddress.TryParse(s.Trim(), out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            return ip;
        throw new FormatException("Invalid IPv4 address");
    }

    private static bool TryParseIpv4(string s, out IPAddress ip)
    {
        return IPAddress.TryParse(s.Trim(), out ip!) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
    }

    private static IEnumerable<Result> Ipv4Auto(string arg)
    {
        if (TryParseIpv4(arg, out var ip))
            return Ipv4Results(ip);
        if (TryParseUnsigned(arg, out var n))
            return NumberToIpv4Results(n.ToString(CultureInfo.InvariantCulture));
        return ErrorResult("Invalid IPv4/int/hex", arg);
    }

    private static IEnumerable<Result> Ipv4ToInt(string arg)
    {
        var ip = ParseIpv4(arg);
        var n = Ipv4ToUInt(ip);
        return new[] { MakeResult(n.ToString(CultureInfo.InvariantCulture), "IPv4 decimal-dot -> uint32", n.ToString(CultureInfo.InvariantCulture)) };
    }

    private static IEnumerable<Result> Ipv4ToHex(string arg)
    {
        var ip = ParseIpv4(arg);
        var n = Ipv4ToUInt(ip);
        return new[] { MakeResult(FormatHex(n, 32), "IPv4 decimal-dot -> hex uint32", FormatHex(n, 32)) };
    }

    private static IEnumerable<Result> IntToIpv4(string arg) => NumberToIpv4Results(arg);

    private static IEnumerable<Result> HexToIpv4(string arg) => NumberToIpv4Results(arg);

    private static IEnumerable<Result> NumberToIpv4Results(string arg)
    {
        if (!TryParseUnsigned(arg, out var n) || n > uint.MaxValue)
            return ErrorResult("Invalid uint32", arg);
        var ip = UIntToIpv4((uint)n);
        return new[] { MakeResult(ip.ToString(), "uint32/hex -> IPv4 decimal-dot", ip.ToString()) };
    }

    private static IEnumerable<Result> Ipv4Results(IPAddress ip)
    {
        var n = Ipv4ToUInt(ip);
        return new[]
        {
            MakeResult(n.ToString(CultureInfo.InvariantCulture), $"{ip} as uint32", n.ToString(CultureInfo.InvariantCulture)),
            MakeResult(FormatHex(n, 32), $"{ip} as hex uint32", FormatHex(n, 32))
        };
    }

    private static uint Ipv4ToUInt(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static IPAddress UIntToIpv4(uint value)
    {
        var bytes = new[]
        {
            (byte)((value >> 24) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF)
        };
        return new IPAddress(bytes);
    }

    private static int MinimalBits(ulong value)
    {
        if (value <= ushort.MaxValue) return 16;
        if (value <= uint.MaxValue) return 32;
        return 64;
    }

    private static string FormatHex(ulong value, int bits)
    {
        var width = bits / 4;
        return "0x" + value.ToString("X" + width, CultureInfo.InvariantCulture);
    }

    private static bool TryParseUnsigned(string s, out ulong value)
    {
        s = s.Trim().Replace("_", string.Empty, StringComparison.Ordinal);
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            return TryParseBinary(s[2..], out value);
        if (Regex.IsMatch(s, "^[01]+$") && s.Length > 1)
            return TryParseBinary(s, out value);
        return ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseBase(string s, int fromBase, out ulong value)
    {
        s = s.Trim().Replace("_", string.Empty, StringComparison.Ordinal);
        if (fromBase == 2)
        {
            if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
                s = s[2..];
            return TryParseBinary(s, out value);
        }
        if (fromBase == 16)
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s[2..];
            return ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }
        return ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseBinary(string s, out ulong value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s) || s.Any(c => c != '0' && c != '1') || s.Length > 64)
            return false;
        foreach (var c in s)
            value = (value << 1) | (ulong)(c - '0');
        return true;
    }

    private static Result MakeResult(string title, string subtitle, string copyText)
    {
        return new Result
        {
            Title = title,
            SubTitle = string.IsNullOrWhiteSpace(copyText) ? subtitle : subtitle + " | Enter: copy",
            IcoPath = IconPath,
            Action = _ =>
            {
                if (!string.IsNullOrWhiteSpace(copyText))
                    Clipboard.SetText(copyText);
                return true;
            }
        };
    }

    private static List<Result> ErrorResult(string title, string subtitle)
    {
        return new List<Result> { MakeResult(title, subtitle, string.Empty) };
    }
}
