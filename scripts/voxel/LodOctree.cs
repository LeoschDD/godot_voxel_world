using Godot;
using System;
using System.Collections.Generic;

namespace Voxel
{ 
    public class LodOctree
    {
        private OctreeChunk _root;

        public LodOctree(WorldSettings worldSettings)
        {
            _root = new OctreeChunk(
                new ChunkKey(Vector3I.Zero, worldSettings.MaxLod),
                new Aabb(-Vector3.One * worldSettings.WorldSize * 0.5f, Vector3.One * worldSettings.WorldSize)
            );
        }

        public void Update(Func<OctreeChunk, bool> shouldSubdivide, Func<OctreeChunk, bool> shouldMerge)
        {
            _root.Update(shouldSubdivide, shouldMerge);
        }

        public List<OctreeChunk> GetLeaves(Vector3 viewPoint)
        {
            var leaves = new List<OctreeChunk>();
            _root.CollectLeaves(leaves);
            leaves.Sort(delegate(OctreeChunk x, OctreeChunk y)
			{
				float distanceX = x.Bounds.GetCenter().DistanceSquaredTo(viewPoint);
				float distanceY = y.Bounds.GetCenter().DistanceSquaredTo(viewPoint);

				return distanceX.CompareTo(distanceY);
			});
            return leaves;
        }
    }
}
