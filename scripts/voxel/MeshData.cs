using Godot;
using System;

namespace Voxel
{
	public class MeshData
	{
		public Vector3[] Vertices {get;}
		public Vector3[] Normals {get;}
		public int[] Indices {get;}
		public static MeshData Empty => new MeshData(Array.Empty<Vector3>(), Array.Empty<Vector3>(), Array.Empty<int>());

		public MeshData(Vector3[] vertices, Vector3[] normals, int[] indices)
		{
			Vertices = vertices;
			Normals = normals;
			Indices = indices;
		}
	}
}