# SequentialGuid vNext Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a major version of SequentialGuid with zero-allocation generation paths, removal of obsolete API + consolidated struct constructors, and verified Native AOT compatibility.

**Architecture:** Three sequential PRs against an integration branch `vNext`. PR1 is internal-only perf. PR2 is the breaking-change PR (deletes obsolete generators + public `Timestamp` static properties; consolidates struct ctors). PR3 wires AOT flags and adds a CI smoke test. A final PR merges `vNext` → `master` for the major release.

**Tech Stack:** .NET 10 / 9 / 8 + .NET Framework 4.6.2 + .NET Standard 2.0 (core); xUnit v3 + Shouldly (tests); BenchmarkDotNet (benchmarks); GitHub Actions on windows-latest (CI).

---

## Prep: branch setup

### Task 0: Create `vNext` integration branch and wire CI

**Files:**
- Modify: `.github/workflows/ci.yml`

- [ ] **Step 1: Create the integration branch**

```bash
git checkout -b vNext master
git push -u origin vNext
git checkout master
```

- [ ] **Step 2: Modify CI to trigger on PRs to vNext**

Open `.github/workflows/ci.yml` and change the `pull_request.branches` list to include `vNext`:

```yaml
on:
  pull_request:
    branches: [master, vNext]
```

- [ ] **Step 3: Commit and push**

```bash
git checkout -b chore/ci-vnext
git add .github/workflows/ci.yml
git commit -m "ci: run on PRs to vNext"
git push -u origin chore/ci-vnext
gh pr create --base master --title "ci: run on PRs to vNext" --body "Adds vNext to the CI pull_request branch list so the three vNext PRs get checks."
```

- [ ] **Step 4: Merge the CI prep PR**

After CI green: `gh pr merge --merge --delete-branch` (or merge in the web UI).

```bash
git checkout master && git pull
git checkout vNext && git merge master --no-edit && git push
```

---

## PR1 — Perf: zero-allocation generation paths

**Branch from `vNext`:** `git checkout -b vNext/perf vNext`

### Task 1.1: Add `Span<byte>` overloads of `SetRfc9562Version` / `SetRfc9562Variant`

**Files:**
- Modify: `src/SequentialGuid/Extensions/ByteArrayExtensions.cs`

**Context:** Currently both setters live only on the `extension(byte[] b)` block. Generators on NET6+ want `stackalloc byte[16]` (a `Span<byte>`), which can't call those. Add equivalent setters on the `extension(Span<byte> b)` block inside the existing `#if NET6_0_OR_GREATER` region. The `ReadOnlySpan<byte>` block stays as-is — it only has read-only helpers.

- [ ] **Step 1: Open `src/SequentialGuid/Extensions/ByteArrayExtensions.cs` and locate the existing `extension(ReadOnlySpan<byte> b)` block at line 105**

- [ ] **Step 2: Add a new `extension(Span<byte> b)` block immediately after the `ReadOnlySpan<byte>` block, still inside `#if NET6_0_OR_GREATER`**

Insert before the `#endif` at line 212:

```csharp
	// Mutable Span helpers for the generation hot path
	extension(Span<byte> b)
	{
		// Sets the RFC 9562 version nibble (bits 48-51) in bytes[6]
		internal void SetRfc9562Version(byte version) =>
			b[6] = (byte)((b[6] & 0x0F) | (version << 4));

		// Sets the RFC 9562 variant bits (10xxxxxx) on bytes[8]
		internal void SetRfc9562Variant() =>
			b[8] = (byte)((b[8] & 0x3F) | 0x80);
	}
```

- [ ] **Step 3: Build and verify**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED on all five TFMs (net10.0, net9.0, net8.0, net462, netstandard2.0). No warnings (`TreatWarningsAsErrors=true`).

- [ ] **Step 4: Commit**

```bash
git add src/SequentialGuid/Extensions/ByteArrayExtensions.cs
git commit -m "perf: add Span<byte> overloads of SetRfc9562 helpers"
```

---

### Task 1.2: Convert `GuidV4.NewGuid` to `stackalloc` on NET6+

**Files:**
- Modify: `src/SequentialGuid/GuidV4.cs`
- Test: `test/SequentialGuid.Tests/GuidV4Tests.cs` (already exists — no new tests needed; the existing suite is the regression guard)

- [ ] **Step 1: Run the existing GuidV4 tests as a baseline**

```powershell
dotnet test test/SequentialGuid.Tests --filter "FullyQualifiedName~GuidV4" --framework net10.0
```

Expected: all green.

- [ ] **Step 2: Replace the body of `GuidV4.NewGuid`**

Open `src/SequentialGuid/GuidV4.cs`. Replace the entire `NewGuid()` method (lines 21–42) with:

```csharp
	public static Guid NewGuid()
	{
		// Build 16 bytes in network (big-endian) byte order per RFC 9562 Section 5.4
#if NET6_0_OR_GREATER
		Span<byte> bytes = stackalloc byte[16];
		RandomNumberGenerator.Fill(bytes);
		bytes.SetRfc9562Version(4);
		bytes.SetRfc9562Variant();
		return new(bytes, bigEndian: true);
#else
		var bytes = new byte[16];
		using var rng = RandomNumberGenerator.Create();
		rng.GetBytes(bytes);
		bytes.SetRfc9562Version(4);
		bytes.SetRfc9562Variant();
		return new(bytes.SwapByteOrder());
#endif
	}
```

- [ ] **Step 3: Build all TFMs**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED on all five TFMs.

- [ ] **Step 4: Run the full test suite**

```powershell
dotnet test
```

Expected: all green.

- [ ] **Step 5: Commit**

```bash
git add src/SequentialGuid/GuidV4.cs
git commit -m "perf: GuidV4.NewGuid uses stackalloc on NET6+"
```

---

### Task 1.3: Convert `GuidV7.NewGuid(long)` to `stackalloc` on NET6+

**Files:**
- Modify: `src/SequentialGuid/GuidV7.cs:136-182`

- [ ] **Step 1: Open `src/SequentialGuid/GuidV7.cs` and replace the body of `NewGuid(long unixMilliseconds)`**

Replace lines 136–182 (the whole method body, keeping the XML doc comment above):

