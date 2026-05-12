using Godot;
using System;
using System.Collections.Generic;

namespace Voxel
{
    public class OctreeChunk
    {
        private readonly ChunkKey _key;
        private readonly Aabb _bounds;
        private OctreeChunk[] _children;

        public ChunkKey Key => _key;
        public Aabb Bounds => _bounds;
        public bool Leaf => _children == null;

        public OctreeChunk(ChunkKey key, Aabb bounds)
        {
            _key = key;
            _bounds = bounds;
        }

        public void Update(Func<OctreeChunk, bool> shouldSubdivide, Func<OctreeChunk, bool> shouldMerge)
        {
            if (Leaf && Key.Lod > 0 && shouldSubdivide(this))
            {
                Subdivide();
                foreach (var child in _children) child.Update(shouldSubdivide, shouldMerge);
            }
            else if (!Leaf)
            {
                if (shouldMerge(this)) Merge();
                else foreach (var child in _children) child.Update(shouldSubdivide, shouldMerge);
            }
        }

        public void CollectLeaves(List<OctreeChunk> leaves)
        {
            if (Leaf) 
            {
                leaves.Add(this); 
                return;
            }
            foreach (var child in _children) 
            {
                child.CollectLeaves(leaves);
            }
        }

        private Vector3I ChildOffset(int index)
        {
            return new Vector3I(
                index & 1,
                (index >> 1) & 1,
                (index >> 2) & 1
            );
        }

        private void Subdivide()
        {
            if (_key.Lod <= 0) return;

            _children = new OctreeChunk[8];
            for (int i = 0; i < 8; i++)
            {
                var childOffset = ChildOffset(i);
				var halfSize =  _bounds.Size * 0.5f;
                
                _children[i] = new OctreeChunk(
                    new ChunkKey(_key.Coord * 2 + childOffset, _key.Lod - 1),
                    new Aabb(_bounds.Position + childOffset * halfSize, halfSize)
                );
            }
        }

        private void Merge()
        {
            _children = null;
        }
    }
}