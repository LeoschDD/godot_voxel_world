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

        public List<OctreeChunk> GetLeaves()
        {
            var leaves = new List<OctreeChunk>();
            _root.CollectLeaves(leaves);
            return leaves;
        }
    }
}