```csharp
	public static Guid NewGuid(long unixMilliseconds)
	{
		if (unixMilliseconds is < 0 or > 0x0000_FFFF_FFFF_FFFF)
			throw new ArgumentOutOfRangeException(nameof(unixMilliseconds),
				"Unix millisecond timestamp must be non-negative and fit within 48 bits.");

		// RFC 9562 §6.2 Method 1: claim a unique slot in the monotonic counter.
		var counter = Interlocked.Increment(ref s_counter) & 0x3FFFFFF;

#if NET6_0_OR_GREATER
		Span<byte> bytes = stackalloc byte[16];
		RandomNumberGenerator.Fill(bytes[10..]);
#else
		var bytes = new byte[16];
		using var rng = RandomNumberGenerator.Create();
		rng.GetBytes(bytes, 10, 6);
#endif
		// unix_ts_ms: 48-bit big-endian millisecond timestamp (octets 0-5)
		bytes[0] = (byte)(unixMilliseconds >> 40);
		bytes[1] = (byte)(unixMilliseconds >> 32);
		bytes[2] = (byte)(unixMilliseconds >> 24);
		bytes[3] = (byte)(unixMilliseconds >> 16);
		bytes[4] = (byte)(unixMilliseconds >> 8);
		bytes[5] = (byte)unixMilliseconds;

		// rand_a: upper 12 bits of 26-bit counter (octets 6-7)
		bytes[6] = (byte)(counter >> 22);
		bytes[7] = (byte)((counter >> 14) & 0xFF);

		// rand_b extension: lower 14 bits of counter (octets 8-9)
		bytes[8] = (byte)((counter >> 8) & 0x3F);
		bytes[9] = (byte)(counter & 0xFF);

		bytes.SetRfc9562Version(7);
		bytes.SetRfc9562Variant();

#if NET6_0_OR_GREATER
		return new(bytes, bigEndian: true);
#else
		return new(bytes.SwapByteOrder());
#endif
	}
```

- [ ] **Step 2: Build all TFMs**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED. No warnings.

- [ ] **Step 3: Run the GuidV7 test suite**

```powershell
dotnet test test/SequentialGuid.Tests --filter "FullyQualifiedName~GuidV7"
```

Expected: all green. Critically, the RFC 9562 §A.6 test vector tests and the monotonic-ordering tests must still pass — they assert byte-level output.

- [ ] **Step 4: Commit**

```bash
git add src/SequentialGuid/GuidV7.cs
git commit -m "perf: GuidV7.NewGuid(long) uses stackalloc on NET6+"
```

---

### Task 1.4: Convert `GuidV8Time.NewGuid(long)` to `stackalloc` on NET6+

**Files:**
- Modify: `src/SequentialGuid/GuidV8Time.cs:164-204`

- [ ] **Step 1: Replace the body of `internal static Guid NewGuid(long timestamp)`**

Replace lines 164–204:

```csharp
	internal static Guid NewGuid(long timestamp)
	{
		// only use low order 22 bits
		var increment = Interlocked.Increment(ref s_increment) & 0x003fffff;

#if NET6_0_OR_GREATER
		Span<byte> bytes = stackalloc byte[16];
#else
		var bytes = new byte[16];
#endif
		// custom_a: timestamp bits [59:12] → octets 0-5
		bytes[0] = (byte)(timestamp >> 52);
		bytes[1] = (byte)(timestamp >> 44);
		bytes[2] = (byte)(timestamp >> 36);
		bytes[3] = (byte)(timestamp >> 28);
		bytes[4] = (byte)(timestamp >> 20);
		bytes[5] = (byte)(timestamp >> 12);

		// custom_b: timestamp bits [11:0] → octets 6-7 (version takes upper nibble of octet 6)
		bytes[6] = (byte)((timestamp >> 8) & 0x0F);
		bytes[7] = (byte)timestamp;

		// custom_c: increment[21:0] + MachinePid → octets 8-15 (variant takes upper 2 bits of octet 8)
		bytes[8] = (byte)((increment >> 16) & 0x3F);
		bytes[9] = (byte)(increment >> 8);
		bytes[10] = (byte)increment;
		bytes[11] = MachinePid[0];
		bytes[12] = MachinePid[1];
		bytes[13] = MachinePid[2];
		bytes[14] = MachinePid[3];
		bytes[15] = MachinePid[4];

		bytes.SetRfc9562Version(8);
		bytes.SetRfc9562Variant();

#if NET6_0_OR_GREATER
		return new(bytes, bigEndian: true);
#else
		return new(bytes.SwapByteOrder());
#endif
	}
```

- [ ] **Step 2: Build all TFMs**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED. No warnings.

- [ ] **Step 3: Run the GuidV8Time test suite**

```powershell
dotnet test test/SequentialGuid.Tests --filter "FullyQualifiedName~GuidV8Time"
```

Expected: all green.

- [ ] **Step 4: Commit**

```bash
git add src/SequentialGuid/GuidV8Time.cs
git commit -m "perf: GuidV8Time.NewGuid(long) uses stackalloc on NET6+"
```

---

### Task 1.5: Switch `GuidV8Time` machine-name hash from SHA-512 to SHA-1

**Files:**
- Modify: `src/SequentialGuid/GuidV8Time.cs:29-70`

**Context:** The static ctor hashes `Environment.MachineName` to extract a 3-byte machine fingerprint. SHA-512 is wildly oversized for 3 bytes. SHA-1 is enough (this is a fingerprint, not a security property) and faster on cold start. Output bytes change, but `MachinePid` is part of the random/identity field of UUIDv8 custom layout — no test asserts a specific value.

- [ ] **Step 1: Replace the static constructor body**

Open `src/SequentialGuid/GuidV8Time.cs`. Replace the body of the static constructor (lines 29–70):

```csharp
	static GuidV8Time()
	{
#if NET6_0_OR_GREATER
		s_increment = RandomNumberGenerator
#else
		using var rng = RandomNumberGenerator.Create();
		s_increment = rng
#endif
			.GetInt32(500000);
		MachinePid = new byte[5];
#if NET6_0_OR_GREATER
		var hash = SHA1.HashData
#else
		using var algorithm = SHA1.Create();
		var hash = algorithm.ComputeHash
#endif
			(Encoding.UTF8.GetBytes(Environment.MachineName));
		for (var i = 0; i < 3; i++)
			MachinePid[i] = hash[i];
		try
		{
			var pid =
#if NET6_0_OR_GREATER
					Environment.ProcessId
#else
					Process.GetCurrentProcess().Id
#endif
				;
			// use low order two bytes only
			MachinePid[3] = (byte)(pid >> 8);
			MachinePid[4] = (byte)pid;
		}
		catch (SecurityException)
		{
		}
	}
```

- [ ] **Step 2: Build all TFMs**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED. No warnings.

- [ ] **Step 3: Run the GuidV8Time test suite**

