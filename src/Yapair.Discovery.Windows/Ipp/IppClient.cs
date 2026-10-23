using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace Yapair.Discovery.Windows.Ipp;

internal sealed record IppAttributeValue(byte Tag, object Value);

internal sealed record IppMessage(
    int MajorVersion,
    int MinorVersion,
    int StatusCode,
    string StatusName,
    int RequestId,
    IReadOnlyDictionary<string, IReadOnlyList<IppAttributeValue>> Attributes)
{
    public string? GetString(string name)
    {
        if (!Attributes.TryGetValue(name, out var values))
        {
            return null;
        }

        return values
            .Where(static value => value.Tag is >= 0x41 and <= 0x4A && value.Value is string)
            .Select(static value => (string)value.Value)
            .FirstOrDefault();
    }

    public int? GetInt(string name)
    {
        if (!Attributes.TryGetValue(name, out var values))
        {
            return null;
        }

        return values
            .Where(static value => value.Tag is 0x21 or 0x23 && value.Value is int)
            .Select(static value => (int)value.Value)
            .FirstOrDefault();
    }

    public bool? GetBool(string name)
    {
        if (!Attributes.TryGetValue(name, out var values))
        {
            return null;
        }

        return values
            .Where(static value => value.Tag == 0x22 && value.Value is bool)
            .Select(static value => (bool)value.Value)
            .FirstOrDefault();
    }

    public IReadOnlyList<string> GetStrings(string name)
    {
        if (!Attributes.TryGetValue(name, out var values))
        {
            return [];
        }

        return values
            .Where(static value => value.Tag is >= 0x41 and <= 0x4A && value.Value is string)
            .Select(static value => (string)value.Value)
            .ToArray();
    }
}

internal sealed record IppCallResult(
    int? HttpStatus,
    IppMessage? Message,
    string? ErrorCode,
    string? ErrorMessage,
    string? EffectiveUri);

