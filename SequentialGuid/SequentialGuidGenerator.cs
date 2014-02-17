using System;

namespace Buvinghausen.SequentialGuid
{
	/// <summary>
	/// 
	/// </summary>
	public sealed class SequentialGuidGenerator : ISequentialGuidGenerator
	{
		/// <summary>
		/// 
		/// </summary>
		private static readonly Lazy<SequentialGuidGenerator> Lazy = new Lazy<SequentialGuidGenerator>(() => new SequentialGuidGenerator());

		/// <summary>
		/// 
		/// </summary>
		private SequentialGuidGenerator() { }

		/// <summary>
		/// 
		/// </summary>
		public static ISequentialGuidGenerator Instance
		{
			get
			{
				return Lazy.Value;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public Guid NewGuid()
		{
			return NewGuid(DateTime.UtcNow);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		public Guid NewGuid(DateTime timestamp)
		{
			return NewGuid(timestamp.Ticks);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		public Guid NewGuid(long timestamp)
		{
			return SequentialGuid.NewGuid(timestamp);
		}
	}
}