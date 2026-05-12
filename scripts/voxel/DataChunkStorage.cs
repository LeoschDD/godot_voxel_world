using Godot;
using System;
using System.Collections.Generic;

namespace Voxel
{
	public class DataChunkStorage
	{
		private Dictionary<ChunkKey, DataChunk> _dataChunks = new();
		private HashSet<ChunkKey> _scheduledDataChunks = new();
		private DataChunkScheduler _dataChunkScheduler = new();
		private WorldSettings _worldSettings;

		public DataChunkStorage(WorldSettings worldSettings)
		{
			_worldSettings = worldSettings;	
		}

		public bool TryGetDataChunk(ChunkKey key, out DataChunk dataChunk)
		{
			return _dataChunks.TryGetValue(key, out dataChunk);
		}

		public bool ContainsDataChunk(ChunkKey key)
		{
			return _dataChunks.ContainsKey(key);
		}

		public bool TrySchedule(ChunkKey key, Generator generator)
		{
			if (_dataChunks.ContainsKey(key) || _scheduledDataChunks.Contains(key)) return false;

			if (_dataChunkScheduler.Schedule(key, () => CreateDataChunk(key, generator)))
			{
				_scheduledDataChunks.Add(key);
				return true;
			}
			return false;
		}

		public void CollectScheduled(MeshChunkStorage meshChunkStorage, Action<DataChunk, MeshChunkStorage> applyPendingEdits)
		{
			for (int i = 0; i < 16; i++)
			{
				if (!_dataChunkScheduler.TryGet(out var dataChunk)) break;
				_scheduledDataChunks.Remove(dataChunk.Key);
				if (_dataChunks.ContainsKey(dataChunk.Key)) continue;
				_dataChunks[dataChunk.Key] = dataChunk;
				applyPendingEdits(dataChunk, meshChunkStorage);
			}
		}

		public void RemoveUnused(HashSet<ChunkKey> neededDataChunks, Dictionary<ChunkKey, List<IModifier>>.KeyCollection pendingEdits)
		{
			var removableDataChunks = new List<ChunkKey>();
			foreach (var key in _dataChunks.Keys)
			{
				if (neededDataChunks.Contains(key)) continue;
				if (_dataChunks[key].Edited || pendingEdits.Contains(key)) continue;
				removableDataChunks.Add(key);
			}

			foreach(var key in removableDataChunks) 
			{
				_dataChunks.Remove(key);
			}
		}

		private DataChunk CreateDataChunk(ChunkKey key, Generator generator)
		{
			var dataChunk = new DataChunk(key, _worldSettings);

			for (int x = 0; x < dataChunk.Size; x++)
			for (int y = 0; y < dataChunk.Size; y++)
			for (int z = 0; z < dataChunk.Size; z++)
			{
				var localCoord = new Vector3I(x, y, z);
				var point = -Vector3.One * _worldSettings.WorldSize * 0.5f + (key.Coord * _worldSettings.ChunkSize + localCoord) * (1 << key.Lod);
				var density = generator.Sample(point);

				dataChunk.Get(localCoord) = density;
				dataChunk.Min = Mathf.Min(dataChunk.Min, density);
				dataChunk.Max = Mathf.Max(dataChunk.Max, density);
			}
			return dataChunk;
		}
	}
}