using Godot;
using System;

namespace Voxel
{
	public class WorldSettings
	{
		public int MaxLod {get; set;}
		public int ChunkSize {get; set;}
		public int WorldSize  
		{
			get {return (1 << MaxLod) * ChunkSize;}
			set {MaxLod = Mathf.CeilToInt(Mathf.Log(value) / Mathf.Log(2));}
		}

		public WorldSettings()
		{
			WorldSize = 10000;
			ChunkSize = 16;
		}
	}
}
