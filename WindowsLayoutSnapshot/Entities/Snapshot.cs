using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsLayoutSnapshot.WinApiInterop;

namespace WindowsLayoutSnapshot.Entities
{
    public class Snapshot : IEquatable<Snapshot>
    {
        private readonly Dictionary<WindowReference, WindowPlacement> m_placements = new Dictionary<WindowReference, WindowPlacement>();
        private List<IntPtr> m_windowsBackToTop = new List<IntPtr>();

        // for deserialization
        protected Snapshot()
        {
        }

        public Snapshot(bool userInitiated)
        {
            WinApi.EnumWindows(EvalWindow, 0);
            
            UserInitiated = userInitiated;
            MonitorPixelCounts = Screen.AllScreens.Select(screen => (long)screen.Bounds.Width * screen.Bounds.Height).ToArray();
            NumMonitors = MonitorPixelCounts.Length;
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
                throw new Exception("Error getting window placement"); // todo: don't use generic exception
            }
            m_placements.Add(new WindowReference(hwnd), placement);
            
            return true;
        }

        public Guid Id { get; protected set; } = Guid.NewGuid();
        public DateTime TimeTaken { get; protected set; } = DateTime.UtcNow;
        public bool UserInitiated { get; protected set; }
        public long[] MonitorPixelCounts { get; protected set; }
        public int NumMonitors { get; protected set; }

        public TimeSpan Age => DateTime.UtcNow.Subtract(TimeTaken);

        public void RestoreAndPreserveMenu(object sender, EventArgs e)
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
                TrayIconForm.TrayMenu.Visible = true;
            }
        }

        public void Restore(object sender, EventArgs e)
        {
            // ignore extra params
            // first, restore the window rectangles and normal/maximized/minimized states
            foreach (var placement in m_placements)
            {
                // this might error out if the window no longer exists
                var placementValue = placement.Value;

                // make sure points and rects will be inside monitor
                var extendedStyles = placement.Key.Handler.GetWindowLongPointer();
                placementValue.ptMaxPosition = GetUpperLeftCornerOfNearestMonitor(extendedStyles, placementValue.ptMaxPosition);
                placementValue.ptMinPosition = GetUpperLeftCornerOfNearestMonitor(extendedStyles, placementValue.ptMinPosition);
                placementValue.rcNormalPosition = GetRectInsideNearestMonitor(extendedStyles, placementValue.rcNormalPosition);

                WinApi.SetWindowPlacement(placement.Key.Handler, ref placementValue);
            }

            // now update the z-orders
            m_windowsBackToTop = m_windowsBackToTop.FindAll(WinApi.IsWindowVisible);
            var positionStructure = WinApi.BeginDeferWindowPos(m_windowsBackToTop.Count);
            for (var i = 0; i < m_windowsBackToTop.Count; i++)
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

            var rectAsRectangle = new Rectangle(rect.Left, rect.Top, width, height);
            var monitorRect = (windowExtendedStyles.ToInt64() & 0x00000080) > 0 ? Screen.GetBounds(rectAsRectangle) : Screen.GetWorkingArea(rectAsRectangle);

            var result = new Rect
            {
                Left = Math.Max(monitorRect.Left, Math.Min(monitorRect.Right - width, rect.Left)),
                Top = Math.Max(monitorRect.Top, Math.Min(monitorRect.Bottom - height, rect.Top))
            };
            result.Right = result.Left + Math.Min(monitorRect.Width, width);
            result.Bottom = result.Top + Math.Min(monitorRect.Height, height);
            return result;
        }

        private static bool IsAltTabWindow(IntPtr hwnd)
        {
            if (!WinApi.IsWindowVisible(hwnd))
            {
                return false;
            }

            var extendedStyles = hwnd.GetWindowLongPointer();
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

            var hwndTry = WinApi.GetAncestor(hwnd, GetAncestorFlags.GetRootOwner);
            var hwndWalk = IntPtr.Zero;
            while (hwndTry != hwndWalk)
            {
                hwndWalk = hwndTry;
                hwndTry = WinApi.GetLastActivePopup(hwndWalk);
                if (WinApi.IsWindowVisible(hwndTry))
                {
                    break;
                }
            }
            
            return hwndWalk == hwnd;
        }

        public bool Equals(Snapshot other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Snapshot) obj);
        }

        public override int GetHashCode() => Id.GetHashCode();
    }
}