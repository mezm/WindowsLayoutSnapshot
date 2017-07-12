using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsLayoutSnapshot.WinApiInterop;

namespace WindowsLayoutSnapshot
{
    public class Snapshot
    {
        private readonly Dictionary<IntPtr, WindowPlacement> m_placements = new Dictionary<IntPtr, WindowPlacement>();
        private List<IntPtr> m_windowsBackToTop = new List<IntPtr>();

        public Snapshot(bool userInitiated)
        {
            WinApi.EnumWindows(EvalWindow, 0);

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
            if (!WinApi.GetWindowPlacement(hwnd, ref placement))
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
            var currentForegroundWindow = WinApi.GetForegroundWindow();

            try
            {
                Restore(sender, e);
            }
            finally
            {
                // A combination of SetForegroundWindow + SetWindowPos (via set_Visible) seems to be needed
                // This was determined by trying a bunch of stuff
                // This prevents the tray menu from closing, and makes sure it's still on top
                WinApi.SetForegroundWindow(currentForegroundWindow);
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

                WinApi.SetWindowPlacement(placement.Key, ref placementValue);
            }

            // now update the z-orders
            m_windowsBackToTop = m_windowsBackToTop.FindAll(WinApi.IsWindowVisible);
            IntPtr positionStructure = WinApi.BeginDeferWindowPos(m_windowsBackToTop.Count);
            for (int i = 0; i < m_windowsBackToTop.Count; i++)
            {
                positionStructure = WinApi.DeferWindowPos(positionStructure,
                    m_windowsBackToTop[i],
                    i == 0 ? IntPtr.Zero : m_windowsBackToTop[i - 1],
                    0,
                    0,
                    0,
                    0,
                    DeferWindowPosCommands.SWP_NOMOVE | DeferWindowPosCommands.SWP_NOSIZE | DeferWindowPosCommands.SWP_NOACTIVATE);
            }
            WinApi.EndDeferWindowPos(positionStructure);
        }

        private static Point GetUpperLeftCornerOfNearestMonitor(IntPtr windowExtendedStyles, Point point)
        {
            return (windowExtendedStyles.ToInt64() & 0x00000080) > 0 ? Screen.GetBounds(point).Location : Screen.GetWorkingArea(point).Location;
        }

        private static Rect GetRectInsideNearestMonitor(IntPtr windowExtendedStyles, Rect rect)
        {
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;

            var rectAsRectangle = new System.Drawing.Rectangle(rect.Left, rect.Top, width, height);
            var monitorRect = (windowExtendedStyles.ToInt64() & 0x00000080) > 0 ? Screen.GetBounds(rectAsRectangle) : Screen.GetWorkingArea(rectAsRectangle);

            var y = new Rect
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
            if (!WinApi.IsWindowVisible(hwnd))
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

            IntPtr hwndTry = WinApi.GetAncestor(hwnd, GetAncestorFlags.GetRootOwner);
            IntPtr hwndWalk = IntPtr.Zero;
            while (hwndTry != hwndWalk)
            {
                hwndWalk = hwndTry;
                hwndTry = WinApi.GetLastActivePopup(hwndWalk);
                if (WinApi.IsWindowVisible(hwndTry))
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

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? WinApi.GetWindowLongPtr64(hWnd, nIndex) : WinApi.GetWindowLongPtr32(hWnd, nIndex);
        }
    }
}