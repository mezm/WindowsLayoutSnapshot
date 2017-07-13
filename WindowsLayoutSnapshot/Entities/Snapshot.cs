using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WindowsLayoutSnapshot.WinApiInterop;
using Jil;

namespace WindowsLayoutSnapshot.Entities
{
    public class Snapshot : IEquatable<Snapshot>
    {
        // for deserialization
        // ReSharper disable once UnusedMember.Local
        private Snapshot()
        {
        }

        public Snapshot(bool userInitiated, long[] monitorPixelCounts, WindowReference[] windows)
        {
            Id = Guid.NewGuid();
            TimeTaken = DateTime.UtcNow;
            UserInitiated = userInitiated;
            MonitorPixelCounts = monitorPixelCounts;
            Windows = windows;
        }

        // for deserialization
        // ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
        public Guid Id { get; private set; }
        public DateTime TimeTaken { get; private set; } 
        public bool UserInitiated { get; private set; }
        public long[] MonitorPixelCounts { get; private set; }
        public WindowReference[] Windows { get; private set; }
        // ReSharper restore AutoPropertyCanBeMadeGetOnly.Local

        [JilDirective(Ignore = true)]
        public TimeSpan Age => DateTime.UtcNow.Subtract(TimeTaken);

        [JilDirective(Ignore = true)]
        public int NumberOfMonitors => MonitorPixelCounts.Length;

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
            foreach (var window in Windows)
            {
                // this might error out if the window no longer exists
                var placementValue = window.Placement;

                // make sure points and rects will be inside monitor
                var extendedStyles = window.Handler.GetWindowLongPointer();
                placementValue.ptMaxPosition = GetUpperLeftCornerOfNearestMonitor(extendedStyles, placementValue.ptMaxPosition);
                placementValue.ptMinPosition = GetUpperLeftCornerOfNearestMonitor(extendedStyles, placementValue.ptMinPosition);
                placementValue.rcNormalPosition = GetRectInsideNearestMonitor(extendedStyles, placementValue.rcNormalPosition);

                WinApi.SetWindowPlacement(window.Handler, ref placementValue);
            }

            // now update the z-orders
            var windowsBackToTop = Windows.Where(x => WinApi.IsWindowVisible(x.Handler)).ToArray();
            var positionStructure = WinApi.BeginDeferWindowPos(windowsBackToTop.Length);
            for (var i = 0; i < windowsBackToTop.Length; i++)
            {
                positionStructure = WinApi.DeferWindowPos(
                    positionStructure,
                    windowsBackToTop[i].Handler,
                    i == 0 ? IntPtr.Zero : windowsBackToTop[i - 1].Handler,
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
            if (obj.GetType() != GetType()) return false;
            return Equals((Snapshot) obj);
        }

        // Id is been initialized only once, but can be not in constructor
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        public override int GetHashCode() => Id.GetHashCode();
    }
}