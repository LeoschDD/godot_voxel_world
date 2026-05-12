using Godot;
using System;

namespace Voxel
{
	public class DataChunk
	{
		public ChunkKey Key {get;}
		public float[] Data {get;}
		public int Size {get;}
		public bool Edited {get; set;}
		public float Min {get; set;}
		public float Max {get; set;}
		public bool HasSurface => Min <= 0.0f && Max >= 0.0f;

		public DataChunk(ChunkKey key, WorldSettings worldSettings)
		{
			Key = key;
			Size = worldSettings.ChunkSize + 1;
			Data = new float[Size * Size * Size];
			Edited = false;
			Min = float.MaxValue;
			Max = float.MinValue;
		}

		public ref float Get(Vector3I coord)
		{
			return ref Data[coord.X * Size * Size + coord.Y * Size + coord.Z];
		}
	}
}
