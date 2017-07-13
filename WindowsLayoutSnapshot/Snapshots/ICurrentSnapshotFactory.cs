using WindowsLayoutSnapshot.Entities;

namespace WindowsLayoutSnapshot.Snapshots
{
    public interface ICurrentSnapshotFactory
    {
        Snapshot TakeSnapshot(bool userInitiated = true);
    }
}