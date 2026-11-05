using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PennCraft.Desktop.Windows;

/// <summary>Same-volume, no-overwrite rename and persistent identity queries. No copy/delete fallback.</summary>
internal static partial class NativeFileMove
{
    public static string Identity(string path)
    {
        using var handle = CreateFile(LongPath(path), 0, 7, 0, 3, 0x02000000, 0);
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (info.IndexHigh == 0 && info.IndexLow == 0)
            throw new IOException("The filesystem does not provide a stable item identity.");
        return $"{info.Volume:X8}:{info.IndexHigh:X8}{info.IndexLow:X8}:{info.CreatedHigh:X8}{info.CreatedLow:X8}";
    }

    public static void Rename(string source, string destination)
    {
        // Deliberately omit REPLACE_EXISTING and COPY_ALLOWED.
        if (!MoveFileEx(LongPath(source), LongPath(destination), 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static string LongPath(string path) => path.StartsWith(@"\\?\", StringComparison.Ordinal)
        ? path : path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path[2..] : @"\\?\" + path;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public uint CreatedLow;
        public uint CreatedHigh;
        public uint AccessedLow;
        public uint AccessedHigh;
        public uint WrittenLow;
        public uint WrittenHigh;
        public uint Volume;
        public uint SizeHigh;
        public uint SizeLow;
        public uint Links;
        public uint IndexHigh;
        public uint IndexLow;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFile(string name, uint access, uint share,
        nint security, uint creation, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);

    [LibraryImport("kernel32.dll", EntryPoint = "MoveFileExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MoveFileEx(string source, string destination, uint flags);
}
