using Godot;
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
		private Generator _generator = new();
		private LodOctree _lodOctree;
		private MeshChunkStorage _meshChunkStorage;
		private DataChunkStorage _dataChunkStorage;
		private Editor _editor;

		public override void _Ready()
		{
			Init();
		}

		public override void _Process(double delta)
		{
			if (_camera == null) return;
			if (_lodOctree == null || _meshChunkStorage == null || _dataChunkStorage == null || _editor == null) Init();
			
			_dataChunkStorage.CollectScheduled(_meshChunkStorage, _editor.ApplyPendingEdits);
			_meshChunkStorage.CollectScheduled(this, _material);

			var neededDataChunks = new HashSet<ChunkKey>();
			var neededMeshChunks = new HashSet<ChunkKey>();

			var viewPoint = ToLocal(_camera.GlobalPosition);
			_lodOctree.Update(
				(OctreeChunk chunk) => LodShouldSubdivide(chunk, viewPoint, neededDataChunks),
				(OctreeChunk chunk) => LodShouldMerge(chunk, viewPoint));

			foreach (var leaf in _lodOctree.GetLeaves(viewPoint))
			{
				neededMeshChunks.Add(leaf.Key);
				neededDataChunks.Add(leaf.Key);

				var hasDataChunk = _dataChunkStorage.TryGetDataChunk(leaf.Key, out var dataChunk);
				var meshChunk = _meshChunkStorage.EnsureMeshChunk(leaf);

				if (!hasDataChunk) _dataChunkStorage.TrySchedule(leaf.Key, _generator);
				if (hasDataChunk && dataChunk.HasSurface && meshChunk.State == MeshState.Dirty) _meshChunkStorage.TrySchedule(dataChunk);
			}

			_meshChunkStorage.RemoveUnused(neededMeshChunks);
			_dataChunkStorage.RemoveUnused(neededDataChunks, _editor.PendingEdits);

			ProcessEdits();
		}

		private void Init()
		{
			_lodOctree = new LodOctree(_worldSettings);
			_meshChunkStorage = new MeshChunkStorage(_worldSettings, new MarchingCubesMesher());
			_dataChunkStorage = new DataChunkStorage(_worldSettings);
			_editor = new Editor(_worldSettings);

			_generator.AddModifier(new NoiseModifier(ModifierType.Add, new SphereModifier(ModifierType.Add, new Vector3(0.0f, -100000.0f, 0.0f), 100000), 1, 0.0007f, 80.0f, 5));
		}

		private void ProcessEdits()
		{
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

		private bool LodShouldSubdivide(OctreeChunk chunk, Vector3 viewPoint, HashSet<ChunkKey> neededDataChunks)
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
				var key = new ChunkKey(chunk.Key.Coord * 2 + chunk.ChildOffset(i), chunk.Key.Lod - 1);

				neededDataChunks.Add(key);
				if (!_dataChunkStorage.ContainsDataChunk(key))
				{
					_dataChunkStorage.TrySchedule(key, _generator);
					childrenReady = false;
				}
			}
			return dataChunk.HasSurface && childrenReady;
		}

		private bool LodShouldMerge(OctreeChunk chunk, Vector3 viewPoint)
		{
			return chunk.Bounds.GetCenter().DistanceTo(viewPoint) > chunk.Bounds.Size.Length() * 1.5f;	
		}
	}
}
