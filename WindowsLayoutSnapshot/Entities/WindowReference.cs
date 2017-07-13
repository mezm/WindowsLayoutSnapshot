using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using WindowsLayoutSnapshot.WinApiInterop;
using Jil;

namespace WindowsLayoutSnapshot.Entities
{
    public class WindowReference : IEquatable<WindowReference>
    {
        // for deserialization
        private WindowReference()
        {
            // todo: find handler
        }

        public WindowReference(IntPtr handler, WindowPlacement placement)
        {
            Handler = handler;
            Placement = placement;
            ProcessFilePath = GetProcessPath();
            Title = GetWindowTitle();
        }

        [JilDirective(Ignore = true)]
        public IntPtr Handler { get; private set; }

        public string ProcessFilePath { get; private set; }
        public string Title { get; private set; }
        public WindowPlacement Placement { get; private set; }

        private string GetProcessPath()
        {
            if (Handler == IntPtr.Zero) return string.Empty;

            WinApi.GetWindowThreadProcessId(Handler, out var processId);
            if (processId == 0) return string.Empty;

            try
            {
                var process = Process.GetProcessById((int) processId);
                return process.MainModule.FileName;
            }
            catch (ArgumentException)
            {
                // The process specified by the processId parameter is not running. The identifier might be expired.
            }
            catch (InvalidOperationException)
            {
                // The process was not started by this object.
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
            {
                // Access denied
            }

            return string.Empty;
        }

        private string GetWindowTitle()
        {
            var length = WinApi.GetWindowTextLength(Handler);
            if (length == 0) return string.Empty;

            var title = new StringBuilder(length);
            WinApi.GetWindowText(Handler, title, length + 1);
            return title.ToString();
        }

        public bool Equals(WindowReference other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Handler.Equals(other.Handler);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((WindowReference) obj);
        }

        public override int GetHashCode() => Handler.GetHashCode();
    }
}