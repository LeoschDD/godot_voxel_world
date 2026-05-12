using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Voxel
{
	public class MeshDataScheduler
	{
		private readonly IMesher _mesher;
		private readonly object _lock = new();
		private ConcurrentQueue<(ChunkKey, MeshData)> _completed = new();
		private HashSet<ChunkKey> _cancelled = new();
		private HashSet<ChunkKey> _generating = new();

		public MeshDataScheduler(IMesher mesher)
		{
			_mesher = mesher;
		}

		public bool Schedule(ChunkKey key, float[] data, int size)
		{
			lock (_lock)
			{
				if (!_generating.Add(key)) return false;
				_cancelled.Remove(key);
			}
			WorkerThreadPool.AddTask(Callable.From(() =>
			{
				lock (_lock)
				{
					if (_cancelled.Remove(key)) 
					{
						_generating.Remove(key);
						return;
					}                
				}
				var meshData = _mesher.BuildMesh(data, size);
				_completed.Enqueue((key, meshData));
				
				lock (_lock)
				{
					_generating.Remove(key);
				}
			}));
			return true;
		}

		public void Cancel(ChunkKey key)
		{
			lock (_lock)
			{
				if (!_generating.Contains(key)) return;
				_cancelled.Add(key);            
			}
		}

		public bool TryGet(out (ChunkKey, MeshData) result)
		{
			while (_completed.TryDequeue(out result)) 
			{
				lock (_lock)
				{
					if (_cancelled.Remove(result.Item1)) continue;              
				}
				return true;
			}
			return false;
		}
	}
}
