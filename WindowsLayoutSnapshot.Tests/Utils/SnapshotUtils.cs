using WindowsLayoutSnapshot.Entities;

namespace WindowsLayoutSnapshot.Tests.Utils
{
    public static class SnapshotUtils
    {
        public static Snapshot MakeOne() => new Snapshot(false, new long[] { 1327104 }, new[] { WindowReferenceUtils.MakeOne() });

        public static Snapshot MakeAnotherOne() => new Snapshot(true,
            new long[] { 1327104, 804864 },
            new[] { WindowReferenceUtils.MakeOne(), WindowReferenceUtils.MakeYetAnotherOne() });

        public static Snapshot MakeYetAnotherOne() => new Snapshot(true,
            new long[] { 1327104 },
            new[] { WindowReferenceUtils.MakeYetAnotherOne(), WindowReferenceUtils.MakeAnotherOne() });
    }
}