using System;
using WindowsLayoutSnapshot.Entities;
using WindowsLayoutSnapshot.WinApiInterop;

namespace WindowsLayoutSnapshot.Tests.Utils
{
    public static class WindowReferenceUtils
    {
        public static WindowReference MakeOne() => new WindowReference(new IntPtr(4464), new WindowPlacement(), 4);
        public static WindowReference MakeAnotherOne() => new WindowReference(new IntPtr(113), new WindowPlacement(), 0);
        public static WindowReference MakeYetAnotherOne() => new WindowReference(new IntPtr(7778), new WindowPlacement(), 14);
    }
}