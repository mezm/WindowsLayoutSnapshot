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
    }
}