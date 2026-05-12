using Godot;
using System;
using System.Collections.Generic;

namespace Voxel
{
	public class MeshChunkStorage
	{
		private Dictionary<ChunkKey, MeshChunk> _meshChunks = new();
		private WorldSettings _worldSettings;

		public MeshChunkStorage(WorldSettings worldSettings)
		{
			_worldSettings = worldSettings;
		}

		public MeshChunk EnsureMeshChunk(OctreeChunk leaf)
		{
			if (TryGetMeshChunk(leaf.Key, out var meshChunk)) return meshChunk;
			meshChunk = new MeshChunk(leaf.Key, leaf.Bounds);
			_meshChunks[leaf.Key] = meshChunk;
			return meshChunk;
		}

		public bool TryGetMeshChunk(ChunkKey key, out MeshChunk meshChunk)
		{
			return _meshChunks.TryGetValue(key, out meshChunk);
		}

		public void RemoveUnused(HashSet<ChunkKey> neededMeshChunks, MeshDataScheduler meshDataScheduler)
		{
			var removableMeshes = new List<ChunkKey>();
			foreach (var key in _meshChunks.Keys)
			{
				if (!neededMeshChunks.Contains(key) && ReplacementReady(key, neededMeshChunks)) 
				{
					removableMeshes.Add(key);
				}
			}

			foreach(var key in removableMeshes)
			{
				if (_meshChunks.TryGetValue(key, out var meshChunk))
				{
					if (meshChunk.Mesh != null)
					{
						var meshParent = meshChunk.Mesh.GetParent();
						if (meshParent != null) meshParent.RemoveChild(meshChunk.Mesh);
						meshChunk.Mesh.QueueFree();
					}
					if (meshChunk.State == MeshState.Generating) 
					{
						meshDataScheduler.Cancel(key);
					}
					_meshChunks.Remove(key);
				}
			}
		}

		private bool ReplacementReady(ChunkKey key, HashSet<ChunkKey> leafKeys)
		{
			var coord = key.Coord;
			for (int lod = key.Lod + 1; lod <= _worldSettings.MaxLod; lod++)
			{
				coord = new Vector3I(coord.X >> 1, coord.Y >> 1, coord.Z >> 1);
				var ancestor = new ChunkKey(coord, lod);
				if (leafKeys.Contains(ancestor))
				{
					return _meshChunks.TryGetValue(ancestor, out var c) && c.State == MeshState.Generated;
				}
			}

			foreach (var leaf in leafKeys)
			{
				if (leaf.Lod >= key.Lod) continue;
				int diff = key.Lod - leaf.Lod;
				var ancestorCoord = new Vector3I(leaf.Coord.X >> diff, leaf.Coord.Y >> diff, leaf.Coord.Z >> diff);
				if (ancestorCoord != key.Coord) continue;
				if (!_meshChunks.TryGetValue(leaf, out var c) || c.State != MeshState.Generated) return false;
			}
			return true;
		}
	}
}
