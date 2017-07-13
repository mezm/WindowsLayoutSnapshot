using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WindowsLayoutSnapshot.Entities;
using WindowsLayoutSnapshot.Persistence;
using WindowsLayoutSnapshot.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace WindowsLayoutSnapshot.Tests.Persistence
{
    public class FileSnapshotStorageTests : IDisposable
    {
        private readonly FileSnapshotStorage _storage = new FileSnapshotStorage();
        private FileSnapshotStorage _restoreStorage;

        public void Dispose()
        {
            _storage.Dispose();
            _restoreStorage?.Dispose();

            if (File.Exists(FileSnapshotStorage.SnapshotsFilename))
            {
                File.Delete(FileSnapshotStorage.SnapshotsFilename);
            }

            var backups = Directory.GetFiles(Directory.GetCurrentDirectory()).Where(x => x.EndsWith(".bak"));
            foreach (var backup in backups)
            {
                File.Delete(backup);
            }
        }

        [Fact]
        public void GetAllSnapshots_NoSnapshots_ShouldReturnEmptyArray() => _storage.AllSnapshots.Should().BeEmpty();

        [Fact]
        public void GetAllSnapshots_StateFileBroken_ShouldReturnEmptyArrayAndRemoveFile()
        {
            File.WriteAllText(FileSnapshotStorage.SnapshotsFilename, @"{'a': 10}");

            _storage.AllSnapshots.Should().BeEmpty();

            _storage.Dispose();
            File.Exists(FileSnapshotStorage.SnapshotsFilename).Should().BeFalse();
            Directory.GetFiles(Directory.GetCurrentDirectory()).Should().Contain(x => x.EndsWith(".json.bak"));
        }

        [Fact]
        public async Task AddSnapshot_NoSnapshots_ShouldAddOne()
        {
            var snapshot = SnapshotUtils.MakeOne();

            await _storage.AddSnapshot(snapshot);

            _storage.AllSnapshots.Should().HaveCount(1).And.Contain(snapshot);
        }

        [Fact]
        public async Task AddSnapshot_FewSnapshotsAdded_ShouldAddOneToTheEnd()
        {
            var snapshot1 = SnapshotUtils.MakeOne();
            var snapshot2 = SnapshotUtils.MakeAnotherOne();
            var snapshot3 = SnapshotUtils.MakeYetAnotherOne();

            await _storage.AddSnapshot(snapshot1);
            await _storage.AddSnapshot(snapshot2);
            await _storage.AddSnapshot(snapshot3);

            _storage.AllSnapshots.Should().BeEquivalentTo(snapshot1, snapshot2, snapshot3);
        }

        [Fact]
        public async Task AddSnapshot_FewSnapshotsAddedConcurent_ShouldAddAll()
        {
            var snapshots = Enumerable.Range(0, 10).Select(_ => SnapshotUtils.MakeOne()).ToArray();

            await Task.WhenAll(snapshots.Select(x => _storage.AddSnapshot(x)));

            // ReSharper disable once CoVariantArrayConversion
            _storage.AllSnapshots.Should().BeEquivalentTo(snapshots);
        }

        [Fact]
        public async Task AddSnapshot_SnapshotsAlreadyAdded_ShouldThrow()
        {
            var snapshot = SnapshotUtils.MakeOne();

            await _storage.AddSnapshot(snapshot);

            _storage.Invoking(x => x.AddSnapshot(snapshot).Wait()).ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public async Task AddSnapshot_RestoreMode_ShouldRestorePrevState()
        {
            var snapshots = Enumerable.Range(0, 10).Select(_ => SnapshotUtils.MakeOne()).ToArray();

            await Task.WhenAll(snapshots.Select(x => _storage.AddSnapshot(x)));

            // ReSharper disable once CoVariantArrayConversion
            GetRestoredSnapshots().Should().BeEquivalentTo(snapshots);
            AssertSnapshotsFileExists();
        }

        [Fact]
        public async Task AddSnapshot_StateFileBroken_ShouldReturnEmptyArrayAndRemoveFile()
        {
            File.WriteAllText(FileSnapshotStorage.SnapshotsFilename, @"{'a': 10}");

            await _storage.AddSnapshot(SnapshotUtils.MakeOne());

            _storage.Dispose();
            File.Exists(FileSnapshotStorage.SnapshotsFilename).Should().BeTrue();
            Directory.GetFiles(Directory.GetCurrentDirectory()).Should().Contain(x => x.EndsWith(".json.bak"));
        }

        [Fact]  
        public async Task RemoveSnapshot_SnapshotExists_ShouldRemoveOne()
        {
            var snapshot1 = SnapshotUtils.MakeOne();
            var snapshot2 = SnapshotUtils.MakeAnotherOne();
            var snapshot3 = SnapshotUtils.MakeYetAnotherOne();

            await _storage.AddSnapshot(snapshot1);
            await _storage.AddSnapshot(snapshot2);
            await _storage.AddSnapshot(snapshot3);

            await _storage.RemoveSnapshot(snapshot2);

            _storage.AllSnapshots.Should().BeEquivalentTo(snapshot1, snapshot3);
        }

        [Fact]
        public async Task RemoveSnapshot_SnapshotExistsConcurently_ShouldRemoveOne()
        {
            var snapshots = Enumerable.Range(0, 30).Select(_ => SnapshotUtils.MakeOne()).ToArray();
            await Task.WhenAll(snapshots.Select(x => _storage.AddSnapshot(x)));

            var removed = new[] { 3, 21, 12, 13, 7 }.Select(x => snapshots[x]).ToArray();
            await Task.WhenAll(removed.Select(x => _storage.RemoveSnapshot(x)));

            _storage.AllSnapshots.Should().BeEquivalentTo(snapshots.Except(removed));
        }

        [Fact]
        public async Task RemoveSnapshot_SnapshotNotExists_ShouldThrow()
        {
            var snapshot1 = SnapshotUtils.MakeOne();
            var snapshot2 = SnapshotUtils.MakeAnotherOne();
            var snapshot3 = SnapshotUtils.MakeYetAnotherOne();

            await _storage.AddSnapshot(snapshot1);
            await _storage.AddSnapshot(snapshot3);

            _storage.Invoking(x => x.RemoveSnapshot(snapshot2).Wait()).ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public async Task RemoveSnapshot_RestoreMode_ShouldRestorePrevState()
        {
            var snapshots = Enumerable.Range(0, 30).Select(_ => SnapshotUtils.MakeOne()).ToArray();

            await Task.WhenAll(snapshots.Select(x => _storage.AddSnapshot(x)));

            await _storage.RemoveSnapshot(snapshots[19]);

            // ReSharper disable once CoVariantArrayConversion
            GetRestoredSnapshots().Should().BeEquivalentTo(snapshots.Except(new[] { snapshots[19] }));
            AssertSnapshotsFileExists();
        }

        [Fact]
        public async Task Clear_NoSnapshots_ShouldDoNothing()
        {
            await _storage.Clear();

            _storage.AllSnapshots.Should().BeEmpty();
        }

        [Fact]
        public async Task Clear_FewSnapshots_ShouldClear()
        {
            // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
            Enumerable.Range(0, 10).Select(_ => SnapshotUtils.MakeOne()).ToArray();

            await _storage.Clear();

            _storage.AllSnapshots.Should().BeEmpty();
        }

        [Fact]
        public async Task Clear_RestoreMode_ShouldRestorePrevState()
        {
            var snapshots = Enumerable.Range(0, 30).Select(_ => SnapshotUtils.MakeOne()).ToArray();

            await Task.WhenAll(snapshots.Select(x => _storage.AddSnapshot(x)));

            await _storage.Clear();

            GetRestoredSnapshots().Should().BeEmpty();
        }

        [Fact]
        public async Task AddAndRemove_Concurently_ShouldHaveCorrectState()
        {
            var snapshots = Enumerable.Range(0, 30).Select(_ => SnapshotUtils.MakeOne()).ToArray();
            var newSnapshot = SnapshotUtils.MakeYetAnotherOne(); 

            await Task.WhenAll(snapshots.Select(x => _storage.AddSnapshot(x)));
            
            await Task.WhenAll(
                _storage.RemoveSnapshot(snapshots[1]),
                _storage.AddSnapshot(newSnapshot),
                _storage.RemoveSnapshot(snapshots[18]));

            var expected = snapshots.Except(new[] { snapshots[1], snapshots[18] }).Concat(new[] { newSnapshot }).ToArray();
            // ReSharper disable CoVariantArrayConversion
            _storage.AllSnapshots.Should().BeEquivalentTo(expected);
            GetRestoredSnapshots().Should().BeEquivalentTo(expected);
            // ReSharper restore CoVariantArrayConversion
        }

        [Fact]
        public void AddSnapshot_UnableToPersist_ShouldThrow()
        {
            using (new FileStream(FileSnapshotStorage.SnapshotsFilename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                _storage.Invoking(x => x.AddSnapshot(SnapshotUtils.MakeOne())).ShouldThrow<IOException>();
            }
        }
        
        private Snapshot[] GetRestoredSnapshots()
        {
            _storage.Dispose();
            _restoreStorage = new FileSnapshotStorage();
            return _restoreStorage.AllSnapshots;
        }

        private static void AssertSnapshotsFileExists() => File.Exists(FileSnapshotStorage.SnapshotsFilename).Should().BeTrue();
    }
}