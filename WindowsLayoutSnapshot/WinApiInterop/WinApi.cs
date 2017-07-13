using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsLayoutSnapshot.WinApiInterop
{
    public static class WinApi
    {
        private const string USER_32_DLL_NAME = "user32.dll";

        public delegate bool EnumWindowsProc(int hWnd, int lParam);

        [DllImport(USER_32_DLL_NAME)]
        public static extern IntPtr BeginDeferWindowPos(int nNumWindows);

        [DllImport(USER_32_DLL_NAME)]
        public static extern IntPtr DeferWindowPos(
            IntPtr hWinPosInfo,
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            [MarshalAs(UnmanagedType.U4)] DeferWindowPosCommands uFlags);

        [DllImport(USER_32_DLL_NAME)]
        public static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);

        [DllImport(USER_32_DLL_NAME, EntryPoint = "GetWindowLong")]
        public static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport(USER_32_DLL_NAME, EntryPoint = "GetWindowLongPtr")]
        public static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport(USER_32_DLL_NAME)]
        public static extern IntPtr GetLastActivePopup(IntPtr hWnd);

        [DllImport(USER_32_DLL_NAME, ExactSpelling = true)]
        public static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestorFlags gaFlags);

        [DllImport(USER_32_DLL_NAME)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport(USER_32_DLL_NAME, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WindowPlacement lpwndpl);

        [DllImport(USER_32_DLL_NAME, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

        [DllImport(USER_32_DLL_NAME)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport(USER_32_DLL_NAME)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport(USER_32_DLL_NAME)]
        public static extern int EnumWindows(EnumWindowsProc ewp, int lParam);

        [DllImport(USER_32_DLL_NAME, CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowThreadProcessId(IntPtr handle, out uint processId);

        [DllImport(USER_32_DLL_NAME)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport(USER_32_DLL_NAME)]
        public static extern int GetWindowTextLength(IntPtr hWnd);
    }
}