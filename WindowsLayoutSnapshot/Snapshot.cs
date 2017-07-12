using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowsLayoutSnapshot
{
    internal class Snapshot
    {
        private readonly Dictionary<IntPtr, WindowPlacement> m_placements = new Dictionary<IntPtr, WindowPlacement>();
        private List<IntPtr> m_windowsBackToTop = new List<IntPtr>();

        private Snapshot(bool userInitiated)
        {
            EnumWindows(EvalWindow, 0);

            TimeTaken = DateTime.UtcNow;
            UserInitiated = userInitiated;

            var pixels = new List<long>();
            foreach (var screen in Screen.AllScreens)
            {
                pixels.Add(screen.Bounds.Width * screen.Bounds.Height);
            }
            MonitorPixelCounts = pixels.ToArray();
            NumMonitors = pixels.Count;
        }

        internal static Snapshot TakeSnapshot(bool userInitiated)
        {
            return new Snapshot(userInitiated);
        }

        private bool EvalWindow(int hwndInt, int lParam)
        {
            var hwnd = new IntPtr(hwndInt);

            if (!IsAltTabWindow(hwnd))
            {
                return true;
            }

            // EnumWindows returns windows in Z order from back to front
            m_windowsBackToTop.Add(hwnd);

            var placement = new WindowPlacement { length = Marshal.SizeOf(typeof(WindowPlacement)) };
            if (!GetWindowPlacement(hwnd, ref placement))
            {
                throw new Exception("Error getting window placement");
            }
            m_placements.Add(hwnd, placement);

            return true;
        }

        internal DateTime TimeTaken { get; }
        internal bool UserInitiated { get; }
        internal long[] MonitorPixelCounts { get; }
        internal int NumMonitors { get; }

        internal TimeSpan Age => DateTime.UtcNow.Subtract(TimeTaken);

        internal void RestoreAndPreserveMenu(object sender, EventArgs e)
        {
            // ignore extra params
            // We save and restore the current foreground window because it's our tray menu
            // I couldn't find a way to get this handle straight from the tray menu's properties;
            //   the ContextMenuStrip.Handle isn't the right one, so I'm using win32
            // More info RE the restore is below, where we do it
            var currentForegroundWindow = GetForegroundWindow();

            try
            {
                Restore(sender, e);
            }
            finally
            {
                // A combination of SetForegroundWindow + SetWindowPos (via set_Visible) seems to be needed
                // This was determined by trying a bunch of stuff
                // This prevents the tray menu from closing, and makes sure it's still on top
                SetForegroundWindow(currentForegroundWindow);
                TrayIconForm.me.Visible = true;
            }
        }

        internal void Restore(object sender, EventArgs e)
        {
            // ignore extra params
            // first, restore the window rectangles and normal/maximized/minimized states
            foreach (var placement in m_placements)
            {
                // this might error out if the window no longer exists
                var placementValue = placement.Value;

                // make sure points and rects will be inside monitor
                IntPtr extendedStyles = GetWindowLongPtr(placement.Key, (-20)); // GWL_EXSTYLE
                placementValue.ptMaxPosition = GetUpperLeftCornerOfNearestMonitor(extendedStyles, placementValue.ptMaxPosition);
                placementValue.ptMinPosition = GetUpperLeftCornerOfNearestMonitor(extendedStyles, placementValue.ptMinPosition);
                placementValue.rcNormalPosition = GetRectInsideNearestMonitor(extendedStyles, placementValue.rcNormalPosition);

                SetWindowPlacement(placement.Key, ref placementValue);
            }

            // now update the z-orders
            m_windowsBackToTop = m_windowsBackToTop.FindAll(IsWindowVisible);
            IntPtr positionStructure = BeginDeferWindowPos(m_windowsBackToTop.Count);
            for (int i = 0; i < m_windowsBackToTop.Count; i++)
            {
                positionStructure = DeferWindowPos(positionStructure,
                    m_windowsBackToTop[i],
                    i == 0 ? IntPtr.Zero : m_windowsBackToTop[i - 1],
                    0,
                    0,
                    0,
                    0,
                    DeferWindowPosCommands.SWP_NOMOVE | DeferWindowPosCommands.SWP_NOSIZE | DeferWindowPosCommands.SWP_NOACTIVATE);
            }
            EndDeferWindowPos(positionStructure);
        }

        private static Point GetUpperLeftCornerOfNearestMonitor(IntPtr windowExtendedStyles, Point point)
        {
            return (windowExtendedStyles.ToInt64() & 0x00000080) > 0 ? Screen.GetBounds(point).Location : Screen.GetWorkingArea(point).Location;
        }

        private static Rectangle GetRectInsideNearestMonitor(IntPtr windowExtendedStyles, Rectangle rect)
        {
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;

            var rectAsRectangle = new System.Drawing.Rectangle(rect.Left, rect.Top, width, height);
            var monitorRect = (windowExtendedStyles.ToInt64() & 0x00000080) > 0 ? Screen.GetBounds(rectAsRectangle) : Screen.GetWorkingArea(rectAsRectangle);

            var y = new Rectangle
            {
                Left = Math.Max(monitorRect.Left, Math.Min(monitorRect.Right - width, rect.Left)),
                Top = Math.Max(monitorRect.Top, Math.Min(monitorRect.Bottom - height, rect.Top))
            };
            y.Right = y.Left + Math.Min(monitorRect.Width, width);
            y.Bottom = y.Top + Math.Min(monitorRect.Height, height);
            return y;
        }

        private static bool IsAltTabWindow(IntPtr hwnd)
        {
            if (!IsWindowVisible(hwnd))
            {
                return false;
            }

            var extendedStyles = GetWindowLongPtr(hwnd, (-20)); // GWL_EXSTYLE
            if ((extendedStyles.ToInt64() & 0x00040000) > 0)
            {
                // WS_EX_APPWINDOW
                return true;
            }
            if ((extendedStyles.ToInt64() & 0x00000080) > 0)
            {
                // WS_EX_TOOLWINDOW
                return false;
            }

            IntPtr hwndTry = GetAncestor(hwnd, GetAncestorFlags.GetRootOwner);
            IntPtr hwndWalk = IntPtr.Zero;
            while (hwndTry != hwndWalk)
            {
                hwndWalk = hwndTry;
                hwndTry = GetLastActivePopup(hwndWalk);
                if (IsWindowVisible(hwndTry))
                {
                    break;
                }
            }
            if (hwndWalk != hwnd)
            {
                return false;
            }

            return true;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr BeginDeferWindowPos(int nNumWindows);

        [DllImport("user32.dll")]
        private static extern IntPtr DeferWindowPos(IntPtr hWinPosInfo, IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy,
            [MarshalAs(UnmanagedType.U4)] DeferWindowPosCommands uFlags);

        [Flags]
        private enum DeferWindowPosCommands : uint
        {
            // ReSharper disable UnusedMember.Local
            SWP_DRAWFRAME = 0x0020,
            SWP_FRAMECHANGED = 0x0020,
            SWP_HIDEWINDOW = 0x0080,
            SWP_NOACTIVATE = 0x0010,
            SWP_NOCOPYBITS = 0x0100,
            SWP_NOMOVE = 0x0002,
            SWP_NOOWNERZORDER = 0x0200,
            SWP_NOREDRAW = 0x0008,
            SWP_NOREPOSITION = 0x0200,
            SWP_NOSENDCHANGING = 0x0400,
            SWP_NOSIZE = 0x0001,
            SWP_NOZORDER = 0x0004,
            SWP_SHOWWINDOW = 0x0040
            // ReSharper restore UnusedMember.Local
        }

        [DllImport("user32.dll")]
        private static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
            {
                return GetWindowLongPtr64(hWnd, nIndex);
            }
            return GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetLastActivePopup(IntPtr hWnd);

        private enum GetAncestorFlags
        {
            // ReSharper disable UnusedMember.Local
            GetParent = 1,
            GetRoot = 2,
            GetRootOwner = 3
            // ReSharper restore UnusedMember.Local
        }

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestorFlags gaFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WindowPlacement lpwndpl);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowPlacement
        {
            public int length;
            public int flags;
            public int showCmd;
            public Point ptMinPosition;
            public Point ptMaxPosition;
            public Rectangle rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rectangle
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int EnumWindows(EnumWindowsProc ewp, int lParam);

        private delegate bool EnumWindowsProc(int hWnd, int lParam);
    }
}