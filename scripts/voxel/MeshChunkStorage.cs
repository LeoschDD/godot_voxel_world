using Godot;
using System;
using System.Collections.Generic;

namespace Voxel
{
	public class MeshChunkStorage
	{
		private Dictionary<ChunkKey, MeshChunk> _meshChunks = new();
		private MeshDataScheduler _meshDataScheduler;
		private WorldSettings _worldSettings;

		public MeshChunkStorage(WorldSettings worldSettings, IMesher mesher)
		{
			_meshDataScheduler = new MeshDataScheduler(mesher);
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

		public void SyncMeshChunk(DataChunk dataChunk)
		{
			if (!_meshChunks.TryGetValue(dataChunk.Key, out var meshChunk)) return;

			if (!dataChunk.HasSurface)
			{
				if (meshChunk.State == MeshState.Generating) _meshDataScheduler.Cancel(meshChunk.Key);
				if (meshChunk.Mesh != null) meshChunk.Mesh.Mesh = null;
				if (meshChunk.CollisionShape != null) meshChunk.CollisionShape.Shape = null;
				meshChunk.State = MeshState.Generated;
				return;
			}

			if (meshChunk.State != MeshState.Dirty) return;
			if (_meshDataScheduler.Schedule(dataChunk.Key, (float[])dataChunk.Data.Clone(), dataChunk.Size))
			{
				meshChunk.State = MeshState.Generating;
			}
		}

		public void CollectScheduled(Node meshParent, Material meshMaterial, HashSet<ChunkKey> neededMeshChunks)
		{
			for (int i = 0; i < 16; i++)
			{
				if (!_meshDataScheduler.TryGet(out var result)) break;
				if (!neededMeshChunks.Contains(result.Item1)) continue;
				CreateMesh(result.Item1, result.Item2, meshParent, meshMaterial);
			}
		}

		public void RemoveUnused(HashSet<ChunkKey> neededMeshChunks)
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
						_meshDataScheduler.Cancel(key);
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


		private void CreateMesh(ChunkKey key, MeshData meshData, Node meshParent, Material meshMaterial)
		{
			if (!_meshChunks.TryGetValue(key, out var meshChunk)) return;

			if (meshData.Vertices.Length < 3 || meshData.Indices.Length < 3)
			{
				if (meshChunk.Mesh != null)
				{
					meshChunk.Mesh.Mesh = null;
				}
				if (meshChunk.CollisionShape != null)
				{
					meshChunk.CollisionShape.Shape = null;
				}
				meshChunk.State = MeshState.Generated;
				return;
			}

			var arrayMesh = new ArrayMesh();
			var arrays = new Godot.Collections.Array();

			arrays.Resize((int)ArrayMesh.ArrayType.Max);
			arrays[(int)ArrayMesh.ArrayType.Vertex] = meshData.Vertices;
			arrays[(int)ArrayMesh.ArrayType.Normal] = meshData.Normals;
			arrays[(int)ArrayMesh.ArrayType.Index] = meshData.Indices;
			arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

			if (meshChunk.Mesh == null)
			{
				meshChunk.Mesh = new MeshInstance3D
				{
					Scale = Vector3.One * (1 << meshChunk.Key.Lod),
					Position = meshChunk.Bounds.Position
				};						
			}
			
			if (meshChunk.CollisionBody == null)
			{
				meshChunk.CollisionBody = new StaticBody3D();
				meshChunk.CollisionShape = new CollisionShape3D();

				meshChunk.CollisionBody.AddChild(meshChunk.CollisionShape);
				meshChunk.Mesh.AddChild(meshChunk.CollisionBody);				
			}

			meshChunk.Mesh.Mesh = arrayMesh;
			meshChunk.CollisionShape.Shape = arrayMesh.CreateTrimeshShape();

			if (meshMaterial != null) meshChunk.Mesh.SetSurfaceOverrideMaterial(0, meshMaterial);

			meshChunk.State = MeshState.Generated;
			if (meshChunk.Mesh.GetParent() != meshParent) meshParent.AddChild(meshChunk.Mesh);
		}
	}
}
