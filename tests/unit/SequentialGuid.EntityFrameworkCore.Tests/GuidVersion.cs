namespace SequentialGuid.EntityFrameworkCore.Tests;

// .NET ToByteArray() is mixed-endian: the RFC version nibble lands in byte[7]
// for standard byte order and byte[8] for SQL Server byte order.
static class GuidVersion
{
	internal static int Standard(Guid id) =>
		id.ToByteArray()[7] >> 4;

	internal static int Sql(Guid id) =>
		id.ToByteArray()[8] >> 4;
}
