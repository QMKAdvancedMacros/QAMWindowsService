namespace QMKMacroService;

using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;

internal struct WindowInfo
{
    public uint Ownerpid;
    public uint Childpid;
}

public abstract class WindowApi
{
    #region User32
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    // When you don't want the ProcessId, use this overload and pass IntPtr.Zero for the second parameter
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
    /// <summary>
    /// Delegate for the EnumChildWindows method
    /// </summary>
    /// <param name="hWnd">Window handle</param>
    /// <param name="parameter">Caller-defined variable; we use it for a pointer to our list</param>
    /// <returns>True to continue enumerating, false to bail.</returns>
    private delegate bool EnumWindowProc(IntPtr hWnd, IntPtr parameter);

    [DllImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowProc lpEnumFunc, IntPtr lParam);
    #endregion

    #region Kernel32

    private const UInt32 ProcessQueryInformation = 0x400;
    private const UInt32 ProcessVmRead = 0x010;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool QueryFullProcessImageName([In]IntPtr hProcess, [In]int dwFlags, [Out]StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        UInt32 dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)]
        Boolean bInheritHandle,
        Int32 dwProcessId
    );
    #endregion

    private static string? GetProcessName(IntPtr hWnd)
    {
        if (hWnd < 0)
        {
            return null;
        }

        hWnd = GetForegroundWindow();

        if (hWnd == IntPtr.Zero)
            return null;

        GetWindowThreadProcessId(hWnd, out var pId);

        IntPtr proc;
        if ((proc = OpenProcess(ProcessQueryInformation | ProcessVmRead, false, (int)pId)) == IntPtr.Zero)
            return null;

        int capacity = 2000;
        StringBuilder sb = new StringBuilder(capacity);
        QueryFullProcessImageName(proc, 0, sb, ref capacity);

        var processName = sb.ToString(0, capacity);

        // UWP apps are wrapped in another app called, if this has focus then try and find the child UWP process
        if (Path.GetFileName(processName).Equals("ApplicationFrameHost.exe"))
        {
            processName = UWP_AppName(hWnd, pId);
        }

        return processName;
    }

    #region Get UWP Application Name

    /// <summary>
    /// Find child process for uwp apps, edge, mail, etc.
    /// </summary>
    /// <param name="hWnd">hWnd</param>
    /// <param name="pId">pID</param>
    /// <returns>The application name of the UWP.</returns>
    private static string? UWP_AppName(IntPtr hWnd, uint pId)
    {
        var windowInfo = new WindowInfo
        {
            Ownerpid = pId,
            Childpid = pId
        };

        IntPtr pWindowInfo = Marshal.AllocHGlobal(Marshal.SizeOf(windowInfo));

        Marshal.StructureToPtr(windowInfo, pWindowInfo, false);

        EnumWindowProc lpEnumFunc = new EnumWindowProc(EnumChildWindowsCallback);
        EnumChildWindows(hWnd, lpEnumFunc, pWindowInfo);

        windowInfo = (WindowInfo)Marshal.PtrToStructure(pWindowInfo, typeof(WindowInfo))!;
        
        IntPtr proc;
        if ((proc = OpenProcess(ProcessQueryInformation | ProcessVmRead, false, (int)windowInfo.Childpid)) == IntPtr.Zero)
            return null;

        int capacity = 2000;
        StringBuilder sb = new StringBuilder(capacity);
        QueryFullProcessImageName(proc, 0, sb, ref capacity);

        Marshal.FreeHGlobal(pWindowInfo);

        return sb.ToString(0, capacity);
    }

    /// <summary>
    /// Callback for enumerating the child windows.
    /// </summary>
    /// <param name="hWnd">hWnd</param>
    /// <param name="lParam">lParam</param>
    /// <returns>always <c>true</c>.</returns>
    private static bool EnumChildWindowsCallback(IntPtr hWnd, IntPtr lParam)
    {
        WindowInfo info = (WindowInfo)Marshal.PtrToStructure(lParam, typeof(WindowInfo))!;

        GetWindowThreadProcessId(hWnd, out var pId);

        if (pId != info.Ownerpid)
            info.Childpid = pId;

        Marshal.StructureToPtr(info, lParam, true);

        return true;
    }
    #endregion
    
    public static string? GetCurrentWindowPath()
    {
        var hWnd = GetForegroundWindow();

        return GetProcessName(hWnd);
    }

    public static string? GetCurrentWindowName()
    {
        return Path.GetFileName(GetCurrentWindowPath());
    }
}
