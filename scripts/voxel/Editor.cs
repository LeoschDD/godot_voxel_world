using Godot;
using System;
using System.Collections.Generic;

namespace Voxel
{
	public class Editor
	{
		private Dictionary<ChunkKey, List<IModifier>> _pendingEdits = new();
		private WorldSettings _worldSettings;

		public Dictionary<ChunkKey, List<IModifier>>.KeyCollection PendingEdits => _pendingEdits.Keys;

		public Editor(WorldSettings worldSettings)
		{
			_worldSettings = worldSettings;	
		}

		public void Edit(IModifier modifier, DataChunkStorage dataChunkStorage, MeshChunkStorage meshChunkStorage, Generator generator)
		{
			if (!modifier.Bounds().HasValue) return;

			Vector3 min = modifier.Bounds().Value.Position;
			Vector3 max = min + modifier.Bounds().Value.Size;

			Vector3I minCoord = CoordFromWorld(min, 0);
			Vector3I maxCoord = CoordFromWorld(max, 0);

			for (int lod = 0; lod <= _worldSettings.MaxLod; lod++)
			{
				Vector3I lodMin = new Vector3I(minCoord.X >> lod, minCoord.Y >> lod, minCoord.Z >> lod);
				Vector3I lodMax = new Vector3I(maxCoord.X >> lod, maxCoord.Y >> lod, maxCoord.Z >> lod);

				for (int x = lodMin.X; x <= lodMax.X; x++)
				for (int y = lodMin.Y; y <= lodMax.Y; y++)
				for (int z = lodMin.Z; z <= lodMax.Z; z++)
				{
					var key = new ChunkKey(new Vector3I(x, y, z), lod);

					if (!dataChunkStorage.TryGetDataChunk(key, out var dataChunk))
					{
						if (dataChunkStorage.TrySchedule(key, generator)) 
						{
							if (!_pendingEdits.ContainsKey(key)) _pendingEdits[key] = new List<IModifier>();
							_pendingEdits[key].Add(modifier);
						}
						return;
					}
					EditChunk(dataChunk, modifier, meshChunkStorage);
				}
			}
		}

		public void ApplyPendingEdits(DataChunk dataChunk, MeshChunkStorage meshChunkStorage)
		{
			if (!_pendingEdits.TryGetValue(dataChunk.Key, out var modifiers) || modifiers == null) return;
			foreach (var modifier in modifiers) EditChunk(dataChunk, modifier, meshChunkStorage);
			_pendingEdits.Remove(dataChunk.Key);
		}

		private Vector3I CoordFromWorld(Vector3 point, int lod)
		{
			int x = Mathf.FloorToInt((point.X + _worldSettings.WorldSize * 0.5f) / _worldSettings.ChunkSize) >> lod;	
			int y = Mathf.FloorToInt((point.Y + _worldSettings.WorldSize * 0.5f) / _worldSettings.ChunkSize) >> lod;	
			int z = Mathf.FloorToInt((point.Z + _worldSettings.WorldSize * 0.5f) / _worldSettings.ChunkSize) >> lod;	
			return new Vector3I(x, y, z);
		}

		private void EditChunk(DataChunk dataChunk, IModifier modifier, MeshChunkStorage meshChunkStorage)
		{
			for (int x = 0; x < dataChunk.Size; x++)
			for (int y = 0; y < dataChunk.Size; y++)
			for (int z = 0; z < dataChunk.Size; z++)
			{
				var localCoord = new Vector3I(x, y, z);
				var point = -Vector3.One * _worldSettings.WorldSize * 0.5f + (dataChunk.Key.Coord * _worldSettings.ChunkSize + localCoord) * (1 << dataChunk.Key.Lod);

				var density = dataChunk.Get(localCoord);
				if (modifier.Type() == ModifierType.Add) density = Mathf.Min(density, modifier.Sample(point));
				if (modifier.Type() == ModifierType.Subtract) density = Mathf.Max(density, -modifier.Sample(point));

				dataChunk.Get(localCoord) = density;

				dataChunk.Min = Mathf.Min(dataChunk.Min, density);
				dataChunk.Max = Mathf.Max(dataChunk.Max, density);
			}
			if (meshChunkStorage.TryGetMeshChunk(dataChunk.Key, out var meshChunk))
			{
				meshChunk.State = MeshState.Dirty;
			}
			dataChunk.Edited = true;
		}
	}
}