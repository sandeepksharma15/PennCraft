using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using PennCraft.Desktop.Items;

namespace PennCraft.Desktop.Windows;

/// <summary>Bounded single-item CIDA parsing. Only the supported Recycle Bin name can escape this boundary.</summary>
internal static class VirtualItemDrop
{
    internal const string Format = "Shell IDList Array";

    internal static bool IsRecycleBin(IDataObject data)
    {
        if (!data.GetDataPresent(Format, autoConvert: false)
            || data.GetData(Format, autoConvert: false) is not MemoryStream stream || stream.Length > 65536) return false;
        return ReadCida(stream.ToArray());
    }

    internal static bool ReadCida(byte[] data)
    {
        if (data.Length < 16 || BinaryPrimitives.ReadUInt32LittleEndian(data) != 1) return false;
        var parent = ReadPidl(data, BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4)));
        var child = ReadPidl(data, BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8)));
        if (parent is null || child is null) return false;
        // Compare to a Windows-created PIDL for our fixed allowlisted object. Never pass untrusted PIDLs to native parsers.
        nint expected = 0;
        try
        {
            if (SHParseDisplayName(VirtualDesktopItem.RecycleBin, 0, out expected, 0, out _) < 0 || expected == 0) return false;
            var size = ILGetSize(expected);
            if (size > 65536 || size < 2) return false;
            var bytes = new byte[(int)size];
            Marshal.Copy(expected, bytes, 0, bytes.Length);
            var combined = parent[..^2].Concat(child).ToArray();
            return combined.AsSpan().SequenceEqual(bytes);
        }
        finally { if (expected != 0) Marshal.FreeCoTaskMem(expected); }
    }

    private static byte[]? ReadPidl(byte[] data, uint offset)
    {
        if (offset < 12 || offset > data.Length - 2) return null;
        var start = (int)offset;
        var cursor = start;
        while (cursor <= data.Length - 2)
        {
            var size = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(cursor));
            if (size == 0) return data[start..(cursor + 2)];
            if (size < 2 || size > data.Length - cursor) return null;
            cursor += size;
        }
        return null;
    }

    [DllImport("shell32.dll")] private static extern uint ILGetSize(nint pidl);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string path, nint bind, out nint pidl, uint mask, out uint attributes);
}
