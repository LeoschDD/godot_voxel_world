using Godot;
using System;

namespace Voxel
{
    public readonly struct ChunkKey : IEquatable<ChunkKey>
    {
        public Vector3I Coord {get;}
        public int Lod {get;}

        public ChunkKey(Vector3I coord, int lod)
        {
            Coord = coord;
            Lod = lod;
        }

        public bool Equals(ChunkKey other)
        {
            return Coord == other.Coord && Lod == other.Lod;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Coord, Lod);
        }

        public override string ToString()
        {
            return $"{Coord.X}_{Coord.Y}_{Coord.Z}_{Lod}";
        }
    };
}
