using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WindowsLayoutSnapshot.Persistence;
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
        }

        [Fact]
        public async Task GetAllSnapshots_NoSnapshots_ShouldReturnEmptyArray()
        {
            var storages = await GetCurrentSnapshots();

            storages.Should().BeEmpty();
        }

        [Fact]
        public async Task AddSnapshot_NoSnapshots_ShouldAddOne()
        {
            var snapshot = new Snapshot(true);

            await _storage.AddSnapshot(snapshot);

            var snapshots = await GetCurrentSnapshots();
            snapshots.Should().HaveCount(1).And.Contain(snapshot);
        }

        [Fact]
        public async Task AddSnapshot_FewSnapshotsAdded_ShouldAddOneToTheEnd()
        {
            var snapshot1 = new Snapshot(true);
            var snapshot2 = new Snapshot(true);
            var snapshot3 = new Snapshot(true);

            await _storage.AddSnapshot(snapshot1);
            await _storage.AddSnapshot(snapshot2);
            await _storage.AddSnapshot(snapshot3);

            var snapshots = await GetCurrentSnapshots();
            snapshots.Should().BeEquivalentTo(snapshot1, snapshot2, snapshot3);
        }

        [Fact]
        public async Task AddSnapshot_FewSnapshotsAddedConcurent_ShouldAddAll()
        {
            var snapshots = Enumerable.Range(0, 10).Select(_ => new Snapshot(true)).ToArray();

            await Task.WhenAll(snapshots.Select(x => _storage.AddSnapshot(x)));

            var actualSnapshots = await GetCurrentSnapshots();
            // ReSharper disable once CoVariantArrayConversion
            actualSnapshots.Should().BeEquivalentTo(snapshots);
        }

        [Fact]
        public async Task AddSnapshot_SnapshotsAlreadyAdded_ShouldThrow()
        {
            var snapshot = new Snapshot(true);
            
            await _storage.AddSnapshot(snapshot);

            _storage.Invoking(x => x.AddSnapshot(snapshot).Wait()).ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public async Task AddSnapshot_RestoreMode_ShouldRestorePrevState()
        {
            var snapshots = Enumerable.Range(0, 10).Select(_ => new Snapshot(true)).ToArray();

            await Task.WhenAll(snapshots.Select(x => _storage.AddSnapshot(x)));

            var restored = await GetRestoredSnapshots();
            // ReSharper disable once CoVariantArrayConversion
            restored.Should().BeEquivalentTo(snapshots);
            AssertSnapshotsFileExists();
        }

        [Fact]
        public async Task RemoveSnapshot_SnapshotExists_ShouldRemoveOne()
        {
            var snapshot1 = new Snapshot(true);
            var snapshot2 = new Snapshot(true);
            var snapshot3 = new Snapshot(true);

            await _storage.AddSnapshot(snapshot1);
            await _storage.AddSnapshot(snapshot2);
            await _storage.AddSnapshot(snapshot3);

            await _storage.RemoveSnapshot(snapshot2);

            var snapshots = await GetCurrentSnapshots();
            snapshots.Should().BeEquivalentTo(snapshot1, snapshot3);
        }

        [Fact]
        public async Task RemoveSnapshot_SnapshotExistsConcurently_ShouldRemoveOne()
        {
            var snapshots = Enumerable.Range(0, 30).Select(_ => new Snapshot(true)).ToArray();
            await Task.WhenAll(snapshots.Select(x => _storage.AddSnapshot(x)));

            var removed = new[] { 3, 21, 12, 13, 7 }.Select(x => snapshots[x]).ToArray();
            await Task.WhenAll(removed.Select(x => _storage.RemoveSnapshot(x)));

            var actualSnapshots = await GetCurrentSnapshots();
            actualSnapshots.Should().BeEquivalentTo(snapshots.Except(removed));
        }

        [Fact]
        public async Task RemoveSnapshot_SnapshotNotExists_ShouldThrow()
        {
            var snapshot1 = new Snapshot(true);
            var snapshot2 = new Snapshot(true);
            var snapshot3 = new Snapshot(true);

            await _storage.AddSnapshot(snapshot1);
            await _storage.AddSnapshot(snapshot3);

            _storage.Invoking(x => x.RemoveSnapshot(snapshot2).Wait()).ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public async Task RemoveSnapshot_RestoreMode_ShouldRestorePrevState()
        {
            var snapshots = Enumerable.Range(0, 30).Select(_ => new Snapshot(true)).ToArray();

            await Task.WhenAll(snapshots.Select(x => _storage.AddSnapshot(x)));

            await _storage.RemoveSnapshot(snapshots[19]);

            var restored = await GetRestoredSnapshots();
            // ReSharper disable once CoVariantArrayConversion
            restored.Should().BeEquivalentTo(snapshots.Except(new[] { snapshots[19] }));
            AssertSnapshotsFileExists();
        }

        [Fact]
        public async Task Clear_NoSnapshots_ShouldDoNothing()
        {
            await _storage.Clear();

            var snapshots = await GetCurrentSnapshots();
            snapshots.Should().BeEmpty();
        }

        [Fact]
        public async Task Clear_FewSnapshots_ShouldClear()
        {
            // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
            Enumerable.Range(0, 10).Select(_ => new Snapshot(true)).ToArray();

            await _storage.Clear();

            var snapshots = await GetCurrentSnapshots();
            snapshots.Should().BeEmpty();
        }

        [Fact]
        public async Task Clear_RestoreMode_ShouldRestorePrevState()
        {
            var snapshots = Enumerable.Range(0, 30).Select(_ => new Snapshot(true)).ToArray();

            await Task.WhenAll(snapshots.Select(x => _storage.AddSnapshot(x)));

            await _storage.Clear();

            var restored = await GetRestoredSnapshots();
            restored.Should().BeEmpty();
        }

        [Fact]
        public async Task AddAndRemove_Concurently_ShouldHaveCorrectState()
        {
            var snapshots = Enumerable.Range(0, 30).Select(_ => new Snapshot(true)).ToArray();
            var newSnapshot = new Snapshot(true);

            await Task.WhenAll(snapshots.Select(x => _storage.AddSnapshot(x)));
            
            await Task.WhenAll(
                _storage.RemoveSnapshot(snapshots[1]),
                _storage.AddSnapshot(newSnapshot),
                _storage.RemoveSnapshot(snapshots[18]));

            var expected = snapshots.Except(new[] { snapshots[1], snapshots[18] }).Concat(new[] { newSnapshot }).ToArray();
            var actual = await GetCurrentSnapshots();
            // ReSharper disable once CoVariantArrayConversion
            actual.Should().BeEquivalentTo(expected);
            
            var restored = await GetRestoredSnapshots();
            // ReSharper disable once CoVariantArrayConversion
            restored.Should().BeEquivalentTo(restored);
        }

        private Task<Snapshot[]> GetCurrentSnapshots() => _storage.GetAllSnapshots();

        private Task<Snapshot[]> GetRestoredSnapshots()
        {
            _storage.Dispose();
            _restoreStorage = new FileSnapshotStorage();
            return _restoreStorage.GetAllSnapshots();
        }

        private static void AssertSnapshotsFileExists() => File.Exists(FileSnapshotStorage.SnapshotsFilename).Should().BeTrue();

        // todo: broken file
        // todo: first read from file
    }
}