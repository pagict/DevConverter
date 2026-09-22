using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace DevConverter.Core;

public sealed class ConversionEngine
{
    public IReadOnlyList<ConversionResult> Convert(string? query)
    {
        var input = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input))
            return Help();

        try
        {
            var (command, argument) = SplitFirstToken(input);
            var cmd = command.ToLowerInvariant();
            var arg = argument.Trim();

            return cmd switch
            {
                "help" or "?" => Help(),
                "ts" or "unix" or "unix_ts_2_date_str" => UnixToDate(arg, null),
                "tsms" => UnixToDate(arg, true),
                "date" or "date_str_2_unix_ts" => DateToUnix(arg, false),
                "dateutc" => DateToUnix(arg, true),
                "b64enc" => Base64Encode(arg),
                "b64dec" => Base64Decode(arg),
                "urlenc" or "urlencode" => UrlEncode(arg),
                "urldec" or "urldecode" => UrlDecode(arg),
                "hash" => HashText(arg),
                "uuid" or "guid" => GenerateUuid(arg),
                "h2n" or "host2net" or "n2h" or "net2host" => SwapEndianAuto(arg, cmd),
                "swap16" => SwapEndianFixed(arg, 16),
                "swap32" => SwapEndianFixed(arg, 32),
                "swap64" => SwapEndianFixed(arg, 64),
                "d2b" or "dec2bin" => BaseConvert(arg, 10, 2),
                "b2d" or "bin2dec" => BaseConvert(arg, 2, 10),
                "d2h" or "dec2hex" => BaseConvert(arg, 10, 16),
                "h2d" or "hex2dec" => BaseConvert(arg, 16, 10),
                "h2b" or "hex2bin" => BaseConvert(arg, 16, 2),
                "b2h" or "bin2hex" => BaseConvert(arg, 2, 16),
                "ip" or "ipv4" => Ipv4Auto(arg),
                "ip2int" or "ipv4_2_int" => Ipv4ToInt(arg),
                "ip2hex" or "ipv4_2_hex" => Ipv4ToHex(arg),
                "int2ip" or "int_2_ipv4" or "hex2ip" or "hex_2_ipv4" => NumberToIpv4(arg),
                _ => AutoConvert(input)
            };
        }
        catch (Exception ex)
        {
            return Error("Convert failed", ex.Message);
        }
    }

    private static IReadOnlyList<ConversionResult> Help() =>
    [
        Result("DevConverter commands", "ts/date, h2n/n2h/swap16/32/64, d2b/b2d/d2h/h2d, ip/ip2int/ip2hex/int2ip/hex2ip"),
        Result("Text commands", "b64enc/b64dec, urlenc/urlencode, urldec/urldecode, hash [md5|sha1|sha256|sha384|sha512], uuid/guid"),
        Result("Examples", "dev date now | dev urlenc hello world | dev hash sha256 hello | dev uuid")
    ];

    private static IReadOnlyList<ConversionResult> UnixToDate(string arg, bool? milliseconds)
    {
        if (!long.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return Error("Invalid timestamp", arg);

        var isMs = milliseconds ?? Math.Abs(value) > 99_999_999_999;
        var dto = isMs ? DateTimeOffset.FromUnixTimeMilliseconds(value) : DateTimeOffset.FromUnixTimeSeconds(value);
        var local = dto.ToLocalTime();
        var localText = local.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
        var utcText = dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return
        [
            Result(localText, "Local time", localText),
            Result(utcText, "UTC", utcText),
            Result(dto.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), "Unix seconds"),
            Result(dto.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), "Unix milliseconds")
        ];
    }

    private static IReadOnlyList<ConversionResult> DateToUnix(string arg, bool assumeUtc)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return Error("Invalid date", "Try: dev date 2026-06-25 17:30:00");

        DateTimeOffset dto;
        if (arg.Equals("now", StringComparison.OrdinalIgnoreCase))
            dto = DateTimeOffset.Now;
        else if (assumeUtc && DateTime.TryParse(arg, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var utc))
            dto = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        else if (DateTimeOffset.TryParse(arg, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            dto = parsed;
        else
            return Error("Invalid date", arg);

        var seconds = dto.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var milliseconds = dto.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var local = dto.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
        var utcText = dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return [Result(seconds, "Unix seconds"), Result(milliseconds, "Unix milliseconds"), Result(local, "Local time"), Result(utcText, "UTC")];
    }

    private static IReadOnlyList<ConversionResult> Base64Encode(string arg)
    {
        var encoded = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(arg));
        return [Result(encoded, "UTF-8 text -> Base64")];
    }

    private static IReadOnlyList<ConversionResult> Base64Decode(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return Error("Invalid Base64", "Try: dev b64dec aGVsbG8=");
        var bytes = System.Convert.FromBase64String(arg.Trim());
        var decoded = new UTF8Encoding(false, true).GetString(bytes);
        return [Result(decoded, "Base64 -> UTF-8 text")];
    }

    private static IReadOnlyList<ConversionResult> UrlEncode(string arg)
    {
        var encoded = Uri.EscapeDataString(arg);
        return [Result(encoded, "UTF-8 text -> URL percent-encoding")];
    }

    private static IReadOnlyList<ConversionResult> UrlDecode(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return Error("Invalid URL-encoded text", "Try: dev urldec hello%20world");
        var decoded = Uri.UnescapeDataString(arg);
        return [Result(decoded, "URL percent-encoding -> UTF-8 text")];
    }

    private static IReadOnlyList<ConversionResult> HashText(string arg)
    {
        var (first, remainder) = SplitFirstToken(arg);
        var requested = first.ToLowerInvariant();
        var supported = requested is "md5" or "sha1" or "sha256" or "sha384" or "sha512";
        var algorithm = supported ? requested : "sha256";
        var text = supported ? remainder : arg;
        if (string.IsNullOrEmpty(text))
            return Error("Missing text", "Try: dev hash sha256 hello");

        var bytes = Encoding.UTF8.GetBytes(text);
        var digest = algorithm switch
        {
            "md5" => MD5.HashData(bytes),
            "sha1" => SHA1.HashData(bytes),
            "sha256" => SHA256.HashData(bytes),
            "sha384" => SHA384.HashData(bytes),
            "sha512" => SHA512.HashData(bytes),
            _ => throw new InvalidOperationException("Unsupported hash algorithm")
        };
        var hex = System.Convert.ToHexString(digest).ToLowerInvariant();
        return [Result(hex, algorithm.ToUpperInvariant() + " (UTF-8)")];
    }

    private static IReadOnlyList<ConversionResult> GenerateUuid(string arg)
    {
        if (!string.IsNullOrWhiteSpace(arg))
            return Error("Unexpected argument", "Try: dev uuid");
        var uuid = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        return [Result(uuid, "UUID v4")];
    }

    private static IReadOnlyList<ConversionResult> SwapEndianAuto(string arg, string command)
    {
        if (!TryParseUnsigned(arg, out var value))
            return Error("Invalid integer", arg);
        return SwapEndianValue(value, MinimalBits(value), command);
    }

    private static IReadOnlyList<ConversionResult> SwapEndianFixed(string arg, int bits)
    {
        if (!TryParseUnsigned(arg, out var value))
            return Error("Invalid integer", arg);
        return SwapEndianValue(value, bits, "swap" + bits);
    }

    private static IReadOnlyList<ConversionResult> SwapEndianValue(ulong value, int bits, string command)
    {
        var swapped = bits switch
        {
            16 => (ulong)IPAddress.HostToNetworkOrder((short)value) & 0xFFFFUL,
            32 => (ulong)IPAddress.HostToNetworkOrder((int)value) & 0xFFFFFFFFUL,
            64 => (ulong)IPAddress.HostToNetworkOrder((long)value),
            _ => value
        };
        var hex = FormatHex(swapped, bits);
        return [Result(hex, $"{command} uint{bits}: {FormatHex(value, bits)} -> {hex}"), Result(swapped.ToString(CultureInfo.InvariantCulture), $"decimal uint{bits}")];
    }

    private static IReadOnlyList<ConversionResult> BaseConvert(string arg, int fromBase, int toBase)
    {
        if (!TryParseBase(arg, fromBase, out var value))
            return Error("Invalid number", arg);
        var text = toBase switch
        {
            2 => "0b" + System.Convert.ToString((long)value, 2),
            10 => value.ToString(CultureInfo.InvariantCulture),
            16 => FormatHex(value, MinimalBits(value)),
            _ => throw new ArgumentOutOfRangeException(nameof(toBase))
        };
        return [Result(text, $"base {fromBase} -> base {toBase}")];
    }

    private static IReadOnlyList<ConversionResult> Ipv4Auto(string arg)
    {
        if (TryParseIpv4(arg, out var ip)) return Ipv4Results(ip);
        if (TryParseUnsigned(arg, out _)) return NumberToIpv4(arg);
        return Error("Invalid IPv4/int/hex", arg);
    }

    private static IReadOnlyList<ConversionResult> Ipv4ToInt(string arg)
    {
        var ip = ParseIpv4(arg);
        var value = Ipv4ToUInt(ip).ToString(CultureInfo.InvariantCulture);
        return [Result(value, "IPv4 decimal-dot -> uint32")];
    }

    private static IReadOnlyList<ConversionResult> Ipv4ToHex(string arg)
    {
        var ip = ParseIpv4(arg);
        var value = FormatHex(Ipv4ToUInt(ip), 32);
        return [Result(value, "IPv4 decimal-dot -> hex uint32")];
    }

    private static IReadOnlyList<ConversionResult> NumberToIpv4(string arg)
    {
        if (!TryParseUnsigned(arg, out var number) || number > uint.MaxValue)
            return Error("Invalid uint32", arg);
        var ip = UIntToIpv4((uint)number).ToString();
        return [Result(ip, "uint32/hex -> IPv4 decimal-dot")];
    }

    private static IReadOnlyList<ConversionResult> Ipv4Results(IPAddress ip)
    {
        var value = Ipv4ToUInt(ip);
        return [Result(value.ToString(CultureInfo.InvariantCulture), $"{ip} as uint32"), Result(FormatHex(value, 32), $"{ip} as hex uint32")];
    }

    private static IReadOnlyList<ConversionResult> AutoConvert(string input)
    {
        var results = new List<ConversionResult>();
        if (TryParseIpv4(input, out var ip)) results.AddRange(Ipv4Results(ip));
        if (TryParseUnsigned(input, out var number))
        {
            results.Add(Result(FormatHex(number, MinimalBits(number)), $"dec: {number}"));
            results.Add(Result("0b" + System.Convert.ToString((long)number, 2), $"dec: {number}"));
            if (number <= uint.MaxValue) results.AddRange(NumberToIpv4(input));
        }
        if (long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp) && Math.Abs(timestamp) is >= 1_000_000_000 and <= 9_999_999_999_999)
            results.AddRange(UnixToDate(input, null));
        return results.Count > 0 ? results : Error("Unknown command", "Try: ts/date/h2n/d2b/ip/b64enc/b64dec/urlenc/urldec/hash/uuid");
    }

    private static (string Command, string Remainder) SplitFirstToken(string value)
    {
        value = value.TrimStart();
        var separator = 0;
        while (separator < value.Length && !char.IsWhiteSpace(value[separator])) separator++;
        if (separator == value.Length) return (value, string.Empty);
        var remainder = separator;
        while (remainder < value.Length && char.IsWhiteSpace(value[remainder])) remainder++;
        return (value[..separator], value[remainder..]);
    }

    private static IPAddress ParseIpv4(string value) =>
        TryParseIpv4(value, out var ip) ? ip : throw new FormatException("Invalid IPv4 address");

    private static bool TryParseIpv4(string value, out IPAddress ip) =>
        IPAddress.TryParse(value.Trim(), out ip!) && ip.AddressFamily == AddressFamily.InterNetwork;

    private static uint Ipv4ToUInt(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    }

    private static IPAddress UIntToIpv4(uint value) => new([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);

    private static int MinimalBits(ulong value) => value <= ushort.MaxValue ? 16 : value <= uint.MaxValue ? 32 : 64;

    private static string FormatHex(ulong value, int bits) => "0x" + value.ToString("X" + bits / 4, CultureInfo.InvariantCulture);

    private static bool TryParseUnsigned(string value, out ulong result)
    {
        var text = value.Trim().Replace("_", string.Empty, StringComparison.Ordinal);
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
        if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            return TryParseBinary(text[2..], out result);
        return ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseBase(string value, int fromBase, out ulong result)
    {
        var text = value.Trim().Replace("_", string.Empty, StringComparison.Ordinal);
        if (fromBase == 2)
        {
            if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) text = text[2..];
            return TryParseBinary(text, out result);
        }
        if (fromBase == 16)
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
            return ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
        }
        return ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseBinary(string value, out ulong result)
    {
        result = 0;
        if (string.IsNullOrEmpty(value) || value.Length > 64) return false;
        foreach (var c in value)
        {
            if (c is not ('0' or '1')) return false;
            result = (result << 1) | (ulong)(c - '0');
        }
        return true;
    }

    private static ConversionResult Result(string title, string subtitle, string? copyText = null) => new(title, subtitle, copyText ?? title);
    private static IReadOnlyList<ConversionResult> Error(string title, string subtitle) => [new(title, subtitle, string.Empty, true)];
}
