using System;
using System.Runtime.InteropServices;

namespace WindowsLayoutSnapshot
{
    // Open source
    // Imported by: Adam Smith
    // Imported on: 8/9/2012
    // Imported from: http://www.codeproject.com/Articles/2286/Window-Hiding-with-C
    // License: CPOL (liberal)
    // Modifications: cleanup

    public class Window
    {
        private bool m_Visible = true;
        private bool m_WasMax;

        public Window(string title, IntPtr handler, string process)
        {
            Title = title;
            Handler = handler;
            Process = process;
        }

        public IntPtr Handler { get; }

        public string Title { get; }

        public string Process { get; }

        public bool Visible
        {
            get => m_Visible;
            set
            {
                //show the window
                if (value)
                {
                    if (m_WasMax)
                    {
                        if (ShowWindowAsync(Handler, SW_SHOWMAXIMIZED))
                            m_Visible = true;
                    }
                    else
                    {
                        if (ShowWindowAsync(Handler, SW_SHOWNORMAL))
                            m_Visible = true;
                    }
                }
                else
                {
                    m_WasMax = IsZoomed(Handler);
                    if (ShowWindowAsync(Handler, SW_HIDE))
                        m_Visible = false;
                }
            }
        }

        public void Activate()
        {
            if (Handler == GetForegroundWindow())
            {
                return;
            }

            IntPtr threadId1 = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
            IntPtr threadId2 = GetWindowThreadProcessId(Handler, IntPtr.Zero);

            if (threadId1 != threadId2)
            {
                AttachThreadInput(threadId1, threadId2, 1);
                SetForegroundWindow(Handler);
                AttachThreadInput(threadId1, threadId2, 0);
            }
            else
            {
                SetForegroundWindow(Handler);
            }

            ShowWindowAsync(Handler, IsIconic(Handler) ? SW_RESTORE : SW_SHOWNORMAL);
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("user32.dll")]
        private static extern IntPtr AttachThreadInput(IntPtr idAttach, IntPtr idAttachTo, int fAttach);

        // ReSharper disable UnusedMember.Local
        private const int SW_HIDE = 0;
        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_SHOWMAXIMIZED = 3;
        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_RESTORE = 9;
        private const int SW_SHOWDEFAULT = 10;
        // ReSharper restore UnusedMember.Local
    }
}