using System.Globalization;
using DevConverter.Core;
using Xunit;

namespace DevConverter.Core.Tests;

public sealed class ConversionEngineTests
{
    private readonly ConversionEngine _engine = new();

    [Theory]
    [InlineData("b64enc hello", "aGVsbG8=")]
    [InlineData("b64dec aGVsbG8=", "hello")]
    [InlineData("urlenc hello world", "hello%20world")]
    [InlineData("urlencode 你好", "%E4%BD%A0%E5%A5%BD")]
    [InlineData("urldec hello%20world", "hello world")]
    [InlineData("urldecode %E4%BD%A0%E5%A5%BD", "你好")]
    [InlineData("hash sha256 hello", "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824")]
    [InlineData("d2h 42", "0x002A")]
    [InlineData("b2d 101010", "42")]
    [InlineData("ip2int 192.168.1.1", "3232235777")]
    [InlineData("int2ip 3232235777", "192.168.1.1")]
    public void ConvertsKnownValues(string command, string expected) =>
        Assert.Equal(expected, _engine.Convert(command)[0].CopyText);

    [Fact]
    public void GeneratesUuidV4()
    {
        var uuid = Guid.Parse(_engine.Convert("uuid")[0].CopyText);
        Assert.Equal(4, (uuid.ToByteArray()[7] >> 4) & 0x0f);
    }

    [Fact]
    public void DateNowReturnsCurrentUnixSeconds()
    {
        var before = DateTimeOffset.Now.ToUnixTimeSeconds();
        var actual = long.Parse(_engine.Convert("date now")[0].CopyText, CultureInfo.InvariantCulture);
        var after = DateTimeOffset.Now.ToUnixTimeSeconds();
        Assert.InRange(actual, before, after);
    }

    [Fact]
    public void UnknownCommandReturnsError() => Assert.True(_engine.Convert("not-a-command")[0].IsError);
}
