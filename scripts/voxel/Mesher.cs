using Godot;
using System;

namespace Voxel
{
	public interface IMesher
	{
		public MeshData BuildMesh(float[] data, int size);
	}
}