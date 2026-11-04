using System.IO;
using System.Runtime.InteropServices;

namespace PennCraft.Desktop.Windows;

/// <summary>Runs the Windows file-operation UI on a dedicated STA, without suppressing prompts.</summary>
internal static class ShellFileMove
{
    internal static Task<T> OnSta<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(action()); }
            catch (Exception exception) { completion.SetException(exception); }
        }) { IsBackground = true, Name = "PennCraft file operation" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    internal static void Move(string source, string destination, nint owner)
    {
        object? operationObject = null;
        IShellItem? sourceItem = null;
        IShellItem? targetFolder = null;
        Marshal.ThrowExceptionForHR(OleInitialize(0));
        try
        {
            operationObject = Activator.CreateInstance(Type.GetTypeFromCLSID(new("3AD05575-8857-4850-9277-11B85BDB8E09"))!)!;
            var operation = (IFileOperation)operationObject;
            var itemId = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(source, 0, ref itemId, out sourceItem));
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(Path.GetDirectoryName(destination)!, 0, ref itemId, out targetFolder));
            operation.SetOwnerWindow(owner);
            operation.SetOperationFlags(0); // Windows owns progress, cancellation, conflicts and permission UI.
            operation.MoveItem(sourceItem, targetFolder, Path.GetFileName(destination), 0);
            operation.PerformOperations();
            operation.GetAnyOperationsAborted(out var aborted);
            if (aborted) throw new OperationCanceledException("Windows cancelled the move.");
        }
        finally
        {
            if (targetFolder is not null) Marshal.FinalReleaseComObject(targetFolder);
            if (sourceItem is not null) Marshal.FinalReleaseComObject(sourceItem);
            if (operationObject is not null) Marshal.FinalReleaseComObject(operationObject);
            OleUninitialize();
        }
    }

    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        void Advise(nint sink, out uint cookie);
        void Unadvise(uint cookie);
        void SetOperationFlags(uint flags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        void SetProgressDialog(nint dialog);
        void SetProperties(nint properties);
        void SetOwnerWindow(nint owner);
        void ApplyPropertiesToItem(nint item);
        void ApplyPropertiesToItems(nint items);
        void RenameItem(nint item, [MarshalAs(UnmanagedType.LPWStr)] string name, nint sink);
        void RenameItems(nint items, [MarshalAs(UnmanagedType.LPWStr)] string name);
        void MoveItem(IShellItem item, IShellItem folder,
            [MarshalAs(UnmanagedType.LPWStr)] string name, nint sink);
        void MoveItems(nint items, nint folder);
        void CopyItem(nint item, nint folder, nint name, nint sink);
        void CopyItems(nint items, nint folder);
        void DeleteItem(nint item, nint sink);
        void DeleteItems(nint items);
        void NewItem(nint folder, uint attributes, nint name, nint template, nint sink);
        void PerformOperations();
        void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint bind, ref Guid handler, ref Guid id, out nint result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint kind, out nint name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, nint bind, ref Guid id,
        out IShellItem item);
}