internal static class IppClient
{
    public static async Task<IppCallResult> GetPrinterAttributesAsync(
        Uri httpUri,
        TimeSpan timeout,
        bool allowInsecureTls,
        CancellationToken cancellationToken)
    {
        try
        {
            using var handler = new SocketsHttpHandler
            {
                ConnectTimeout = timeout < TimeSpan.FromSeconds(3) ? timeout : TimeSpan.FromSeconds(3),
                PooledConnectionLifetime = TimeSpan.FromMinutes(1),
                AllowAutoRedirect = false,
            };

            if (allowInsecureTls)
            {
                // Explicit opt-in only. IPP exposes no credentials here, but a
                // spoofed device could still report false evidence.
                handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
            }

            using var client = new HttpClient(handler, disposeHandler: false)
            {
                Timeout = timeout,
            };

            return await SendCoreAsync(client, httpUri, followedRedirect: false, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new IppCallResult(null, null, "ipp_timeout", "IPP endpoint did not respond before the timeout.", null);
        }
        catch (HttpRequestException ex) when (IsTlsFailure(ex))
        {
            return new IppCallResult(
                null,
                null,
                "ipp_tls_certificate",
                "IPP endpoint redirected to HTTPS but its certificate could not be validated. Use --ipp-insecure-tls only for a trusted local device.",
                null);
        }
        catch (HttpRequestException)
        {
            return new IppCallResult(null, null, "ipp_unreachable", "IPP endpoint could not be reached.", null);
        }
        catch (Exception ex) when (ex is IOException or SocketException or FormatException)
        {
            return new IppCallResult(null, null, "ipp_transport_error", "IPP request failed before a valid response was received.", null);
        }
    }

    private static async Task<IppCallResult> SendCoreAsync(
        HttpClient client,
        Uri uri,
        bool followedRedirect,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new ByteArrayContent(BuildGetPrinterAttributes(BuildPrinterUri(uri))),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/ipp");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/ipp"));

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken).ConfigureAwait(false);

        var httpStatus = (int)response.StatusCode;

        if (!followedRedirect && httpStatus is 301 or 302 or 307 or 308)
        {
            var location = response.Headers.Location;
            if (location is null)
            {
                return new IppCallResult(httpStatus, null, "ipp_invalid_redirect", "IPP endpoint redirected without a Location header.", uri.ToString());
            }

            var redirected = location.IsAbsoluteUri ? location : new Uri(uri, location);
            if (!string.Equals(redirected.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return new IppCallResult(
                    httpStatus,
                    null,
                    "ipp_unsupported_redirect",
                    "IPP endpoint redirected to an unsupported scheme.",
                    uri.ToString());
            }

            return await SendCoreAsync(client, redirected, followedRedirect: true, cancellationToken).ConfigureAwait(false);
        }

        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return new IppCallResult(
                httpStatus,
                null,
                "ipp_http_error",
                $"IPP endpoint returned HTTP {httpStatus}.",
                uri.ToString());
        }

        if (body.Length < 8)
        {
            return new IppCallResult(httpStatus, null, "ipp_invalid_response", "IPP response was shorter than the mandatory header.", uri.ToString());
        }

        var message = Parse(body);
        return new IppCallResult(httpStatus, message, null, null, uri.ToString());
    }

    private static string BuildPrinterUri(Uri uri)
    {
        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            var port = uri.Port is 443 ? string.Empty : $":{uri.Port}";
            return $"ipps://{uri.Host}{port}{uri.PathAndQuery}";
        }

        return $"ipp://{uri.Host}:{uri.Port}{uri.PathAndQuery}";
    }

    private static bool IsTlsFailure(Exception exception)
    {
        if (exception is AuthenticationException)
        {
            return true;
        }

        return exception.InnerException is not null && IsTlsFailure(exception.InnerException);
    }

    internal static byte[] BuildGetPrinterAttributes(string printerUri)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(0x01); // IPP/1.1
        stream.WriteByte(0x01);
        WriteUInt16(stream, 0x000B); // Get-Printer-Attributes
        WriteUInt32(stream, 1); // request-id

        stream.WriteByte(0x01); // operation attributes group
        WriteAttribute(stream, 0x47, "attributes-charset", "utf-8");
        WriteAttribute(stream, 0x48, "attributes-natural-language", "en");
        WriteAttribute(stream, 0x45, "printer-uri", printerUri);

        WriteAttribute(stream, 0x44, "requested-attributes", "printer-name");
        foreach (var attribute in new[]
                 {
                     "printer-state",
                     "printer-state-reasons",
                     "printer-is-accepting-jobs",
                     "queued-job-count",
                     "printer-make-and-model",
                     "printer-info",
                 })
        {
            WriteAdditionalValue(stream, 0x44, attribute);
        }

        stream.WriteByte(0x03); // end-of-attributes
        return stream.ToArray();
    }

    internal static IppMessage Parse(ReadOnlySpan<byte> body)
    {
        if (body.Length < 8)
        {
            throw new FormatException("IPP response is too short.");
        }

        var major = body[0];
        var minor = body[1];
        var statusCode = BinaryPrimitives.ReadUInt16BigEndian(body[2..4]);
        var requestId = (int)BinaryPrimitives.ReadUInt32BigEndian(body[4..8]);

        var attributes = new Dictionary<string, List<IppAttributeValue>>(StringComparer.OrdinalIgnoreCase);
        string? currentName = null;
        var offset = 8;

        while (offset < body.Length)
        {
            var tag = body[offset++];
            if (tag == 0x03)
            {
                break;
            }

            if (tag < 0x10)
            {
                currentName = null;
                continue;
            }

            if (offset + 2 > body.Length)
            {
                throw new FormatException("IPP attribute name length is truncated.");
            }

            var nameLength = BinaryPrimitives.ReadUInt16BigEndian(body[offset..(offset + 2)]);
            offset += 2;

            if (offset + nameLength > body.Length)
            {
                throw new FormatException("IPP attribute name is truncated.");
            }

            var name = nameLength == 0 ? null : Encoding.UTF8.GetString(body.Slice(offset, nameLength));
            offset += nameLength;

            if (offset + 2 > body.Length)
            {
                throw new FormatException("IPP attribute value length is truncated.");
            }

            var valueLength = BinaryPrimitives.ReadUInt16BigEndian(body[offset..(offset + 2)]);
            offset += 2;

            if (offset + valueLength > body.Length)
            {
                throw new FormatException("IPP attribute value is truncated.");
            }

            var value = ParseValue(tag, body.Slice(offset, valueLength));
            offset += valueLength;

            if (name is not null)
            {
                currentName = name;
            }

            if (currentName is null)
            {
                continue;
            }

            if (!attributes.TryGetValue(currentName, out var list))
            {
                list = [];
                attributes[currentName] = list;
            }

            list.Add(new IppAttributeValue(tag, value));
        }

        return new IppMessage(
            major,
            minor,
            statusCode,
            IppStatusName(statusCode),
            requestId,
            attributes.ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyList<IppAttributeValue>)pair.Value,
                StringComparer.OrdinalIgnoreCase));
    }

    internal static string IppStatusName(int statusCode) => statusCode switch
    {
        0x0000 => "successful-ok",
        0x0001 => "successful-ok-ignored-or-substituted-attributes",
        0x0002 => "successful-ok-conflicting-attributes",
        0x0400 => "client-error-bad-request",
        0x0401 => "client-error-forbidden",
        0x0402 => "client-error-not-authenticated",
        0x0403 => "client-error-not-authorized",
        0x0404 => "client-error-not-possible",
        0x0405 => "client-error-timeout",
        0x0406 => "client-error-not-found",
        0x0407 => "client-error-gone",
        0x0408 => "client-error-request-entity-too-large",
        0x0409 => "client-error-request-value-too-long",
        0x040A => "client-error-document-format-not-supported",
        0x040B => "client-error-attributes-or-values-not-supported",
        0x040C => "client-error-uri-scheme-not-supported",
        0x040D => "client-error-charset-not-supported",
        0x040E => "client-error-conflicting-attributes",
        0x040F => "client-error-compression-not-supported",
        0x0410 => "client-error-compression-error",
        0x0411 => "client-error-document-format-error",
        0x0412 => "client-error-document-access-error",
        0x0500 => "server-error-internal-error",
        0x0501 => "server-error-operation-not-supported",
        0x0502 => "server-error-service-unavailable",
        0x0503 => "server-error-version-not-supported",
        0x0504 => "server-error-device-error",
        0x0505 => "server-error-temporary-error",
        0x0506 => "server-error-not-accepting-jobs",
        0x0507 => "server-error-busy",
        0x0508 => "server-error-job-canceled",
        0x0509 => "server-error-multiple-document-jobs-not-supported",
        _ => $"ipp_status_0x{statusCode:X4}",
    };

    internal static string PrinterStateName(int state) => state switch
    {
        3 => "idle",
        4 => "processing",
        5 => "stopped",
        _ => "unknown",
    };

    private static object ParseValue(byte tag, ReadOnlySpan<byte> value)
    {
        switch (tag)
        {
            case 0x21:
            case 0x23:
                return value.Length >= 4 ? (int)BinaryPrimitives.ReadUInt32BigEndian(value[..4]) : 0;
            case 0x22:
                return value.Length >= 1 && value[0] != 0;
            case 0x41:
            case 0x42:
            case 0x43:
            case 0x44:
            case 0x45:
            case 0x46:
            case 0x47:
            case 0x48:
            case 0x49:
            case 0x4A:
                return Encoding.UTF8.GetString(value);
            default:
                return value.ToArray();
        }
    }

    private static void WriteAttribute(Stream stream, byte tag, string name, string value)
    {
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var valueBytes = Encoding.UTF8.GetBytes(value);

        stream.WriteByte(tag);
        WriteUInt16(stream, (ushort)nameBytes.Length);
        stream.Write(nameBytes);
        WriteUInt16(stream, (ushort)valueBytes.Length);
        stream.Write(valueBytes);
    }

    private static void WriteAdditionalValue(Stream stream, byte tag, string value)
    {
        var valueBytes = Encoding.UTF8.GetBytes(value);
        stream.WriteByte(tag);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, (ushort)valueBytes.Length);
        stream.Write(valueBytes);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }
}
