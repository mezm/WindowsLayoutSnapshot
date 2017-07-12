using System.Threading.Tasks;

namespace WindowsLayoutSnapshot.Persistence
{
    public class FileSnapshotStorage : ISnapshotStorage
    {
        public Task<Snapshot[]> GetAllSnapshots()
        {
            throw new System.NotImplementedException();
        }

        public Task AddSnapshot(Snapshot snapshot)
        {
            throw new System.NotImplementedException();
        }

        public Task RemoveSnapshot(Snapshot snapshot)
        {
            throw new System.NotImplementedException();
        }

        public Task Clear()
        {
            throw new System.NotImplementedException();
        }
    }
}