using System.Threading.Tasks;

namespace WindowsLayoutSnapshot.Persistence
{
    public interface ISnapshotStorage
    {
        Snapshot[] AllSnapshots { get; }
        
        Task AddSnapshot(Snapshot snapshot);
        Task RemoveSnapshot(Snapshot snapshot);
        Task Clear();
    }
}