using System;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Buvinghausen.SequentialGuid
{
	/// <summary>
	/// Generates sequential Guids based on the MongoDB ObjectId specification only uses a 16 byte value in order to be Guid compatible.
	/// The additional bytes are taken up by using a 64 bit time value rather than the 32 bit Unix epoch
	/// </summary>
	public static class SequentialGuid
	{
		private static readonly int StaticMachine;
		private static readonly short StaticPid;
		private static int _staticIncrement;

		/// <summary>
		/// Static constructor initializes the three needed variables
		/// </summary>
		static SequentialGuid()
		{
			_staticIncrement = new Random().Next();
			using (var algorithm = MD5.Create())
			{
				var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(Environment.MachineName));
				StaticMachine = (hash[0] << 16) + (hash[1] << 8) + hash[2]; // use first 3 bytes of hash
			}
			try
			{
				StaticPid = (short)Process.GetCurrentProcess().Id; // use low order two bytes only
			}
			catch (SecurityException)
			{
				StaticPid = 0;
			}
		}

		/// <summary>
		/// Returns a Guid that is sequential through time based on DateTime.UtcNow
		/// </summary>
		/// <returns>Guid</returns>
		public static Guid NewGuid()
		{
			return NewGuid(DateTime.UtcNow);
		}

		/// <summary>
		/// Returns a Guid that is sequential for a given DateTime value you can provide
		/// </summary>
		/// <param name="timestamp">Instance of DateTime you wish to provide in your Guid</param>
		/// <returns>Guid</returns>
		public static Guid NewGuid(DateTime timestamp)
		{
			return NewGuid(timestamp.Ticks);
		}

		/// <summary>
		/// Gets a sequential Guid for a given tick value based on ObjectId spec
		/// </summary>
		/// <param name="timestamp">Should be the system Ticks value you wish to provide in your Guid</param>
		/// <returns>Guid</returns>
		public static Guid NewGuid(long timestamp)
		{
			var increment = Interlocked.Increment(ref _staticIncrement) & 0x00ffffff; // only use low order 3 bytes
			return new Guid(
				(int)(timestamp >> 32),
				(short)(timestamp >> 16),
				(short)timestamp,
				(byte)(StaticMachine >> 16),
				(byte)(StaticMachine >> 8),
				(byte)(StaticMachine),
				(byte)(StaticPid >> 8),
				(byte)StaticPid,
				(byte)(increment >> 16),
				(byte)(increment >> 8),
				(byte)increment
			);
		}

		/// <summary>
		/// Get sequential SqlGuid based on DateTime.UtcNow
		/// </summary>
		/// <returns>SqlGuid</returns>
		public static SqlGuid NewSqlGuid()
		{
			return NewSqlGuid(DateTime.UtcNow);
		}

		/// <summary>
		/// Get sequential SqlGuid with time value encapsulated
		/// </summary>
		/// <param name="timestamp">Instance of DateTime structure</param>
		/// <returns>SqlGuid</returns>
		public static SqlGuid NewSqlGuid(DateTime timestamp)
		{
			return NewSqlGuid(timestamp.Ticks);
		}

		/// <summary>
		/// Return a sequential SqlGuid for a given time value in ticks
		/// </summary>
		/// <param name="timestamp">Time value in ticks</param>
		/// <returns>SqlGuid</returns>
		public static SqlGuid NewSqlGuid(long timestamp)
		{
			return NewGuid(timestamp).ToSqlGuid();
		}
	}
}