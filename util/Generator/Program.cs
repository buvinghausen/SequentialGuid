using SequentialGuid;

/*
var min = DateTimeOffset.UnixEpoch.Ticks;
var max = DateTimeOffset.UtcNow.Ticks;

for (var i = 0; i < 2000; i++)
{
	var ticks = Random.Shared.NextInt64(min, max);
	Console.WriteLine($"[InlineData(\"{GuidV8Time.NewGuid(new(ticks, DateTimeKind.Utc))}\",{ticks})]");
}
*/

var max = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

for (var i = 0; i < 2000; i++)
{
	var unixMs = Random.Shared.NextInt64(0, max);
	Console.WriteLine($"[InlineData(\"{GuidV7.NewGuid(DateTimeOffset.FromUnixTimeMilliseconds(unixMs))}\",{unixMs})]");
}

Console.WriteLine("Press any key to continue...");
Console.ReadKey();
