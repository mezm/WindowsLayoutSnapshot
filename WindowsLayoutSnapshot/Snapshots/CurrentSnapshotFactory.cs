using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsLayoutSnapshot.Entities;
using WindowsLayoutSnapshot.WinApiInterop;

namespace WindowsLayoutSnapshot.Snapshots
{
    public class CurrentSnapshotFactory : ICurrentSnapshotFactory
    {
        public Snapshot TakeSnapshot(bool userInitiated = true) => new SnapshotSession(userInitiated).Take();

        private class SnapshotSession
        {
            private readonly bool _userInitiated;
            private readonly List<WindowReference> _windows = new List<WindowReference>();
            private int _zOrder;

            public SnapshotSession(bool userInitiated) => _userInitiated = userInitiated;

            public Snapshot Take()
            {
                WinApi.EnumWindows(EvalWindow, 0);
                var monitorPixelCounts = Screen.AllScreens.Select(screen => (long)screen.Bounds.Width * screen.Bounds.Height).ToArray();
                return new Snapshot(_userInitiated, monitorPixelCounts, _windows.ToArray());
            }

            private bool EvalWindow(int hwndInt, int lParam)
            {
                var handler = new IntPtr(hwndInt);

                if (!IsAltTabWindow(handler))
                {
                    return true;
                }

                var placement = new WindowPlacement { length = Marshal.SizeOf(typeof(WindowPlacement)) };
                if (!WinApi.GetWindowPlacement(handler, ref placement))
                {
                    throw new Exception("Error getting window placement"); // todo: don't use generic exception
                }
                
                _windows.Add(new WindowReference(handler, placement, _zOrder++));
                return true;
            }

            private static bool IsAltTabWindow(IntPtr hwnd)
            {
                if (!WinApi.IsWindowVisible(hwnd))
                {
                    return false;
                }

                var extendedStyles = hwnd.GetWindowLongPointer();
                if (extendedStyles.IsTopLevelWindow()) return true;
                if (extendedStyles.IsToolbar()) return false;
                
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
        }
    }
}