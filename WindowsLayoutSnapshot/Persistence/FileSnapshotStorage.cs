using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Jil;

namespace WindowsLayoutSnapshot.Persistence
{
    public sealed class FileSnapshotStorage : ISnapshotStorage, IDisposable
    {
        public const string SnapshotsFilename = "snapshots.json";

        private readonly Lazy<ConcurrentDictionary<Guid, Snapshot>> _snapshots = new Lazy<ConcurrentDictionary<Guid, Snapshot>>(RestoreState);
        private readonly ActionBlock<Snapshot[]> _persistBlock = new ActionBlock<Snapshot[]>(x => PersistState(x));
        
        private Snapshot[] CurrentState => _snapshots.Value.Select(x => x.Value).OrderBy(x => x.TimeTaken).ToArray(); // avoid global lock
        
        public Task<Snapshot[]> GetAllSnapshots()
        {
            //Interlocked.Exchange()
            var snapshots = CurrentState;
            return Task.FromResult(snapshots);
        }

        public Task AddSnapshot(Snapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            if (!_snapshots.Value.TryAdd(snapshot.Id, snapshot))
            {
                throw new InvalidOperationException($"Snapshot {snapshot.Id:D} has already been added to the storage");
            }

            return QueuePersist();
        }

        public Task RemoveSnapshot(Snapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            if (!_snapshots.Value.TryRemove(snapshot.Id, out var _))
            {
                throw new InvalidOperationException($"Snapshot {snapshot.Id:D} has not been added to the storage");
            }

            return QueuePersist();
        }

        public Task Clear()
        {
            _snapshots.Value.Clear();
            return QueuePersist();
        }

        public void Dispose()
        {
            _persistBlock.Complete();
            _persistBlock.Completion.Wait(TimeSpan.FromSeconds(5));
        }

        private Task QueuePersist() => _persistBlock.SendAsync(CurrentState);

        private static ConcurrentDictionary<Guid, Snapshot> RestoreState()
        {
            if (!File.Exists(SnapshotsFilename)) return new ConcurrentDictionary<Guid, Snapshot>();

            using (var file = new FileStream(SnapshotsFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(file))
            {
                var snapshots = JSON.Deserialize<Snapshot[]>(reader);
                return new ConcurrentDictionary<Guid, Snapshot>(snapshots.ToDictionary(x => x.Id));
            }
        }

        private static void PersistState(Snapshot[] state)
        {
            using (var file = new FileStream(SnapshotsFilename, FileMode.OpenOrCreate)) 
            using (var writter = new StreamWriter(file))
            {
                file.Seek(0, SeekOrigin.Begin);
                file.SetLength(0);
                JSON.Serialize(state, writter);
            }
        }
    }
}