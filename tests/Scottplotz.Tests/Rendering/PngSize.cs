using System.Buffers.Binary;

namespace Scottplotz.Tests.Rendering;

internal static class PngSize
{
    public static (int Width, int Height) Read(byte[] png) =>
        png.Length >= 24 && png[12] == (byte)'I' && png[13] == (byte)'H' && png[14] == (byte)'D' && png[15] == (byte)'R'
            ? (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)))
            : throw new InvalidOperationException("The bytes are not a PNG with an IHDR chunk.");
}
