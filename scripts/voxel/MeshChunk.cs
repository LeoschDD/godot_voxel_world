using Godot;
using System;

namespace Voxel
{
	public enum MeshState
	{
		Generating,
		Generated,
		Dirty
	}

	public class MeshChunk
	{
		public ChunkKey Key {get;}
		public Aabb Bounds {get;}
		public MeshState State {get; set;}
		public MeshInstance3D Mesh {get; set;}
		public StaticBody3D CollisionBody {get; set;}
		public CollisionShape3D CollisionShape{get; set;}

		public MeshChunk(ChunkKey key, Aabb bounds)
		{
			Key = key;
			Bounds = bounds;
			State = MeshState.Dirty;
		}
	}
}
