using System;

namespace WindowsLayoutSnapshot.WinApiInterop
{
    public static class WinApiExtensions
    {
        private const int GWL_EXSTYLE = -20;

        public static IntPtr GetWindowLongPointer(this IntPtr handler)
        {
            return IntPtr.Size == 8 ? WinApi.GetWindowLongPtr64(handler, GWL_EXSTYLE) : WinApi.GetWindowLongPtr32(handler, GWL_EXSTYLE);
        }

        public static bool IsTopLevelWindow(this IntPtr handler) => (handler.ToInt64() & ExtendedWindowStyles.WS_EX_APPWINDOW) > 0;
        public static bool IsToolbar(this IntPtr handler) => (handler.ToInt64() & ExtendedWindowStyles.WS_EX_TOOLWINDOW) > 0;

        private static class ExtendedWindowStyles
        {
            public const long WS_EX_APPWINDOW = 0x00040000L;
            public const long WS_EX_TOOLWINDOW = 0x00000080L;
        }
    }
}