```powershell
dotnet test test/SequentialGuid.Tests --filter "FullyQualifiedName~GuidV8Time"
```

Expected: all green.

- [ ] **Step 4: Commit**

```bash
git add src/SequentialGuid/GuidV8Time.cs
git commit -m "perf: GuidV8Time uses SHA-1 for machine fingerprint"
```

---

### Task 1.6: Stack-allocate the digest in `GuidNameBased.Create` on NET6+

**Files:**
- Modify: `src/SequentialGuid/GuidNameBased.cs`

**Context:** `IncrementalHash.TryGetHashAndReset(Span<byte>, out int)` is .NET 5+ / .NET Standard 2.1+ only — *not* netstandard2.0. So the Span path applies to NET6+ only. netstandard2.0 keeps `GetHashAndReset()`. NETFRAMEWORK path (uses `HashAlgorithm.Create`) is unchanged.

- [ ] **Step 1: Rewrite `GuidNameBased.Create`**

Replace the entire method body (lines 16–45):

```csharp
	internal static Guid Create(Guid namespaceId, byte[] name, HashAlgorithmName algorithmName, byte version)
	{
#if NETFRAMEWORK
		// Legacy .NET Framework — full-blown hash then read .Hash
		using var hash = HashAlgorithm.Create(algorithmName.Name)!;
		hash.TransformBlock(namespaceId.ToByteArray().SwapByteOrder(), 0, 16, null, 0);
		hash.TransformFinalBlock(name, 0, name.Length);
		var digest = hash.Hash;
		digest.SetRfc9562Version(version);
		digest.SetRfc9562Variant();
		return new(digest.SwapByteOrder());
#else
		using var hash = IncrementalHash.CreateHash(algorithmName);
		hash.AppendData(namespaceId
#if NETSTANDARD
			.ToByteArray().SwapByteOrder()
#else
			.ToByteArray(true)
#endif
		);
		hash.AppendData(name);
#if NET6_0_OR_GREATER
		// SHA-256 is the widest digest we use (32 bytes); SHA-1 fills the first 20.
		Span<byte> digest = stackalloc byte[32];
		hash.TryGetHashAndReset(digest, out _);
		var head = digest[..16];
		head.SetRfc9562Version(version);
		head.SetRfc9562Variant();
		return new(head, bigEndian: true);
#else
		var digest = hash.GetHashAndReset();
		digest.SetRfc9562Version(version);
		digest.SetRfc9562Variant();
		return new(digest.SwapByteOrder());
#endif
#endif
	}
```

- [ ] **Step 2: Build all TFMs**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED on all five TFMs. No warnings.

- [ ] **Step 3: Run the V5 and V8Name test suites**

```powershell
dotnet test test/SequentialGuid.Tests --filter "FullyQualifiedName~GuidV5|FullyQualifiedName~GuidV8Name"
```

Expected: all green. RFC 9562 deterministic test vectors must still produce the documented output.

- [ ] **Step 4: Commit**

```bash
git add src/SequentialGuid/GuidNameBased.cs
git commit -m "perf: stackalloc digest in GuidNameBased on NET6+"
```

---

### Task 1.7: Refactor struct ctors to stop double-decoding the timestamp

**Files:**
- Modify: `src/SequentialGuid/SequentialGuid.cs:24-39`
- Modify: `src/SequentialGuid/SequentialSqlGuid.cs:25-40`

**Context:** Both struct constructors currently call `GuidV{7,8}.Timestamp` (a public static property) and pass the truncated value back into `NewGuid(...)`. They then assign `Timestamp` from the static property. Drop the parallel read — decode `Timestamp` from `Value` after construction. This is internal-only refactor; the public `Timestamp` static properties are still alive (they get deleted in PR2).

- [ ] **Step 1: Replace `SequentialGuid(SequentialGuidType type)`**

Open `src/SequentialGuid/SequentialGuid.cs`. Replace lines 24–39:

```csharp
	public SequentialGuid(SequentialGuidType type = SequentialGuidType.Rfc9562V7)
	{
		Value = type switch
		{
			SequentialGuidType.Rfc9562V7 => GuidV7.NewGuid(),
			SequentialGuidType.Rfc9562V8Custom => GuidV8Time.NewGuid(),
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};
		Timestamp = Value.ToDateTime().GetValueOrDefault();
	}
```

- [ ] **Step 2: Replace `SequentialSqlGuid(SequentialGuidType type)`**

Open `src/SequentialGuid/SequentialSqlGuid.cs`. Replace lines 25–40:

```csharp
	public SequentialSqlGuid(SequentialGuidType type)
	{
		Value = type switch
		{
			SequentialGuidType.Rfc9562V7 => GuidV7.NewSqlGuid(),
			SequentialGuidType.Rfc9562V8Custom => GuidV8Time.NewSqlGuid(),
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};
		Timestamp = Value.ToDateTime().GetValueOrDefault();
	}
```

- [ ] **Step 3: Build and run the struct tests**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
dotnet test test/SequentialGuid.Tests --filter "FullyQualifiedName~Struct"
```

Expected: all green.

- [ ] **Step 4: Commit**

```bash
git add src/SequentialGuid/SequentialGuid.cs src/SequentialGuid/SequentialSqlGuid.cs
git commit -m "perf: struct ctors decode timestamp from Value, not parallel read"
```

---

### Task 1.8: Verify zero-allocation generation in benchmarks

**Files:**
- Run: `util/Benchmarks/GenerationBenchmarks.cs`

- [ ] **Step 1: Run the generation benchmarks**

```powershell
dotnet run -c Release --project util/Benchmarks -- --filter *Generation*
```

Expected: in the BenchmarkDotNet output, the `Allocated` column for `GuidV4NewGuid`, `GuidV7NewGuid`, `GuidV7NewSqlGuid`, `GuidV8TimeNewGuid`, `GuidV8TimeNewSqlGuid` is `-` or `0 B`. The `Guid.NewGuid` baseline is also `-`.

If any row shows non-zero allocations, the corresponding generator still has a heap allocation — revisit the prior task that converted it.

- [ ] **Step 2: Run the name benchmarks**

```powershell
dotnet run -c Release --project util/Benchmarks -- --filter *Name*
```

Expected: `GuidV5.Create(byte[])` and `GuidV8Name.Create(byte[])` show zero allocations (the digest path is now on the stack on .NET 10). The `(string)` overloads still allocate — `Encoding.UTF8.GetBytes` is the caller's contribution.

- [ ] **Step 3: Capture the output**

Save the benchmark table to a temporary file for the PR description:

```powershell
dotnet run -c Release --project util/Benchmarks -- --filter *Generation* > bench-perf-pr.txt
dotnet run -c Release --project util/Benchmarks -- --filter *Name* >> bench-perf-pr.txt
```

No commit — this is purely for the PR description.

---

### Task 1.9: Open PR1 against `vNext`

- [ ] **Step 1: Push the perf branch**

```bash
git push -u origin vNext/perf
```

- [ ] **Step 2: Open the PR**

```bash
gh pr create --base vNext --title "perf: zero-allocation generation paths" --body "$(cat <<'EOF'
## Summary
- `GuidV4`, `GuidV7`, `GuidV8Time` generators use `stackalloc byte[16]` on NET6+
- `GuidNameBased` digest is stack-allocated on NET6+ (`IncrementalHash.TryGetHashAndReset`)
- `GuidV8Time` machine fingerprint hash switched from SHA-512 to SHA-1
- Struct constructors stop the parallel `Timestamp` read; decode from `Value` instead

