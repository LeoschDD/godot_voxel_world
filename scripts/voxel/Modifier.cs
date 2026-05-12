using Godot;
using System;

namespace Voxel
{
	public enum ModifierType
	{
		Add,
		Subtract
	}

	public interface IModifier
	{
		public float Sample(Vector3 point);
		public ModifierType Type();
		public Aabb? Bounds();
	}

	public class SphereModifier : IModifier
	{
		private readonly ModifierType _type;
		private readonly Vector3 _center;
		private readonly float _radius;

		public SphereModifier(ModifierType type, Vector3 center, float radius)
		{
			_type = type;
			_center = center;
			_radius = radius;
		}

		public float Sample(Vector3 point)
		{
			return point.DistanceTo(_center) - _radius;
		}

		public ModifierType Type()
		{
			return _type;
		}

		public Aabb? Bounds()
		{
			var position = _center - Vector3.One * _radius;
			var size = Vector3.One * _radius * 2.0f;
			return new Aabb(position, size);
		}

		
	}
	public class PlaneModifier : IModifier
	{
		private readonly ModifierType _type;
		private readonly float _height;

		public PlaneModifier(ModifierType type, float height)
		{
			_type = type;
			_height = height;
		}

		public float Sample(Vector3 point)
		{
			return point.Y - _height;
		}

		public ModifierType Type()
		{
			return _type;
		}

		public Aabb? Bounds()
		{
			return null;
		}
	}

	public class NoiseModifier : IModifier
	{
		private readonly ModifierType _type;
		private IModifier _baseModifier;
		private FastNoiseLite _noise;
		private float _amplitude;
		private int _octaves;

		public NoiseModifier(ModifierType type, IModifier baseModifier, int seed, float frequency, float amplitude, int octaves, bool smooth = false)
		{
			_type = type;
			_baseModifier = baseModifier;
			_noise = new FastNoiseLite();
			_noise.NoiseType = smooth ? FastNoiseLite.NoiseTypeEnum.SimplexSmooth : FastNoiseLite.NoiseTypeEnum.Simplex;
			_noise.Seed = seed;
			_noise.Frequency = frequency;
			_amplitude = amplitude;
			_octaves = octaves;
		}

		public float Sample(Vector3 point)
		{
			var density = _baseModifier.Sample(point);
			for (int i = 0; i < _octaves; i++)
			{
				float noise = _noise.GetNoise3Dv(point * (1 << i));
				density += noise * (_amplitude / (1 << i));	
			}
			return density;
		}

		public ModifierType Type()
		{
			return _type;
		}

		public Aabb? Bounds()
		{
			return null;
		}
	}
}
