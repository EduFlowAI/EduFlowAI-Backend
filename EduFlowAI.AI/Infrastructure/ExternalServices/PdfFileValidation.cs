using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.AI.Infrastructure.ExternalServices;

/// <summary>
/// Content-based PDF validation. A real PDF starts with the bytes "%PDF"; checking the magic
/// header is authoritative and cannot be spoofed by renaming a non-PDF to ".pdf".
/// </summary>
public static class PdfFileValidation
{
    // "%PDF"
    private static readonly byte[] PdfMagic = { 0x25, 0x50, 0x44, 0x46 };

    public static bool HasPdfHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length < PdfMagic.Length)
            return false;

        for (var i = 0; i < PdfMagic.Length; i++)
        {
            if (header[i] != PdfMagic[i])
                return false;
        }

        return true;
    }

    public static async Task<bool> HasPdfHeaderAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[PdfMagic.Length];

        if (stream.CanSeek)
            stream.Position = 0;

        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);

        if (stream.CanSeek)
            stream.Position = 0;

        return read >= PdfMagic.Length && HasPdfHeader(buffer);
    }
}
