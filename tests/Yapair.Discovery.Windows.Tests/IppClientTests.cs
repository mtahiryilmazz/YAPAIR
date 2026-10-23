using System.Buffers.Binary;
using System.Text;
using Yapair.Discovery.Windows.Ipp;

namespace Yapair.Discovery.Windows.Tests;

public sealed class IppClientTests
{
    [Fact]
    public void ParserReadsPrinterAttributes()
    {
        var response = BuildResponse(
            (0x42, "printer-name", Encoding.UTF8.GetBytes("Test Printer")),
            (0x23, "printer-state", UInt32(3)),
            (0x44, "printer-state-reasons", Encoding.UTF8.GetBytes("none")),
            (0x22, "printer-is-accepting-jobs", [1]),
            (0x21, "queued-job-count", UInt32(2)),
            (0x41, "printer-make-and-model", Encoding.UTF8.GetBytes("Test Model")));

        var message = IppClient.Parse(response);

        Assert.Equal(0x0000, message.StatusCode);
        Assert.Equal("successful-ok", message.StatusName);
        Assert.Equal("Test Printer", message.GetString("printer-name"));
        Assert.Equal(3, message.GetInt("printer-state"));
        Assert.Equal("idle", IppClient.PrinterStateName(3));
        Assert.True(message.GetBool("printer-is-accepting-jobs"));
        Assert.Equal(2, message.GetInt("queued-job-count"));
        Assert.Equal(["none"], message.GetStrings("printer-state-reasons"));
    }

    [Fact]
    public void RequestContainsIppVersionOperationAndPrinterUri()
    {
        var bytes = IppClient.BuildGetPrinterAttributes("ipp://192.168.1.10:631/ipp/print");

        Assert.True(bytes.Length > 8);
        Assert.Equal(0x01, bytes[0]);
        Assert.Equal(0x01, bytes[1]);
        Assert.Equal(0x000B, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(2, 2)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4, 4)));
        Assert.Contains("ipp://192.168.1.10:631/ipp/print", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.Equal(0x03, bytes[^1]);
    }

    [Fact]
    public void ParserRejectsTruncatedResponse()
    {
        Assert.Throws<FormatException>(() => IppClient.Parse(new byte[] { 0x01, 0x01, 0x00 }));
    }

    [Theory]
    [InlineData(0x0000, "successful-ok")]
    [InlineData(0x0401, "client-error-forbidden")]
    [InlineData(0x0506, "server-error-not-accepting-jobs")]
    public void StatusNamesAreStable(int code, string expected)
    {
        Assert.Equal(expected, IppClient.IppStatusName(code));
    }

    private static byte[] BuildResponse(params (byte Tag, string Name, byte[] Value)[] attributes)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(0x01);
        stream.WriteByte(0x01);
        WriteUInt16(stream, 0x0000);
        WriteUInt32(stream, 1);
        stream.WriteByte(0x02); // printer attributes group

        foreach (var (tag, name, value) in attributes)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name);
            stream.WriteByte(tag);
            WriteUInt16(stream, (ushort)nameBytes.Length);
            stream.Write(nameBytes);
            WriteUInt16(stream, (ushort)value.Length);
            stream.Write(value);
        }

        stream.WriteByte(0x03);
        return stream.ToArray();
    }

    private static byte[] UInt32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }
}
