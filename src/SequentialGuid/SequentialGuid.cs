using System;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace SequentialGuid
{
	/// <summary>
	/// Generates sequential Guids based on the MongoDB ObjectId specification only uses a 16 byte value in order to be Guid compatible.
	/// The additional bytes are taken up by using a 64 bit time value rather than the 32 bit Unix epoch
	/// </summary>
	internal static class SequentialGuid
	{
		internal static readonly DateTime UnixEpoch =
			new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		private static readonly byte[] StaticMachinePid;
		private static int _staticIncrement;

		/// <summary>
		/// Static constructor initializes the three needed variables
		/// </summary>
		static SequentialGuid()
		{
			_staticIncrement = new Random().Next();
			StaticMachinePid = new byte[5];
			using (var algorithm = MD5.Create())
			{
				var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(Environment.MachineName));
				// use first 3 bytes of hash
				for (var i = 0; i < 3; i++) StaticMachinePid[i] = hash[i];
			}
			try
			{
				var pid = Process.GetCurrentProcess().Id;
				// use low order two bytes only
				StaticMachinePid[3] = (byte)(pid >> 8);
				StaticMachinePid[4] = (byte)pid;
			}
			catch (SecurityException) { }
		}

		/// <summary>
		/// Gets a sequential Guid for a given tick value based on ObjectId spec
		/// </summary>
		/// <param name="timestamp">Should be the system Ticks value you wish to provide in your Guid</param>
		/// <returns>Guid</returns>
		internal static Guid NewGuid(long timestamp)
		{
			var increment = Interlocked.Increment(ref _staticIncrement) & 0x00ffffff; // only use low order 3 bytes
			return new Guid(
				(int)(timestamp >> 32),
				(short)(timestamp >> 16),
				(short)timestamp,
				StaticMachinePid.Concat(
				new[] {
					(byte)(increment >> 16),
					(byte)(increment >> 8),
					(byte)increment
				}).ToArray()
			);
		}
	}
}