## Test plan
- [ ] xUnit suite green on net10/net9/net8/net472
- [ ] `GenerationBenchmarks` shows `Allocated: 0 B` for V4/V7/V8Time
- [ ] `NameBenchmarks` shows `Allocated: 0 B` on the `(byte[])` overloads

## Benchmark results

(paste contents of bench-perf-pr.txt here)
EOF
)"
```

- [ ] **Step 3: After CI green and review, merge into `vNext`**

```bash
gh pr merge --merge --delete-branch
git checkout vNext && git pull
```

---

## PR2 — Cleanup: drop obsolete API and consolidate constructors

**Branch from `vNext`:** `git checkout -b vNext/cleanup vNext`

### Task 2.1: Add internal `SequentialGuidByteOrder.TryDetect` helper

**Files:**
- Create: `src/SequentialGuid/Extensions/SequentialGuidByteOrder.cs`

**Context:** The two struct constructors duplicate the V7/V8/legacy detection logic and the SQL-V8 false-positive guard. Extract once into an internal helper. This task adds the helper; Task 2.2 wires it up.

- [ ] **Step 1: Create the file**

Create `src/SequentialGuid/Extensions/SequentialGuidByteOrder.cs`:

```csharp
namespace SequentialGuid.Extensions;

internal static class SequentialGuidByteOrder
{
	/// <summary>
	/// Detects whether <paramref name="value"/> is a recognised sequential GUID,
	/// and in which byte order. Encapsulates the V7/V8/legacy detection and the
	/// SQL-V8 false-positive guard that disambiguates by requiring a valid timestamp
	/// when both standard and SQL detection fire on the same bytes.
	/// </summary>
	/// <param name="value">The candidate GUID.</param>
	/// <param name="wasSqlOrder">
	/// On return, <see langword="true"/> if the bytes were in SQL Server byte order;
	/// <see langword="false"/> if in standard RFC byte order.
	/// Undefined when this method returns <see langword="false"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="value"/> is a V7, V8, or legacy
	/// sequential GUID in either byte order; otherwise <see langword="false"/>.
	/// </returns>
	internal static bool TryDetect(Guid value, out bool wasSqlOrder)
	{
#if NET6_0_OR_GREATER
		Span<byte> bytes = stackalloc byte[16];
		value.TryWriteBytes(bytes);
#else
		var bytes = value.ToByteArray();
#endif
		// Standard byte order detection. Guard against SQL-ordered V8 GUIDs whose
		// counter byte (mapped to position [7]) accidentally has high nibble 7 or 8,
		// which makes IsRfc9562Version fire as a false positive. Disambiguate by
		// requiring a valid timestamp when SQL detection also fires.
		if ((bytes.IsRfc9562Version(7) || bytes.IsRfc9562Version(8) || bytes.IsLegacy()) &&
			(bytes.ToTicks() is { IsDateTime: true } ||
			 !bytes.IsSqlRfc9562Version(7) && !bytes.IsSqlRfc9562Version(8) && !bytes.IsSqlLegacy()))
		{
			wasSqlOrder = false;
			return true;
		}

		if (bytes.IsSqlRfc9562Version(7) || bytes.IsSqlRfc9562Version(8) || bytes.IsSqlLegacy())
		{
			wasSqlOrder = true;
			return true;
		}

		wasSqlOrder = false;
		return false;
	}
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED on all five TFMs. No warnings.

- [ ] **Step 3: Commit**

```bash
git add src/SequentialGuid/Extensions/SequentialGuidByteOrder.cs
git commit -m "refactor: add SequentialGuidByteOrder.TryDetect helper"
```

---

### Task 2.2: Use `TryDetect` in `SequentialGuid` / `SequentialSqlGuid` constructors

**Files:**
- Modify: `src/SequentialGuid/SequentialGuid.cs:44-79`
- Modify: `src/SequentialGuid/SequentialSqlGuid.cs:45-79`

- [ ] **Step 1: Replace `SequentialGuid(Guid value)`**

Open `src/SequentialGuid/SequentialGuid.cs`. Replace the existing `public SequentialGuid(Guid value)` constructor (lines 44–79) with:

```csharp
	public SequentialGuid(Guid value)
	{
		if (!SequentialGuidByteOrder.TryDetect(value, out var wasSqlOrder))
			throw new ArgumentException(
				"Guid must be a version 7, version 8, or legacy sequential guid in standard or SQL Server byte order.",
				nameof(value));
		Value = wasSqlOrder ? value.FromSqlGuid() : value;
		Timestamp = Value.ToDateTime().GetValueOrDefault();
	}
```

- [ ] **Step 2: Replace `SequentialSqlGuid(Guid value)`**

Open `src/SequentialGuid/SequentialSqlGuid.cs`. Replace lines 45–79 with:

```csharp
	public SequentialSqlGuid(Guid value)
	{
		if (!SequentialGuidByteOrder.TryDetect(value, out var wasSqlOrder))
			throw new ArgumentException(
				"Guid must be a version 7, version 8, or legacy sequential guid in standard or SQL Server byte order.",
				nameof(value));
		Value = wasSqlOrder ? value : value.ToSqlGuid();
		Timestamp = Value.ToDateTime().GetValueOrDefault();
	}
```

- [ ] **Step 3: Build all TFMs**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED. No warnings.

- [ ] **Step 4: Run the struct test suites**

```powershell
dotnet test test/SequentialGuid.Tests --filter "FullyQualifiedName~Struct"
```

Expected: all green. The SQL-V8 false-positive guard test in `SequentialSqlGuidStructTests` is the critical regression case.

- [ ] **Step 5: Commit**

```bash
git add src/SequentialGuid/SequentialGuid.cs src/SequentialGuid/SequentialSqlGuid.cs
git commit -m "refactor: struct ctors delegate to SequentialGuidByteOrder.TryDetect"
```

---

### Task 2.3: Loosen `TicksExtensions.IsDateTime` upper bound (TDD)

**Files:**
- Create: `test/SequentialGuid.Tests/TicksExtensionsTests.cs`
- Modify: `src/SequentialGuid/Extensions/TicksExtensions.cs:16-18`

**Context:** Currently `value <= DateTime.UtcNow.Ticks` — a near-now timestamp can fail validation if the system clock moves backward between minting and validating. Allow 1 second of forward slack.

- [ ] **Step 1: Write the failing tests**

Create `test/SequentialGuid.Tests/TicksExtensionsTests.cs`:

```csharp
using SequentialGuid.Extensions;

namespace SequentialGuid.Tests;

public sealed class TicksExtensionsTests
{
	[Fact]
	void NowTicksAreValid()
	{
		DateTime.UtcNow.Ticks.IsDateTime.ShouldBeTrue();
	}

	[Fact]
	void UnixEpochTicksAreValid()
	{
		new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks.IsDateTime.ShouldBeTrue();
	}

	[Fact]
	void OneTickBeforeUnixEpochIsInvalid()
	{
		(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks - 1L).IsDateTime.ShouldBeFalse();
	}

	[Fact]
	void HalfSecondInFutureIsValid()
	{
		// New slack window allows up to 1 s in the future
		(DateTime.UtcNow.Ticks + TimeSpan.TicksPerMillisecond * 500).IsDateTime.ShouldBeTrue();
	}

	[Fact]
	void TwoSecondsInFutureIsInvalid()
	{
		(DateTime.UtcNow.Ticks + TimeSpan.TicksPerSecond * 2).IsDateTime.ShouldBeFalse();
	}
}
```

- [ ] **Step 2: Run the new tests — expect failures**

```powershell
dotnet test test/SequentialGuid.Tests --filter "FullyQualifiedName~TicksExtensionsTests"
```

Expected: `HalfSecondInFutureIsValid` FAILS (current code rejects future timestamps).

- [ ] **Step 3: Update `TicksExtensions.IsDateTime`**

Open `src/SequentialGuid/Extensions/TicksExtensions.cs`. Replace lines 16–18:

```csharp
		internal bool IsDateTime =>
			value >= UnixEpochTicks &&
			value <= DateTime.UtcNow.Ticks + TimeSpan.TicksPerSecond;
```

- [ ] **Step 4: Run the new tests — expect green**

```powershell
dotnet test test/SequentialGuid.Tests --filter "FullyQualifiedName~TicksExtensionsTests"
```

Expected: all green.

- [ ] **Step 5: Run the full test suite to verify no regressions**

```powershell
dotnet test
```

Expected: all green. The pre-existing `GuidV8TimeTests.TestFutureTimestampThrows` and `SequentialGuidTests` tests that assert `+1 second` throws need to still pass — they use `AddSeconds(1)` which is exactly at the slack boundary. **If those tests fail**, bump the test offset to `AddSeconds(2)` in those specific tests (do not weaken the slack — the production behavior is intentional).

Pattern for the test update if needed (in `SequentialGuidTests.cs` line ~296 and `GuidV8TimeTests.cs` line ~172):

```csharp
// Before:
Should.Throw<ArgumentException>(() => GuidV8Time.NewGuid(GuidV8Time.Timestamp.AddSeconds(1)));
// After:
Should.Throw<ArgumentException>(() => GuidV8Time.NewGuid(DateTime.UtcNow.AddSeconds(2)));
```

(Also note: `GuidV8Time.Timestamp` is being deleted in Task 2.5; use `DateTime.UtcNow` directly.)

- [ ] **Step 6: Commit**

```bash
git add test/SequentialGuid.Tests/TicksExtensionsTests.cs src/SequentialGuid/Extensions/TicksExtensions.cs
# stage any test files updated in step 5
git add test/SequentialGuid.Tests/*.cs
git commit -m "fix: TicksExtensions.IsDateTime allows 1 s clock-skew slack"
```

---

### Task 2.4: Delete obsolete generator classes

**Files:**
- Delete: `src/SequentialGuid/Obsolete/SequentialGuidGenerator.cs`
- Delete: `src/SequentialGuid/Obsolete/SequentialGuidGeneratorBase.cs`
- Delete: `src/SequentialGuid/Obsolete/SequentialSqlGuidGenerator.cs`

- [ ] **Step 1: Search the repo for any remaining references**

```powershell
dotnet build  # baseline
```

Then grep:

```powershell
git grep -n "SequentialGuidGenerator\b\|SequentialSqlGuidGenerator\b"
```

Expected: only matches inside `src/SequentialGuid/Obsolete/` and any README. Note any others (tests, README sections) for cleanup in this task's step 4.

- [ ] **Step 2: Delete the files**

```powershell
git rm src/SequentialGuid/Obsolete/SequentialGuidGenerator.cs
git rm src/SequentialGuid/Obsolete/SequentialGuidGeneratorBase.cs
git rm src/SequentialGuid/Obsolete/SequentialSqlGuidGenerator.cs
```

The `Obsolete` folder will be empty after deletion — Git removes it automatically.

- [ ] **Step 3: Build all TFMs**

```powershell
dotnet build
```

Expected: BUILD SUCCEEDED. No remaining references in the production assembly. If a test references these, fix in step 4.

- [ ] **Step 4: Remove any test references found in step 1**

If `git grep` in step 1 surfaced test files referencing the obsolete classes, update or delete those tests. Do not preserve obsolete-class test coverage — the behavior is replaced by the static methods on `GuidV8Time`, which have their own tests.

```powershell
dotnet test
```

Expected: all green.

- [ ] **Step 5: Commit**

```bash
git commit -m "feat!: remove obsolete SequentialGuidGenerator classes

BREAKING CHANGE: SequentialGuidGenerator and SequentialSqlGuidGenerator are
removed. Use GuidV8Time.NewGuid() / GuidV8Time.NewSqlGuid() instead."
```

---

### Task 2.5: Delete public `Timestamp` static properties and update all references

**Files:**
- Modify: `src/SequentialGuid/GuidV7.cs:44-45`
- Modify: `src/SequentialGuid/GuidV8Time.cs:75-76`
- Modify: `test/SequentialGuid.Tests/GuidV7Tests.cs` (lines 162, 165, 236, 249, 262)
- Modify: `test/SequentialGuid.Tests/GuidV8TimeTests.cs` (lines 97, 100, 110, 160, 172)
- Modify: `test/SequentialGuid.Tests/SequentialGuidStructTests.cs` (lines 147, 150, 184, 187)
- Modify: `test/SequentialGuid.Tests/SequentialSqlGuidStructTests.cs` (lines 116, 119, 154, 157)
- Modify: `test/SequentialGuid.Tests/SequentialGuidTests.cs` (lines 236, 275, 296, 300, 329)

- [ ] **Step 1: Delete `GuidV7.Timestamp`**

Open `src/SequentialGuid/GuidV7.cs`. Delete the property and its XML doc (lines 39–45):

```csharp
	/// <summary>
	/// Gets the current date and time in Coordinated Universal Time (UTC).
	/// </summary>
	/// <remarks>This property provides the current UTC date and time with millisecond precision. The value is based
	/// on the system clock and may be affected by system time changes.</remarks>
	public static DateTime Timestamp =>
		DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).UtcDateTime;
```

- [ ] **Step 2: Delete `GuidV8Time.Timestamp`**

Open `src/SequentialGuid/GuidV8Time.cs`. Delete the property and its XML doc (lines 72–76):

```csharp
	/// <summary>
	/// Gets the current date and time in Coordinated Universal Time (UTC).
	/// </summary>
	public static DateTime Timestamp =>
		DateTime.UtcNow;
```

- [ ] **Step 3: Build to surface every broken caller**

```powershell
dotnet build
```

Expected: build fails with `CS0117: 'GuidV7' does not contain a definition for 'Timestamp'` (and same for `GuidV8Time`) in each test file. This is the list of references to fix.

- [ ] **Step 4: Replace `GuidV7.Timestamp` references in tests**

Each existing call site falls into one of three patterns. Apply the matching replacement:

**Pattern A — used as a "now" sentinel for bounding tests** (`GuidV7Tests.cs:162,165`, `SequentialGuidStructTests.cs:147,150`, `SequentialSqlGuidStructTests.cs:116,119`):

```csharp
// Before
var before = GuidV7.Timestamp;
// After
var before = DateTime.UtcNow.TruncateToMs();
```

You'll need a small helper. The test project already has `test/SequentialGuid.Tests/GuidExtensions.cs` containing an `extension(Guid id)` block. Add a sibling `extension(DateTime dt)` block inside the same `internal static class GuidExtensions`:

```csharp
	extension(DateTime dt)
	{
		internal DateTime TruncateToMs() =>
			new(dt.Ticks - dt.Ticks % TimeSpan.TicksPerMillisecond, dt.Kind);
	}
```

Place it directly below the existing `extension(Guid id) { ... }` block, still inside the class braces.

**Pattern B — used as the "expected" value for round-trip assertions** (`GuidV7Tests.cs:236,249,262`):

```csharp
// Before
var expected = GuidV7.Timestamp;
// After
var expected = DateTime.UtcNow.TruncateToMs();
```

Same pattern as A but the variable is named `expected`. Apply identically.

- [ ] **Step 5: Replace `GuidV8Time.Timestamp` references in tests**

Pattern is simpler — GuidV8Time has tick precision, so no truncation is needed. **Pure token-level replacement: every `GuidV8Time.Timestamp` → `DateTime.UtcNow`.** The `.AddSeconds(N)`, `.AddMinutes(N)`, or other surrounding expressions stay as-is (Task 2.3 already adjusted any offsets that needed bumping for the new slack window).

Files and lines (offsets approximate — search for the literal `GuidV8Time.Timestamp` to find each occurrence):

- `test/SequentialGuid.Tests/GuidV8TimeTests.cs` — 5 occurrences (originally lines 97, 100, 110, 160, 172)
- `test/SequentialGuid.Tests/SequentialGuidTests.cs` — 5 occurrences (originally lines 236, 275, 296, 300, 329)
- `test/SequentialGuid.Tests/SequentialGuidStructTests.cs` — 2 occurrences (lines 184, 187)
- `test/SequentialGuid.Tests/SequentialSqlGuidStructTests.cs` — 2 occurrences (lines 154, 157)

Suggested PowerShell to verify zero remaining occurrences after the replace:

```powershell
git grep -n "GuidV8Time\.Timestamp\|GuidV7\.Timestamp"
```

Expected: no output.

- [ ] **Step 6: Build and test**

```powershell
dotnet build
dotnet test
```

Expected: all green on all four test TFMs.

- [ ] **Step 7: Commit**

```bash
git commit -m "feat!: remove public GuidV7.Timestamp and GuidV8Time.Timestamp

BREAKING CHANGE: GuidV7.Timestamp and GuidV8Time.Timestamp static properties
are removed. Use DateTime.UtcNow directly. The struct constructors now decode
their Timestamp property from the generated Value."
```

---

### Task 2.6: Update README — remove Upgrade Guide section

**Files:**
- Modify: `src/SequentialGuid/README.md:266-275`

- [ ] **Step 1: Delete the "Upgrade Guide" section**

Open `src/SequentialGuid/README.md`. Delete the entire `## Upgrade Guide` section (the heading at line ~266 and the paragraph + table + closing note below it, through line ~275). Keep the `## Backwards Compatibility` section that follows it — that's still relevant.

After deletion, the file flows: ... `## Performance` block → `## Backwards Compatibility` block.

- [ ] **Step 2: Verify the file builds (it gets packaged into the NuGet)**

```powershell
dotnet pack src/SequentialGuid/SequentialGuid.csproj --no-build
```

Expected: PACK SUCCEEDED.

- [ ] **Step 3: Commit**

```bash
git add src/SequentialGuid/README.md
git commit -m "docs: remove Upgrade Guide section (obsolete generators are gone)"
```

---

### Task 2.7: Open PR2 against `vNext`

- [ ] **Step 1: Push and open the PR**

```bash
git push -u origin vNext/cleanup
gh pr create --base vNext --title "feat!: drop obsolete API, consolidate struct constructors" --body "$(cat <<'EOF'
## Summary
- Delete `SequentialGuidGenerator`, `SequentialSqlGuidGenerator`, and their base class
- Delete `GuidV7.Timestamp` and `GuidV8Time.Timestamp` public static properties
- Add internal `SequentialGuidByteOrder.TryDetect` helper; struct ctors delegate to it
- Loosen `TicksExtensions.IsDateTime` to allow 1 s of forward clock skew

## Test plan
- [ ] xUnit suite green on net10/net9/net8/net472
- [ ] New `TicksExtensionsTests` covers the slack boundary explicitly
- [ ] No remaining references to deleted API anywhere in the repo

## Breaking changes
- Consumers using `SequentialGuidGenerator.Instance.NewGuid()` must switch to `GuidV8Time.NewGuid()`
- Consumers using `GuidV7.Timestamp` or `GuidV8Time.Timestamp` must use `DateTime.UtcNow` directly
EOF
)"
```

- [ ] **Step 2: After CI green and review, merge into `vNext`**

```bash
gh pr merge --merge --delete-branch
git checkout vNext && git pull
```

---

## PR3 — AOT hardening

**Branch from `vNext`:** `git checkout -b vNext/aot vNext`

### Task 3.1: Add `IsAotCompatible` to all four production csproj files

**Files:**
- Modify: `src/SequentialGuid/SequentialGuid.csproj`
- Modify: `src/SequentialGuid.EntityFrameworkCore/SequentialGuid.EntityFrameworkCore.csproj`
- Modify: `src/SequentialGuid.MongoDB/SequentialGuid.MongoDB.csproj`
- Modify: `src/SequentialGuid.NodaTime/SequentialGuid.NodaTime.csproj`

**Context:** `IsAotCompatible=true` in .NET 8+ implies `IsTrimmable`, `EnableTrimAnalyzer`, and `EnableAotAnalyzer`. The setting only applies to `net8.0+` TFMs; with a multi-target project, condition it accordingly.

- [ ] **Step 1: Add the property group to `src/SequentialGuid/SequentialGuid.csproj`**

Open the file. Add a new `<PropertyGroup>` after the existing one (before `<ItemGroup>`):

```xml
	<PropertyGroup Condition="'$(TargetFramework)' == 'net8.0' or '$(TargetFramework)' == 'net9.0' or '$(TargetFramework)' == 'net10.0'">
		<IsAotCompatible>true</IsAotCompatible>
	</PropertyGroup>
```

- [ ] **Step 2: Repeat for the other three projects**

Add the same `<PropertyGroup>` block to:
- `src/SequentialGuid.EntityFrameworkCore/SequentialGuid.EntityFrameworkCore.csproj`
- `src/SequentialGuid.MongoDB/SequentialGuid.MongoDB.csproj`
- `src/SequentialGuid.NodaTime/SequentialGuid.NodaTime.csproj`

In each, place the block after the existing `<PropertyGroup>` and before any `<ItemGroup>`.

- [ ] **Step 3: Build all four projects on all TFMs**

```powershell
dotnet build
```

Expected: BUILD SUCCEEDED with no AOT/trim warnings in the core `SequentialGuid` project. The companion packages may surface warnings from their dependencies (MongoDB driver, EF Core); record any that appear for Task 3.2.

- [ ] **Step 4: Commit**

```bash
git add src/**/*.csproj
git commit -m "build: declare IsAotCompatible on all production projects"
```

---

### Task 3.2: Resolve AOT/trim analyzer warnings (if any)

**Files:**
- (depends on what surfaced in Task 3.1 step 3)

- [ ] **Step 1: Re-run the build and capture warnings**

```powershell
dotnet build 2>&1 | Select-String -Pattern "warning IL"
```

- [ ] **Step 2: Address each warning by category**

- **Core `SequentialGuid` warnings:** These are bugs. Fix the code path.
- **`SequentialGuid.MongoDB` warnings:** The MongoDB driver does its own AOT story. Document the limitation in `src/SequentialGuid.MongoDB/README.md` (add a "Native AOT" note explaining that AOT support depends on the underlying MongoDB driver). Do not suppress driver warnings inside our code; if the warning is from our consuming code, suppress with a justified `[UnconditionalSuppressMessage]` and a comment.
- **`SequentialGuid.EntityFrameworkCore` warnings:** Should be clean — value converters use static abstract `T.Create(v)`, no reflection.
- **`SequentialGuid.NodaTime` warnings:** Should be clean — pure extension methods.

If only the MongoDB package surfaces warnings, this task reduces to a doc edit:

```markdown
## Native AOT support

`SequentialGuid.MongoDB` declares `<IsAotCompatible>true</IsAotCompatible>` for
`net8.0+` TFMs. Note however that the MongoDB C# driver has its own AOT
compatibility story — verify your end-to-end scenario with `dotnet publish -r <rid> -p:PublishAot=true`.
```

- [ ] **Step 3: Rebuild and verify clean**

```powershell
dotnet build
```

Expected: BUILD SUCCEEDED, no warnings.

- [ ] **Step 4: Commit**

```bash
git add -- :/  # whatever files changed
git commit -m "build: resolve AOT analyzer warnings"
```

(If no warnings surfaced, skip step 4 entirely and this task contributes no commit.)

---

### Task 3.3: Create the AOT smoke test project

**Files:**
- Create: `test/SequentialGuid.AotSmokeTest/SequentialGuid.AotSmokeTest.csproj`
- Create: `test/SequentialGuid.AotSmokeTest/Program.cs`

- [ ] **Step 1: Create the project file**

Create `test/SequentialGuid.AotSmokeTest/SequentialGuid.AotSmokeTest.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<OutputType>Exe</OutputType>
		<TargetFramework>net10.0</TargetFramework>
		<PublishAot>true</PublishAot>
		<InvariantGlobalization>true</InvariantGlobalization>
		<IsPackable>false</IsPackable>
		<RootNamespace>SequentialGuid.AotSmokeTest</RootNamespace>
	</PropertyGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\src\SequentialGuid\SequentialGuid.csproj" />
	</ItemGroup>

</Project>
```

Note: deliberately excludes the companion packages. The smoke test exercises the *core* surface — that's what we're claiming AOT-compatible.

- [ ] **Step 2: Create `Program.cs`**

Create `test/SequentialGuid.AotSmokeTest/Program.cs`:

```csharp
using System.Text.Json;
using SequentialGuid;
using SequentialGuid.Extensions;

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
var sg = new SequentialGuid();
Check("SequentialGuid ctor", sg.Value != Guid.Empty);
Check("SequentialGuid timestamp populated", sg.Timestamp > DateTime.MinValue);

var sg2 = new SequentialGuid(v7);
Check("SequentialGuid wraps v7", sg2.Value == v7);

var sg3 = new SequentialGuid(v7.ToString());
Check("SequentialGuid wraps string", sg3.Value == v7);

// SequentialSqlGuid struct
var ssg = new SequentialSqlGuid();
Check("SequentialSqlGuid ctor", ssg.Value != Guid.Empty);

var ssg2 = new SequentialSqlGuid(v7);
Check("SequentialSqlGuid wraps v7", ssg2.Value == v7.ToSqlGuid());

// JSON converters
var opts = new JsonSerializerOptions();
opts.AddSequentialGuidConverters();
var json = JsonSerializer.Serialize(sg, opts);
var roundTripped = JsonSerializer.Deserialize<SequentialGuid>(json, opts);
Check("JSON roundtrip preserves Value", roundTripped.Value == sg.Value);

if (failures.Count == 0)
{
	Console.WriteLine("AOT smoke test: PASS");
	return 0;
}

Console.WriteLine($"AOT smoke test: FAIL ({failures.Count} failures)");
foreach (var f in failures) Console.WriteLine($"  - {f}");
return 1;
```

(Note: the smoke test deliberately uses only public API. `ToUnixMs` exists in the test project as an internal extension and is not visible across project boundaries — the timestamp check above goes through `ToDateTime()` instead.)

- [ ] **Step 3: Build and run as JIT first (smoke test the smoke test)**

```powershell
dotnet run --project test/SequentialGuid.AotSmokeTest
```

Expected: `AOT smoke test: PASS` and exit code 0.

If the `ToUnixMs` call fails to compile (it's an internal test helper, not exported), apply the fallback noted above.

- [ ] **Step 4: Publish AOT and run the binary**

```powershell
dotnet publish test/SequentialGuid.AotSmokeTest -c Release -r win-x64 -o aot-out
.\aot-out\SequentialGuid.AotSmokeTest.exe
```

Expected: `AOT smoke test: PASS` and exit code 0. **And** the `dotnet publish` step must complete without `IL2xxx` / `IL3xxx` warnings — if any surface, fix them before continuing.

- [ ] **Step 5: Commit**

```bash
git add test/SequentialGuid.AotSmokeTest/
git commit -m "test: add Native AOT smoke test for core package"
```

---

### Task 3.4: Add CI job step to run the AOT smoke test

**Files:**
- Modify: `.github/workflows/ci.yml`

- [ ] **Step 1: Add the AOT publish + run step**

Open `.github/workflows/ci.yml`. After the existing `- name: Test` step, add:

```yaml
    - name: AOT smoke test
      run: |
        dotnet publish test/SequentialGuid.AotSmokeTest -c Release -r win-x64 -o aot-out
        .\aot-out\SequentialGuid.AotSmokeTest.exe
      shell: pwsh
```

- [ ] **Step 2: Verify YAML syntax**

```powershell
# Confirm the file parses by reading it back
Get-Content .github/workflows/ci.yml
```

Visually inspect: indentation matches the existing `- name:` steps (4 spaces for the dash, 6 for the contents).

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: run AOT smoke test as part of CI"
```

---

### Task 3.5: Update README to mention Native AOT

**Files:**
- Modify: `src/SequentialGuid/README.md`

- [ ] **Step 1: Add a Native AOT bullet to the Highlights section**

Open `src/SequentialGuid/README.md`. Locate the `## Highlights` section. After the existing "**Broad platform support**" bullet, add:

```markdown
- **Native AOT compatible** — declares `IsAotCompatible=true` and is verified end-to-end with a published AOT smoke test in CI
```

- [ ] **Step 2: Update the Blazor WebAssembly mention**

In the same Highlights section, find the existing "**Broad platform support**" bullet. Update the trailing clause from:

> ...with explicit `browser` platform support for Blazor WebAssembly

to:

> ...with explicit `browser` platform support and Native AOT compatibility for Blazor WebAssembly

- [ ] **Step 3: Commit**

```bash
git add src/SequentialGuid/README.md
git commit -m "docs: announce Native AOT support"
```

---

### Task 3.6: Open PR3 against `vNext`

- [ ] **Step 1: Push and open the PR**

```bash
git push -u origin vNext/aot
gh pr create --base vNext --title "build: Native AOT compatibility" --body "$(cat <<'EOF'
## Summary
- All four production projects declare `IsAotCompatible=true` on net8.0+ TFMs
- New `test/SequentialGuid.AotSmokeTest/` console app exercises every public entry point
- CI publishes and runs the AOT smoke test on every PR
- README announces AOT support

## Test plan
- [ ] xUnit suite green on net10/net9/net8/net472
- [ ] `dotnet publish` of AOT smoke test produces no IL2xxx/IL3xxx warnings
- [ ] AOT smoke test exits 0 in CI
EOF
)"
```

- [ ] **Step 2: After CI green and review, merge into `vNext`**

```bash
gh pr merge --merge --delete-branch
git checkout vNext && git pull
```

---

## Release

### Task 4: Open the integration PR `vNext` → `master`

- [ ] **Step 1: Verify `vNext` is at the right state**

```powershell
git checkout vNext && git pull
dotnet build
dotnet test
dotnet run -c Release --project util/Benchmarks -- --filter *Generation*
```

All three commands clean.

- [ ] **Step 2: Open the integration PR**

```bash
gh pr create --base master --head vNext --title "release: vN.0.0 — zero-alloc, drop obsolete API, Native AOT" --body "$(cat <<'EOF'
## Summary
Major release. Three independently-reviewed PRs already landed on `vNext`:

1. **perf** — Zero-allocation generation paths on .NET 6+
2. **feat!** — Removed obsolete `SequentialGuidGenerator` family and public `GuidV{7,8}.Timestamp` properties; consolidated struct constructors
3. **build** — Native AOT compatibility with a CI-verified smoke test

## Breaking changes
- `SequentialGuidGenerator` and `SequentialSqlGuidGenerator` removed → use `GuidV8Time.NewGuid()` / `GuidV8Time.NewSqlGuid()`
- `GuidV7.Timestamp` and `GuidV8Time.Timestamp` removed → use `DateTime.UtcNow`

## Test plan
- [ ] CI green
- [ ] Manual `dotnet pack` produces clean NuGet packages
- [ ] Local consumer project upgrades cleanly from previous major
EOF
)"
```

- [ ] **Step 3: After merge, tag and publish**

```bash
git checkout master && git pull
git tag vN.0.0
git push origin vN.0.0
# Release workflow handles NuGet push
```

(Replace `vN.0.0` with the actual next major version number — check current `<Version>` in csproj or the latest GitHub release.)

---

## Open questions

None — all scope decisions are locked in by the spec at
`docs/superpowers/specs/2026-05-12-sequentialguid-vnext-design.md`.
