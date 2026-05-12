using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using SequentialGuid;
using SequentialGuid.Extensions;
// Disambiguate the SequentialGuid type from the SequentialGuid namespace.
using SgStruct = SequentialGuid.SequentialGuid;
using SsgStruct = SequentialGuid.SequentialSqlGuid;

var failures = new List<string>();

void Check(string name, bool condition)
{
	if (!condition) failures.Add(name);
}

// GuidV4
var v4 = GuidV4.NewGuid();
Check("v4 non-default", v4 != Guid.Empty);

// GuidV5
var v5 = GuidV5.Create(GuidV5.Namespaces.Url, "https://example.com");
Check("v5 deterministic",
	v5 == GuidV5.Create(GuidV5.Namespaces.Url, "https://example.com"));

// GuidV7
var v7 = GuidV7.NewGuid();
Check("v7 non-default", v7 != Guid.Empty);
Check("v7 ToDateTime non-null", v7.ToDateTime() is not null);
Check("v7 NewSqlGuid roundtrip", GuidV7.NewSqlGuid().FromSqlGuid().ToDateTime() is not null);

var v7Ts = GuidV7.NewGuid(DateTimeOffset.UtcNow);
Check("v7 from DateTimeOffset", v7Ts != Guid.Empty);

var v7Ms = GuidV7.NewGuid(1_000_000L);
var expectedV7Ms = DateTimeOffset.FromUnixTimeMilliseconds(1_000_000L).UtcDateTime;
Check("v7 from unix ms roundtrips", v7Ms.ToDateTime() == expectedV7Ms);

// GuidV8Time
var v8 = GuidV8Time.NewGuid();
Check("v8 non-default", v8 != Guid.Empty);
Check("v8 ToDateTime non-null", v8.ToDateTime() is not null);
Check("v8 NewSqlGuid roundtrip", GuidV8Time.NewSqlGuid().FromSqlGuid().ToDateTime() is not null);

// GuidV8Name
var v8n = GuidV8Name.Create(GuidV8Name.Namespaces.Url, "https://example.com");
Check("v8n deterministic",
	v8n == GuidV8Name.Create(GuidV8Name.Namespaces.Url, "https://example.com"));

// SequentialGuid struct
var sg = new SgStruct();
Check("SequentialGuid ctor", sg.Value != Guid.Empty);
Check("SequentialGuid timestamp populated", sg.Timestamp > DateTime.MinValue);

var sg2 = new SgStruct(v7);
Check("SequentialGuid wraps v7", sg2.Value == v7);

var sg3 = new SgStruct(v7.ToString());
Check("SequentialGuid wraps string", sg3.Value == v7);

// SequentialSqlGuid struct
var ssg = new SsgStruct();
Check("SequentialSqlGuid ctor", ssg.Value != Guid.Empty);

var ssg2 = new SsgStruct(v7);
Check("SequentialSqlGuid wraps v7", ssg2.Value == v7.ToSqlGuid());

// JSON converters — use a source-generated JsonSerializerContext so the round-trip is
// trimmer- and AOT-safe. AddSequentialGuidConverters() registers the SequentialGuid converters
// on top of the generated context's options.
var opts = new JsonSerializerOptions(SmokeJsonContext.Default.Options);
opts.AddSequentialGuidConverters();
var typeInfo = (JsonTypeInfo<SgStruct>)opts.GetTypeInfo(typeof(SgStruct));
var json = JsonSerializer.Serialize(sg, typeInfo);
var roundTripped = JsonSerializer.Deserialize(json, typeInfo);
Check("JSON roundtrip preserves Value", roundTripped.Value == sg.Value);

if (failures.Count == 0)
{
	Console.WriteLine("AOT smoke test: PASS");
	return 0;
}

Console.WriteLine($"AOT smoke test: FAIL ({failures.Count} failures)");
foreach (var f in failures) Console.WriteLine($"  - {f}");
return 1;

[JsonSerializable(typeof(SgStruct))]
internal sealed partial class SmokeJsonContext : JsonSerializerContext;
