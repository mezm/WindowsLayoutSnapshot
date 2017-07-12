using System.Drawing;
using System.Runtime.InteropServices;

namespace WindowsLayoutSnapshot.WinApiInterop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct WindowPlacement
    {
        public int length;
        public int flags;
        public int showCmd;
        public Point ptMinPosition;
        public Point ptMaxPosition;
        public Rect rcNormalPosition;
    }
}