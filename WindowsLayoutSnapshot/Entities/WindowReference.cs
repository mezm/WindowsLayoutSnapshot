using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using WindowsLayoutSnapshot.WinApiInterop;

namespace WindowsLayoutSnapshot.Entities
{
    public class WindowReference : IEquatable<WindowReference>
    {
        // for deserialization
        private WindowReference()
        {
        }

        public WindowReference(IntPtr handler)
        {
            Handler = handler;
            ProcessFilePath = GetProcessPath();
            Title = GetWindowTitle();
        }

        public IntPtr Handler { get; set; }
        public string ProcessFilePath { get; set; }
        public string Title { get; set; }

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
            catch (Win32Exception ex) when(ex.NativeErrorCode == 5) 
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