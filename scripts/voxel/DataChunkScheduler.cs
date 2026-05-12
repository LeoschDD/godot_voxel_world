using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Voxel
{
	public class DataChunkScheduler
	{
		private readonly object _lock = new();
		private ConcurrentQueue<DataChunk> _completed = new();
		private HashSet<ChunkKey> _cancelled = new();
		private HashSet<ChunkKey> _generating = new();

		public bool Schedule(ChunkKey key, Func<DataChunk> createDataChunk)
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
				var data = createDataChunk();
				_completed.Enqueue(data);

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

		public bool TryGet(out DataChunk dataChunk)
		{
			while (_completed.TryDequeue(out dataChunk)) 
			{
				lock (_lock)
				{
					if (_cancelled.Remove(dataChunk.Key)) continue;              
				}
				return true;
			}
			return false;
		}
	}
}
