using Godot;
using System;
using System.Collections.Generic;

namespace Voxel
{
	public class Generator
	{
		private List<IModifier> _modifiers = new();

		public float Sample(Vector3 point)
		{
			var density = float.MaxValue;
			foreach (var modifier in _modifiers)
			{
				if (modifier.Bounds().HasValue && !modifier.Bounds().Value.HasPoint(point)) continue;
				if (modifier.Type() == ModifierType.Add) density = Mathf.Min(density, modifier.Sample(point));
				if (modifier.Type() == ModifierType.Subtract) density = Mathf.Max(density, -modifier.Sample(point));
			}
			return density;
		}

		public void AddModifier(IModifier modifier)
		{
			_modifiers.Add(modifier);
		}
	}
}
