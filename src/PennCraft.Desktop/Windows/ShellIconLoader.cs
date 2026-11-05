using System.Collections.Concurrent;
using PennCraft.Desktop.Items;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace PennCraft.Desktop.Windows;

/// <summary>Serial STA shell requests keep slow icon handlers off the WPF dispatcher.</summary>
internal sealed class ShellIconLoader : IDisposable
{
    private readonly BlockingCollection<Request> requests = new();
    private readonly Thread worker;
    private bool disposed;

    public ShellIconLoader()
    {
        worker = new Thread(Work) { IsBackground = true, Name = "PennCraft shell icons" };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

    public Task<BitmapSource?> LoadAsync(string path)
    {
        var completion = new TaskCompletionSource<BitmapSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!disposed) requests.Add(new Request(path, completion));
        else completion.SetResult(null);
        return completion.Task;
    }

    private void Work()
    {
        var initialized = CoInitializeEx(0, 2) >= 0; // COINIT_APARTMENTTHREADED
        try
        {
            foreach (var request in requests.GetConsumingEnumerable())
            {
                try { request.Completion.SetResult(initialized ? Load(request.Path) : null); }
                catch (Exception exception) when (exception is ArgumentException or ExternalException or InvalidOperationException)
                {
                    Diagnostics.Write($"icon load failed: {exception.GetType().Name}");
                    request.Completion.SetResult(null);
                }
            }
        }
        finally
        {
            if (initialized) CoUninitialize();
            requests.Dispose();
        }
    }

    internal static BitmapSource? Load(string path)
    {
        var info = new ShellFileInfo();
        nint pidl = 0;
        try
        {
            // Query the actual item so shortcuts retain their custom target icon.
            nint result;
            if (VirtualDesktopItem.IsRecycleBin(path))
            {
                Marshal.ThrowExceptionForHR(SHParseDisplayName(VirtualDesktopItem.RecycleBin, 0, out pidl, 0, out _));
                result = SHGetFileInfo(pidl, 0, ref info, (uint)Marshal.SizeOf<ShellFileInfo>(), 0x108);
            }
            else result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<ShellFileInfo>(), 0x100);
            if (result == 0 || info.Icon == 0) return null; // SHGFI_ICON | SHGFI_LARGEICON
            var bitmap = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(32, 32));
            bitmap.Freeze();
            return bitmap;
        }
        finally
        {
            if (info.Icon != 0) DestroyIcon(info.Icon);
            if (pidl != 0) Marshal.FreeCoTaskMem(pidl);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        requests.CompleteAdding();
    }

    private sealed record Request(string Path, TaskCompletionSource<BitmapSource?> Completion);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public nint Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(string path, uint attributes, ref ShellFileInfo info, uint size, uint flags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string path, nint bind, out nint pidl, uint mask, out uint attributes);
    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW")]
    private static extern nint SHGetFileInfo(nint pidl, uint attributes, ref ShellFileInfo info, uint size, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);
}
