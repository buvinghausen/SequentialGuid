using SequentialGuid;

const long ticks = 639084490271870091L;

for (var i = 0; i < 100; i++)
{
	Console.WriteLine($"(new Guid(\"{SequentialSqlGuidGenerator.Instance.NewGuid(new(ticks + 100 * i, DateTimeKind.Utc))}\"),1),");
}

Console.WriteLine("Press any key to continue...");
Console.ReadKey();
