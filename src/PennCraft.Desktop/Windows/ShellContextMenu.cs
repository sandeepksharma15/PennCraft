using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PennCraft.Desktop.Windows;

internal enum ItemMenuAction { Cancel, ShellCommand, Rename, MoveToDesktop, RemoveReference, ShowInExplorer }
internal sealed record ItemMenuChoice(ItemMenuAction Action, uint CommandId = 0);

/// <summary>STA-owned classic Windows shell menu for the item itself, including shortcut files.</summary>
internal sealed class ShellContextMenu : IDisposable
{
    private const uint FirstShellId = 1;
    private const uint LastShellId = 0x6FFF;
    private const uint MembershipId = 0x7000;
    private const uint ExplorerId = 0x7001;
    private readonly nint owner;
    private readonly bool managed;
    private readonly bool oleInitialized;
    private nint pidl;
    private nint menu;
    private IShellFolder? parent;
    private object? contextObject;
    private IContextMenu? context;
    private IContextMenu2? context2;
    private IContextMenu3? context3;
    private bool disposed;

    internal int ShellCommandCount { get; private set; }

    public ShellContextMenu(string path, nint owner, bool managed, bool includeMembership = true)
    {
        this.owner = owner;
        this.managed = managed;
        var hr = OleInitialize(0);
        Marshal.ThrowExceptionForHR(hr);
        oleInitialized = true;
        try
        {
            Marshal.ThrowExceptionForHR(SHParseDisplayName(path, 0, out pidl, 0, out _));
            var folderId = typeof(IShellFolder).GUID;
            Marshal.ThrowExceptionForHR(SHBindToParent(pidl, ref folderId, out parent, out var child));
            var menuId = typeof(IContextMenu).GUID;
            Marshal.ThrowExceptionForHR(parent.GetUIObjectOf(owner, 1, [child], ref menuId, 0, out var pointer));
            try { contextObject = Marshal.GetObjectForIUnknown(pointer); }
            finally { Marshal.Release(pointer); }
            context = (IContextMenu)contextObject;
            context2 = contextObject as IContextMenu2;
            context3 = contextObject as IContextMenu3;
            menu = CreatePopupMenu();
            if (menu == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            var result = context.QueryContextMenu(menu, 0, FirstShellId, LastShellId, 0x10); // CMF_CANRENAME
            Marshal.ThrowExceptionForHR(result);
            ShellCommandCount = result & 0xFFFF;
            Append(0x800, 0, null);
            if (includeMembership) Append(0, MembershipId, managed ? "Move to Desktop" : "Remove from Pen");
            Append(0, ExplorerId, "Show in File Explorer");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void Append(uint flags, uint id, string? label)
    {
        if (!AppendMenu(menu, flags, id, label)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public ItemMenuChoice Show(Point pixels)
    {
        var source = HwndSource.FromHwnd(owner) ?? throw new InvalidOperationException("Missing owner window.");
        source.AddHook(MenuMessage);
        try
        {
            SetForegroundWindow(owner);
            var command = TrackPopupMenuEx(menu, 0x0100 | 0x0002, (int)pixels.X, (int)pixels.Y, owner, 0);
            if (command == 0) return new(ItemMenuAction.Cancel);
            if (command == MembershipId) return new(managed ? ItemMenuAction.MoveToDesktop : ItemMenuAction.RemoveReference);
            if (command == ExplorerId) return new(ItemMenuAction.ShowInExplorer);
            return new(string.Equals(CanonicalVerb(command), "rename", StringComparison.OrdinalIgnoreCase)
                ? ItemMenuAction.Rename : ItemMenuAction.ShellCommand, command);
        }
        finally
        {
            source.RemoveHook(MenuMessage);
            PostMessage(owner, 0, 0, 0); // Required for reliable popup dismissal on subsequent invocations.
        }
    }

    internal string? CanonicalVerb(uint command)
    {
        var buffer = Marshal.AllocCoTaskMem(1024);
        try
        {
            Marshal.Copy(new byte[1024], 0, buffer, 1024);
            if (context!.GetCommandString(command - FirstShellId, 4, 0, buffer, 512) >= 0)
                return Marshal.PtrToStringUni(buffer);
            if (context.GetCommandString(command - FirstShellId, 0, 0, buffer, 512) >= 0)
                return Marshal.PtrToStringAnsi(buffer);
            return null;
        }
        finally { Marshal.FreeCoTaskMem(buffer); }
    }

    public void Invoke(uint command)
    {
        if (command < FirstShellId || command > LastShellId) throw new ArgumentOutOfRangeException(nameof(command));
        var info = new InvokeInfo
        {
            Size = Marshal.SizeOf<InvokeInfo>(), Owner = owner, Verb = (nint)(command - FirstShellId), Show = 1
        };
        // IDs require no ANSI path/parameter strings. Windows owns confirmation, recycle and elevation UI.
        Marshal.ThrowExceptionForHR(context!.InvokeCommand(ref info));
    }

    private nint MenuMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message is not (0x0117 or 0x002B or 0x002C or 0x0120)) return 0;
        if (context3 is not null && context3.HandleMenuMsg2((uint)message, wParam, lParam, out var result) == 0)
        {
            handled = true;
            return result;
        }
        if (context2 is not null && context2.HandleMenuMsg((uint)message, wParam, lParam) == 0) handled = true;
        return 0;
    }

    public static void ShowInExplorer(string path)
    {
        var initialized = OleInitialize(0);
        Marshal.ThrowExceptionForHR(initialized);
        nint item = 0;
        try
        {
            Marshal.ThrowExceptionForHR(SHParseDisplayName(path, 0, out item, 0, out _));
            Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(item, 0, 0, 0));
        }
        finally
        {
            if (item != 0) Marshal.FreeCoTaskMem(item);
            OleUninitialize();
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (menu != 0) DestroyMenu(menu);
        if (contextObject is not null) Marshal.FinalReleaseComObject(contextObject);
        if (parent is not null && !ReferenceEquals(parent, contextObject)) Marshal.FinalReleaseComObject(parent);
        if (pidl != 0) Marshal.FreeCoTaskMem(pidl);
        if (oleInitialized) OleUninitialize();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InvokeInfo
    {
        public int Size;
        public uint Mask;
        public nint Owner, Verb, Parameters, Directory;
        public int Show;
        public uint HotKey;
        public nint Icon;
    }

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        [PreserveSig] int ParseDisplayName(nint hwnd, nint bind, nint name, nint eaten, nint item, nint attributes);
        [PreserveSig] int EnumObjects(nint hwnd, uint flags, out nint result);
        [PreserveSig] int BindToObject(nint item, nint bind, ref Guid id, out nint result);
        [PreserveSig] int BindToStorage(nint item, nint bind, ref Guid id, out nint result);
        [PreserveSig] int CompareIDs(nint flags, nint first, nint second);
        [PreserveSig] int CreateViewObject(nint hwnd, ref Guid id, out nint result);
        [PreserveSig] int GetAttributesOf(uint count, nint items, ref uint attributes);
        [PreserveSig] int GetUIObjectOf(nint hwnd, uint count,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] nint[] items, ref Guid id, nint reserved, out nint result);
        [PreserveSig] int GetDisplayNameOf(nint item, uint flags, nint name);
        [PreserveSig] int SetNameOf(nint hwnd, nint item, nint name, uint flags, out nint result);
    }

    [ComImport, Guid("000214E4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref InvokeInfo info);
        [PreserveSig] int GetCommandString(nuint id, uint flags, nint reserved, nint name, uint maximum);
    }

    [ComImport, Guid("000214F4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu2
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref InvokeInfo info);
        [PreserveSig] int GetCommandString(nuint id, uint flags, nint reserved, nint name, uint maximum);
        [PreserveSig] int HandleMenuMsg(uint message, nint wParam, nint lParam);
    }

    [ComImport, Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu3
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref InvokeInfo info);
        [PreserveSig] int GetCommandString(nuint id, uint flags, nint reserved, nint name, uint maximum);
        [PreserveSig] int HandleMenuMsg(uint message, nint wParam, nint lParam);
        [PreserveSig] int HandleMenuMsg2(uint message, nint wParam, nint lParam, out nint result);
    }

    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string path, nint bind, out nint item, uint mask, out uint attributes);
    [DllImport("shell32.dll")] private static extern int SHBindToParent(nint item, ref Guid id, out IShellFolder parent, out nint child);
    [DllImport("shell32.dll")] private static extern int SHOpenFolderAndSelectItems(nint item, uint count, nint children, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenu(nint menu, uint flags, nuint id, string? label);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint owner, nint parameters);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);
}
