using SequentialGuid.EntityFrameworkCore;

IList<string> failures = [];

// The four public value generators are the new v6.2 surface; exercising them under ILC
// verifies our code paths trim cleanly. No DbContext — EF Core's full NativeAOT story is
// experimental and out of scope.
SequentialGuidValueGenerator guidGen = new();
var v7 = guidGen.Next(null!);
Check("Guid generator emits v7", v7 != Guid.Empty && v7.ToByteArray()[7] >> 4 == 7);
Check("Guid generator not temporary", !guidGen.GeneratesTemporaryValues);

SequentialSqlGuidValueGenerator sqlGen = new();
var sqlV7 = sqlGen.Next(null!);
Check("SQL generator emits SQL-ordered v7", sqlV7 != Guid.Empty && sqlV7.ToByteArray()[8] >> 4 == 7);

SequentialGuidStructValueGenerator structGen = new();
var sg = structGen.Next(null!);
Check("struct generator non-default", sg.Value != Guid.Empty);
Check("struct generator timestamp populated", sg.Timestamp > DateTime.MinValue);

SequentialSqlGuidStructValueGenerator sqlStructGen = new();
var ssg = sqlStructGen.Next(null!);
Check("SQL struct generator non-default", ssg.Value != Guid.Empty);

if (failures.Count == 0)
{
	Console.WriteLine("EF Core AOT smoke test: PASS");
	return 0;
}

Console.WriteLine($"EF Core AOT smoke test: FAIL ({failures.Count} failures)");
foreach (var f in failures)
	Console.WriteLine($"  - {f}");
return 1;

void Check(string name, bool condition)
{
	if (!condition)
		failures.Add(name);
}
