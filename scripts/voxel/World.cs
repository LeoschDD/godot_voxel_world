using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;

namespace Voxel
{
	[Tool]
	public partial class World : Node3D
	{
		[Export] Camera3D _camera;
		[Export] Material _material;
		private WorldSettings _worldSettings = new();
		private LodOctree _lodOctree;
		private MeshChunkStorage _meshChunkStorage;
		private DataChunkStorage _dataChunkStorage;
		private Editor _editor;
		private IMesher _mesher = new MarchingCubesMesher();
		private MeshDataScheduler _meshDataScheduler;
		private Generator _generator = new();

		public override void _Ready()
		{
			Init();
		}

		public override void _Process(double delta)
		{
			if (_camera == null) return;
			if (_lodOctree == null || _meshChunkStorage == null || 
				_dataChunkStorage == null || _editor == null || _meshDataScheduler == null) Init();
			
			_dataChunkStorage.CollectDataChunks(_meshChunkStorage, _editor.ApplyPendingEdits);

			var viewPoint = ToLocal(_camera.GlobalPosition);
			_lodOctree.Update((OctreeChunk chunk) => ShouldSubdivide(chunk, viewPoint), (OctreeChunk chunk) => ShouldMerge(chunk, viewPoint));

			var neededMeshChunks = new HashSet<ChunkKey>();
			var neededDataChunks = new HashSet<ChunkKey>();

			foreach (var leaf in _lodOctree.GetLeaves())
			{
				neededMeshChunks.Add(leaf.Key);

				var meshChunk = _meshChunkStorage.EnsureMeshChunk(leaf);
				var hasDataChunk = _dataChunkStorage.TryGetDataChunk(leaf.Key, out var dataChunk);
				var hasSurface = hasDataChunk && dataChunk.HasSurface;

				if (!hasDataChunk) _dataChunkStorage.TrySchedule(leaf.Key, _generator);

				if (hasDataChunk && !hasSurface)
				{
					if (meshChunk.State == MeshState.Generating)
					{
						_meshDataScheduler.Cancel(meshChunk.Key);
					}
					if (meshChunk.Mesh != null)
					{
						meshChunk.Mesh.Mesh = null;
					}
					if (meshChunk.CollisionShape != null)
					{
						meshChunk.CollisionShape.Shape = null;
					}
					meshChunk.State = MeshState.Generated;
				}
				else if (hasSurface && meshChunk.State == MeshState.Dirty)
				{
					if (_meshDataScheduler.Schedule(dataChunk.Key, (float[])dataChunk.Data.Clone(), dataChunk.Size))
					{
						meshChunk.State = MeshState.Generating;
					}
				}

				if (meshChunk.Mesh != null && meshChunk.State == MeshState.Generated && meshChunk.Mesh.GetParent() != this)
				{
					AddChild(meshChunk.Mesh);
				}
			}

			for (int i = 0; i < 8; i++) 
			{
				if (!_meshDataScheduler.TryGet(out var result)) break;

				var key = result.Item1;
				var chunkData = result.Item2;

				if (!neededMeshChunks.Contains(key)) continue;

				CreateMesh(key, chunkData);
			}

			_meshChunkStorage.RemoveUnused(neededMeshChunks, _meshDataScheduler);
			_dataChunkStorage.RemoveUnused(neededDataChunks, _editor.PendingEdits);

			Vector3 from = _camera.GlobalPosition + (_camera.GlobalBasis * Vector3.Forward).Normalized() * 2.0f;
			Vector3 to = from + (_camera.GlobalBasis * Vector3.Forward).Normalized() * 100.0f;

			var query = PhysicsRayQueryParameters3D.Create(from, to);
			var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);

			if (hit.Count > 0)
			{
				Vector3 hitPosition = (Vector3)hit["position"];

				if (Input.IsActionJustPressed("add")) 
				{
					_editor.Edit(new SphereModifier(ModifierType.Add, ToLocal(hitPosition), 8.0f), _dataChunkStorage, _meshChunkStorage, _generator);
				}
				if (Input.IsActionJustPressed("subtract"))
				{
					_editor.Edit(new SphereModifier(ModifierType.Subtract, ToLocal(hitPosition), 8.0f), _dataChunkStorage, _meshChunkStorage, _generator);
				} 
			}
		}

		private void CreateMesh(ChunkKey key, MeshData meshData)
		{
			if (!_meshChunkStorage.TryGetMeshChunk(key, out var meshChunk)) return;

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

			if (meshChunk.CollisionBody == null || meshChunk.CollisionShape == null)
			{
				meshChunk.CollisionBody = new StaticBody3D();
				meshChunk.CollisionShape = new CollisionShape3D();

				meshChunk.CollisionBody.AddChild(meshChunk.CollisionShape);
				meshChunk.Mesh.AddChild(meshChunk.CollisionBody);
			}

			meshChunk.Mesh.Mesh = arrayMesh;
			meshChunk.CollisionShape.Shape = arrayMesh.CreateTrimeshShape();

			if (_material != null) meshChunk.Mesh.SetSurfaceOverrideMaterial(0, _material);

			meshChunk.State = MeshState.Generated;
			if (meshChunk.Mesh.GetParent() != this)
			{
				AddChild(meshChunk.Mesh);
			}
		}

		private bool ShouldSubdivide(OctreeChunk chunk, Vector3 viewPoint)
		{
			if (chunk.Bounds.GetCenter().DistanceTo(viewPoint) > chunk.Bounds.Size.Length()) return false;	
			if (!_dataChunkStorage.TryGetDataChunk(chunk.Key, out var dataChunk))
			{
				_dataChunkStorage.TrySchedule(chunk.Key, _generator);
				return false;
			}
			bool childrenReady = true;
			for (int i = 0; i < 8; i++)
			{
				var offset = new Vector3I(
					i & 1,
					(i >> 1) & 1,
					(i >> 2) & 1
				);
				var key = new ChunkKey(chunk.Key.Coord * 2 + offset, chunk.Key.Lod - 1);

				if (!_dataChunkStorage.ContainsDataChunk(key))
				{
					_dataChunkStorage.TrySchedule(key, _generator);
					childrenReady = false;
				}
			}
			return dataChunk.HasSurface && childrenReady;
		}

		private bool ShouldMerge(OctreeChunk chunk, Vector3 viewPoint)
		{
			return chunk.Bounds.GetCenter().DistanceTo(viewPoint) > chunk.Bounds.Size.Length() * 1.5f;	
		}

		private void Init()
		{
			_lodOctree = new LodOctree(_worldSettings);
			_meshChunkStorage = new MeshChunkStorage(_worldSettings);
			_dataChunkStorage = new DataChunkStorage(_worldSettings);
			_editor = new Editor(_worldSettings);
			_meshDataScheduler = new MeshDataScheduler(_mesher);
		}
	}
}
