using SequentialGuid;

const long ticks = 639084490271870091L;

for (var i = 0; i < 100000; i++)
{
	Console.WriteLine($"\"{SequentialGuidGenerator.Instance.NewGuid(new(ticks + 100 * i, DateTimeKind.Utc))}\",");
}

Console.WriteLine("Press any key to continue...");
Console.ReadKey();
