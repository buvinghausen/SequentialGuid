using System;

namespace SequentialGuid
{
	/// <summary>
	/// 
	/// </summary>
	public interface ISequentialGuidGenerator
	{
		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		Guid NewGuid();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		Guid NewGuid(DateTime timestamp);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		Guid NewGuid(long timestamp);
	}
}
