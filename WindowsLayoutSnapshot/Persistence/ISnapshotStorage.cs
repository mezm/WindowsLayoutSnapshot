using System.Threading.Tasks;

namespace WindowsLayoutSnapshot.Persistence
{
    public interface ISnapshotStorage
    {
        Task<Snapshot[]> GetAllSnapshots();
        Task AddSnapshot(Snapshot snapshot);
        Task RemoveSnapshot(Snapshot snapshot);
        Task Clear();
    }
}