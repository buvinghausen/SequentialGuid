# SequentialGuid v6.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the last allocation gap in `GuidNameBased.Create` (the path behind `GuidV5.Create` and `GuidV8Name.Create`), apply free perf wins missed in v6.0, and add four small ergonomic extension members. SemVer minor release v6.1.0, single PR against `master`.

**Architecture:** All work on a single feature branch `v6.1/perf-and-ergonomics`. Eleven implementation tasks then one final PR. No breaking changes — everything is additive or internal. Spec lives at `docs/superpowers/specs/2026-05-12-sequentialguid-v6.1-design.md`.

**Tech Stack:** .NET 10 / 9 / 8 + .NET Framework 4.6.2 + .NET Standard 2.0 (core); xUnit v3 + Shouldly (tests); BenchmarkDotNet (benchmarks); GitHub Actions on windows-latest (CI).

---

## Prep

### Task 0: Create feature branch

- [ ] **Step 1: Create and switch to the feature branch**

```bash
git checkout -b v6.1/perf-and-ergonomics master
```

Expected: `Switched to a new branch 'v6.1/perf-and-ergonomics'`.

No commit yet — this is just the branch setup.

---

## Section 1 — Deterministic generator zero-allocation

### Task 1: Add `SwapGuidBytesInPlace` Span helper

**Files:**
- Modify: `C:\code\Buvinghausen\SequentialGuid\src\SequentialGuid\Extensions\ByteArrayExtensions.cs`

**Context:** Task 2's NET6/7 path needs to write a `Guid`'s bytes in big-endian (network) order. `Guid.TryWriteBytes(span, bigEndian, out _)` is NET8+ only. On NET6/7 we can call `TryWriteBytes(span)` (native mixed-endian) and then swap the bytes in place. This task adds the in-place swap helper.

- [ ] **Step 1: Locate the existing `extension(Span<byte> b)` block in `ByteArrayExtensions.cs`**

It currently contains `SetRfc9562Version` and `SetRfc9562Variant` (lines ~213-223). Open the file and find that block.

- [ ] **Step 2: Add `SwapGuidBytesInPlace` to the existing `extension(Span<byte> b)` block**

Inside the block, after the existing `SetRfc9562Variant()` method, add:

```csharp
		// Swaps the first 16 bytes of `b` between .NET mixed-endian and RFC 9562 network (big-endian)
		// byte order, in place. Reverses Data1 (4 bytes), Data2 (2 bytes), Data3 (2 bytes); Data4 unchanged.
		// This mapping is self-inverse: applying it twice returns the original bytes.
		internal void SwapGuidBytesInPlace()
		{
			(b[0], b[3]) = (b[3], b[0]);
			(b[1], b[2]) = (b[2], b[1]);
			(b[4], b[5]) = (b[5], b[4]);
			(b[6], b[7]) = (b[7], b[6]);
		}
```

- [ ] **Step 3: Build**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED on all 5 TFMs (net10.0, net9.0, net8.0, net462, netstandard2.0), 0 warnings.

- [ ] **Step 4: Run tests**

```powershell
dotnet test
```

Expected: all green (no behavior change yet — this helper isn't called from anywhere yet).

- [ ] **Step 5: Commit**

```bash
git add src/SequentialGuid/Extensions/ByteArrayExtensions.cs
git commit -m "perf: add SwapGuidBytesInPlace Span helper for GuidNameBased rewrite"
```

---

### Task 2: Rewrite `GuidNameBased.Create` — zero-alloc + flat `#if` nest

**Files:**
- Modify: `C:\code\Buvinghausen\SequentialGuid\src\SequentialGuid\GuidNameBased.cs`

**Context:** Replace the `IncrementalHash`-based path with one-shot `SHA1.HashData`/`SHA256.HashData` (both .NET 5+, zero-alloc). Use a stack-allocated concat buffer (`namespace[16] + name`) with `ArrayPool<byte>.Shared` fallback when total length exceeds 256 bytes. On NET8+, write the namespace directly in big-endian order; on NET6/7 use the new `SwapGuidBytesInPlace` helper from Task 1. The `netstandard2.0` and `NETFRAMEWORK` paths remain unchanged.

Net effect: `GuidV5.Create(byte[])` and `GuidV8Name.Create(byte[])` drop from ~176 B/call to 0 B/call on .NET 6+.

- [ ] **Step 1: Open `src/SequentialGuid/GuidNameBased.cs`** — note the current state for comparison

The current file (after v6.0) has nested `#if` for NETFRAMEWORK / NETSTANDARD / NET6_0_OR_GREATER. Total method is ~38 lines.

- [ ] **Step 2: Add the `using System.Buffers;` directive at the top of the file**

Insert after the existing `using System.Security.Cryptography;` line:

```csharp
using System.Buffers;
using System.Security.Cryptography;
using SequentialGuid.Extensions;
```

(If `System.Buffers` is already covered by an implicit using or `using static` directive elsewhere — verify by attempting the build in Step 5; if so, no edit needed. `ArrayPool<T>` lives in `System.Buffers`.)

- [ ] **Step 3: Replace the entire `Create` method body**

Replace lines 16-52 (the entire `internal static Guid Create(...)` body, keeping the existing namespace + class brace structure):

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
#elif NET6_0_OR_GREATER
		const int StackThreshold = 256;
		var totalLen = 16 + name.Length;

		Span<byte> stackBuf = stackalloc byte[StackThreshold];
		byte[]? rented = null;
		var input = totalLen <= StackThreshold
			? stackBuf[..totalLen]
			: (rented = ArrayPool<byte>.Shared.Rent(totalLen)).AsSpan(0, totalLen);
		try
		{
#if NET8_0_OR_GREATER
			namespaceId.TryWriteBytes(input[..16], bigEndian: true, out _);
#else
			// NET6/7: TryWriteBytes(span, bigEndian) doesn't exist; write native order then swap in-place
			namespaceId.TryWriteBytes(input[..16]);
			input[..16].SwapGuidBytesInPlace();
#endif
			name.AsSpan().CopyTo(input.Slice(16, name.Length));

			// SHA-256 is the widest digest we use (32 bytes); SHA-1 fills the first 20.
			Span<byte> digest = stackalloc byte[32];
			if (algorithmName == HashAlgorithmName.SHA1)
				SHA1.HashData(input, digest);
			else
				SHA256.HashData(input, digest);

			var head = digest[..16];
			head.SetRfc9562Version(version);
			head.SetRfc9562Variant();
			return new(head, bigEndian: true);
		}
		finally
		{
			if (rented is not null) ArrayPool<byte>.Shared.Return(rented);
		}
#else
		// netstandard2.0 — TryGetHashAndReset(Span<byte>, out int) is .NET 5+ / netstandard 2.1+
		using var hash = IncrementalHash.CreateHash(algorithmName);
		hash.AppendData(namespaceId.ToByteArray().SwapByteOrder());
		hash.AppendData(name);
		var digest = hash.GetHashAndReset();
		digest.SetRfc9562Version(version);
		digest.SetRfc9562Variant();
		return new(digest.SwapByteOrder());
#endif
	}
```

- [ ] **Step 4: Verify the final file structure**

The full file should now flow:
1. `using System.Buffers;` (newly added)
2. `using System.Security.Cryptography;`
3. `using SequentialGuid.Extensions;`
4. `namespace SequentialGuid;`
5. `internal static class GuidNameBased { ... }` containing `Namespaces` nested class + the rewritten `Create` method

- [ ] **Step 5: Build**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED on all 5 TFMs, 0 warnings.

If you get a build error about `ArrayPool` not being found, the `using System.Buffers;` directive is required — add it (Step 2). Verify by re-running this step.

- [ ] **Step 6: Run the V5 and V8Name test suites — critical RFC test vectors must still match**

```powershell
dotnet test test/SequentialGuid.Tests -- --filter-method "*GuidV5*"
dotnet test test/SequentialGuid.Tests -- --filter-method "*GuidV8Name*"
```

Expected: all green. The key tests assert deterministic output for known namespace+name pairs (RFC 9562 Appendix A.4 vector `2ed6657d-e927-568b-95e1-2665a8aea6a2` for SHA-1, and the SHA-256 vectors in `GuidV8NameTests.cs`). If any byte-level test fails, the rewrite is incorrect.

- [ ] **Step 7: Run the full test suite**

```powershell
dotnet test
```

Expected: all green.

- [ ] **Step 8: Commit**

```bash
git add src/SequentialGuid/GuidNameBased.cs
git commit -m "perf: zero-alloc GuidNameBased.Create via stackalloc + HashData

Replaces IncrementalHash with one-shot SHA1.HashData / SHA256.HashData on
NET6+. Concat buffer is stackalloc'd up to 256B with ArrayPool fallback.
NET8+ writes the namespace directly in network byte order; NET6/7 falls back
to TryWriteBytes + in-place swap. netstandard2.0 and NETFRAMEWORK paths
unchanged. Result: GuidV5/GuidV8Name no longer allocate on .NET 6+."
```

---

## Section 2 — Free perf wins

### Task 3: Apply `[SkipLocalsInit]` to generator hot paths

**Files:**
- Modify: `C:\code\Buvinghausen\SequentialGuid\src\SequentialGuid\GuidV4.cs`
- Modify: `C:\code\Buvinghausen\SequentialGuid\src\SequentialGuid\GuidV7.cs`
- Modify: `C:\code\Buvinghausen\SequentialGuid\src\SequentialGuid\GuidV8Time.cs`
- Modify: `C:\code\Buvinghausen\SequentialGuid\src\SequentialGuid\GuidNameBased.cs`

**Context:** Each generator method's stackalloc'd buffer is fully written before use — every byte index is explicitly assigned or filled by `RandomNumberGenerator.Fill` / `SHA*.HashData`. The JIT's automatic zero-init is redundant work. `[SkipLocalsInit]` is already on the struct wrappers (class-level); extending to generator methods is consistent.

The polyfill shim at `src/SequentialGuid/SkipLocalsInitPolyfill.cs` already declares `AttributeTargets.Method` so the attribute applies to methods on all TFMs including netstandard2.0 and net462.

- [ ] **Step 1: Add `using System.Runtime.CompilerServices;` to each file that doesn't already have it**

Check `GuidV4.cs`, `GuidV7.cs`, `GuidV8Time.cs`, `GuidNameBased.cs`. The attribute `[SkipLocalsInit]` lives in that namespace. If any file is missing the directive, add it at the top.

- [ ] **Step 2: Add `[SkipLocalsInit]` to `GuidV4.NewGuid`**

Open `src/SequentialGuid/GuidV4.cs`. Add the attribute directly above the `public static Guid NewGuid()` method declaration:

```csharp
	/// <summary>
	/// Creates a new UUID version 4 using a cryptographically strong random number generator.
	/// </summary>
	/// <returns>A new random version 4 <see cref="Guid"/>.</returns>
	[SkipLocalsInit]
	public static Guid NewGuid()
	{
		// ... existing body unchanged
```

- [ ] **Step 3: Add `[SkipLocalsInit]` to `GuidV7.NewGuid(long)`**

Open `src/SequentialGuid/GuidV7.cs`. Find `public static Guid NewGuid(long unixMilliseconds)` (around line 128). Add the attribute directly above the method declaration (after the XML doc):

```csharp
	[SkipLocalsInit]
	public static Guid NewGuid(long unixMilliseconds)
	{
		// ... existing body unchanged
```

- [ ] **Step 4: Add `[SkipLocalsInit]` to `GuidV8Time.NewGuid(long)`**

Open `src/SequentialGuid/GuidV8Time.cs`. Find `internal static Guid NewGuid(long timestamp)` (around line 158). Add the attribute:

```csharp
	[SkipLocalsInit]
	internal static Guid NewGuid(long timestamp)
	{
		// ... existing body unchanged
```

- [ ] **Step 5: Add `[SkipLocalsInit]` to `GuidNameBased.Create`**

Open `src/SequentialGuid/GuidNameBased.cs`. Find the `internal static Guid Create(...)` method. Add the attribute:

```csharp
	[SkipLocalsInit]
	internal static Guid Create(Guid namespaceId, byte[] name, HashAlgorithmName algorithmName, byte version)
	{
		// ... body from Task 2
```

- [ ] **Step 6: Build**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED on all 5 TFMs, 0 warnings.

- [ ] **Step 7: Run the full test suite (sanity check — no behavior change expected)**

```powershell
dotnet test
```

Expected: all green.

- [ ] **Step 8: Commit**

```bash
git add src/SequentialGuid/GuidV4.cs src/SequentialGuid/GuidV7.cs src/SequentialGuid/GuidV8Time.cs src/SequentialGuid/GuidNameBased.cs
git commit -m "perf: apply [SkipLocalsInit] to generator hot paths"
```

---

### Task 4: `GuidV7.NewGuid()` no-arg cleanup — eliminate one struct copy

**Files:**
- Modify: `C:\code\Buvinghausen\SequentialGuid\src\SequentialGuid\GuidV7.cs`

**Context:** The no-arg `GuidV7.NewGuid()` currently calls `NewGuid(DateTimeOffset.UtcNow)`, which immediately calls `NewGuid(timestamp.ToUnixTimeMilliseconds())`. The intermediate `DateTimeOffset` parameter pass is unnecessary — extract the ms value inline.

- [ ] **Step 1: Open `src/SequentialGuid/GuidV7.cs` and locate the no-arg `NewGuid()`**

Around line 90:

```csharp
	public static Guid NewGuid() =>
		NewGuid(DateTimeOffset.UtcNow);
```

- [ ] **Step 2: Replace the body**

```csharp
	public static Guid NewGuid() =>
		NewGuid(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
```

Keep the XML doc above unchanged. The `NewGuid(DateTimeOffset)` overload itself stays — only the no-arg call site changes.

- [ ] **Step 3: Build**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED, 0 warnings.

- [ ] **Step 4: Run the GuidV7 test suite**

```powershell
dotnet test test/SequentialGuid.Tests -- --filter-method "*GuidV7*"
```

Expected: all green.

- [ ] **Step 5: Commit**

```bash
git add src/SequentialGuid/GuidV7.cs
git commit -m "perf: GuidV7.NewGuid() inlines unix-ms extraction, saves one struct copy"
```

---

## Section 3 — Ergonomic features (TDD)

### Task 5: Add `Guid.MaxValue` static extension property (TDD)

**Files:**
- Create: `C:\code\Buvinghausen\SequentialGuid\test\SequentialGuid.Tests\GuidExtensionsTests.cs`
- Modify: `C:\code\Buvinghausen\SequentialGuid\src\SequentialGuid\Extensions\GuidExtensions.cs`

**Context:** RFC 9562 §5.10 defines the Max UUID as all bits set (`ffffffff-ffff-ffff-ffff-ffffffffffff`). Add a static extension property on `Guid` so consumers get `Guid.MaxValue` syntax (matching `int.MaxValue` / `DateTime.MaxValue`).

- [ ] **Step 1: Create the failing test file**

Create `test/SequentialGuid.Tests/GuidExtensionsTests.cs`:

```csharp
namespace SequentialGuid.Tests;

public sealed class GuidExtensionsTests
{
	[Fact]
	void MaxValueIsAllBitsSet()
	{
		Guid.MaxValue.ShouldBe(new("ffffffff-ffff-ffff-ffff-ffffffffffff"));
	}

	[Fact]
	void MaxValueIsNotEmpty()
	{
		Guid.MaxValue.ShouldNotBe(Guid.Empty);
	}
}
```

- [ ] **Step 2: Run the new tests — expect compile failure**

```powershell
dotnet test test/SequentialGuid.Tests -- --filter-method "*GuidExtensionsTests*"
```

Expected: build error `CS0117: 'Guid' does not contain a definition for 'MaxValue'`.

- [ ] **Step 3: Open `src/SequentialGuid/Extensions/GuidExtensions.cs` and add the static extension block**

The current file has `extension(Guid id)` for instance members. Add a new `extension(Guid)` block (no parameter name — that's the static-extension syntax) and a private static-readonly cache field.

Insert after the `[SkipLocalsInit]` line and before the existing `extension(Guid id)` block:

```csharp
[SkipLocalsInit]
public static class GuidExtensions
{
	private static readonly Guid s_maxValue = new("ffffffff-ffff-ffff-ffff-ffffffffffff");

	extension(Guid)
	{
		/// <summary>Gets the RFC 9562 §5.10 max UUID — all bits set to 1.</summary>
		public static Guid MaxValue => s_maxValue;
	}

	extension(Guid id)
	{
		// ... existing members unchanged
```

(The `s_maxValue` field lives at class scope, alongside the extension blocks.)

- [ ] **Step 4: Build**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED on all 5 TFMs, 0 warnings.

- [ ] **Step 5: Run the tests — expect both pass**

```powershell
dotnet test test/SequentialGuid.Tests -- --filter-method "*GuidExtensionsTests*"
```

Expected: 2 tests passing (`MaxValueIsAllBitsSet`, `MaxValueIsNotEmpty`).

- [ ] **Step 6: Commit**

```bash
git add test/SequentialGuid.Tests/GuidExtensionsTests.cs src/SequentialGuid/Extensions/GuidExtensions.cs
git commit -m "feat: add Guid.MaxValue static extension property

Per RFC 9562 §5.10 (Max UUID). Naming matches BCL convention
(int.MaxValue / DateTime.MaxValue)."
```

---

### Task 6: Add `TryToDateTime` extension (TDD)

**Files:**
- Modify: `C:\code\Buvinghausen\SequentialGuid\test\SequentialGuid.Tests\GuidExtensionsTests.cs`
- Modify: `C:\code\Buvinghausen\SequentialGuid\src\SequentialGuid\Extensions\GuidExtensions.cs`

**Context:** BCL-style `Try` pattern for the existing `ToDateTime()` extension. Returns `bool` with a `DateTime` out param. Returns `false` for non-sequential GUIDs (where the existing `ToDateTime()` returns `null`).

- [ ] **Step 1: Append failing tests to `GuidExtensionsTests.cs`**

Open the file. Inside the existing `public sealed class GuidExtensionsTests` block, before the closing `}`, append:

```csharp
	[Fact]
	void TryToDateTimeReturnsTrueForSequentialGuid()
	{
		// Arrange
		var v7 = GuidV7.NewGuid();
		// Act
		var success = v7.TryToDateTime(out var timestamp);
		// Assert
		success.ShouldBeTrue();
		timestamp.ShouldBe(v7.ToDateTime()!.Value);
	}

	[Fact]
	void TryToDateTimeReturnsFalseForRandomV4()
	{
		// RFC 9562 §A.4 v4 vector — known not a sequential guid
		var v4 = new Guid("919108f7-52d1-4320-9bac-f847db4148a8");
		var success = v4.TryToDateTime(out var timestamp);
		success.ShouldBeFalse();
		timestamp.ShouldBe(default(DateTime));
	}

	[Fact]
	void TryToDateTimeReturnsFalseForEmpty()
	{
		var success = Guid.Empty.TryToDateTime(out var timestamp);
		success.ShouldBeFalse();
		timestamp.ShouldBe(default(DateTime));
	}
```

(The `GuidV7` static class is in the `SequentialGuid` namespace; if the test file lacks a `using SequentialGuid;` directive, add it at the top.)

- [ ] **Step 2: Run the new tests — expect compile failure**

```powershell
dotnet test test/SequentialGuid.Tests -- --filter-method "*TryToDateTime*"
```

Expected: build error `'Guid' does not contain a definition for 'TryToDateTime'`.

- [ ] **Step 3: Add `TryToDateTime` to `GuidExtensions.cs`**

Inside the existing `extension(Guid id)` block, after the `ToDateTime()` method's closing brace, append:

```csharp
		/// <summary>Try-pattern variant of <see cref="ToDateTime"/>.</summary>
		/// <param name="timestamp">When this method returns <see langword="true"/>, the embedded UTC timestamp; otherwise <c>default</c>.</param>
		/// <returns><see langword="true"/> if the GUID contains a valid embedded timestamp; otherwise <see langword="false"/>.</returns>
		public bool TryToDateTime(out DateTime timestamp)
		{
			if (id.ToDateTime() is { } dt)
			{
				timestamp = dt;
				return true;
			}
			timestamp = default;
			return false;
		}
```

- [ ] **Step 4: Build**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED, 0 warnings.

- [ ] **Step 5: Run the tests**

```powershell
dotnet test test/SequentialGuid.Tests -- --filter-method "*TryToDateTime*"
```

Expected: 3 tests passing.

- [ ] **Step 6: Commit**

```bash
git add test/SequentialGuid.Tests/GuidExtensionsTests.cs src/SequentialGuid/Extensions/GuidExtensions.cs
git commit -m "feat: add Guid.TryToDateTime extension matching BCL Try-pattern"
```

---

### Task 7: Add `ToDateTimeOffset` + `TryToDateTimeOffset` extensions (TDD)

**Files:**
- Modify: `C:\code\Buvinghausen\SequentialGuid\test\SequentialGuid.Tests\GuidExtensionsTests.cs`
- Modify: `C:\code\Buvinghausen\SequentialGuid\src\SequentialGuid\Extensions\GuidExtensions.cs`

**Context:** Many consumers prefer `DateTimeOffset` (explicit UTC offset) over `DateTime` (implicit kind). Mirror the existing `ToDateTime()` / new `TryToDateTime` pair. Returned `DateTimeOffset` always has `Offset == TimeSpan.Zero` because the embedded timestamp is always UTC.

- [ ] **Step 1: Append failing tests to `GuidExtensionsTests.cs`**

Append inside the test class:

```csharp
	[Fact]
	void ToDateTimeOffsetReturnsUtcOffsetForSequentialGuid()
	{
		// Arrange
		var v7 = GuidV7.NewGuid();
		// Act
		var dto = v7.ToDateTimeOffset();
		// Assert
		dto.ShouldNotBeNull();
		dto.Value.Offset.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	void ToDateTimeOffsetReturnsNullForRandomV4()
	{
		var v4 = new Guid("919108f7-52d1-4320-9bac-f847db4148a8");
		v4.ToDateTimeOffset().ShouldBeNull();
	}

	[Fact]
	void TryToDateTimeOffsetReturnsTrueForSequentialGuid()
	{
		var v7 = GuidV7.NewGuid();
		var success = v7.TryToDateTimeOffset(out var dto);
		success.ShouldBeTrue();
		dto.Offset.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	void TryToDateTimeOffsetReturnsFalseForRandomV4()
	{
		var v4 = new Guid("919108f7-52d1-4320-9bac-f847db4148a8");
		var success = v4.TryToDateTimeOffset(out var dto);
		success.ShouldBeFalse();
		dto.ShouldBe(default(DateTimeOffset));
	}
```

- [ ] **Step 2: Run the new tests — expect compile failure**

```powershell
dotnet test test/SequentialGuid.Tests -- --filter-method "*ToDateTimeOffset*"
```

Expected: build error `'Guid' does not contain a definition for 'ToDateTimeOffset'`.

- [ ] **Step 3: Add `ToDateTimeOffset` and `TryToDateTimeOffset` to `GuidExtensions.cs`**

Inside the existing `extension(Guid id)` block, after the `TryToDateTime` method's closing brace, append:

```csharp
		/// <summary>Converts a <see cref="Guid"/> to a <see cref="DateTimeOffset"/> if the GUID contains a valid timestamp.</summary>
		/// <returns>A <see cref="DateTimeOffset"/> with a zero (UTC) offset, or <c>null</c> if the GUID does not contain a valid timestamp.</returns>
		public DateTimeOffset? ToDateTimeOffset() =>
			id.ToDateTime() is { } dt ? new DateTimeOffset(dt, TimeSpan.Zero) : null;

		/// <summary>Try-pattern variant of <see cref="ToDateTimeOffset"/>.</summary>
		/// <param name="timestamp">When this method returns <see langword="true"/>, the embedded UTC timestamp as a <see cref="DateTimeOffset"/>; otherwise <c>default</c>.</param>
		/// <returns><see langword="true"/> if the GUID contains a valid embedded timestamp; otherwise <see langword="false"/>.</returns>
		public bool TryToDateTimeOffset(out DateTimeOffset timestamp)
		{
			if (id.ToDateTime() is { } dt)
			{
				timestamp = new DateTimeOffset(dt, TimeSpan.Zero);
				return true;
			}
			timestamp = default;
			return false;
		}
```

- [ ] **Step 4: Build**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED, 0 warnings.

- [ ] **Step 5: Run the tests**

```powershell
dotnet test test/SequentialGuid.Tests -- --filter-method "*ToDateTimeOffset*"
```

Expected: 4 tests passing.

- [ ] **Step 6: Commit**

```bash
git add test/SequentialGuid.Tests/GuidExtensionsTests.cs src/SequentialGuid/Extensions/GuidExtensions.cs
git commit -m "feat: add Guid.ToDateTimeOffset + TryToDateTimeOffset extensions"
```

---

### Task 8: Add public `IsSequentialGuid` predicate (TDD)

**Files:**
- Modify: `C:\code\Buvinghausen\SequentialGuid\test\SequentialGuid.Tests\GuidExtensionsTests.cs`
- Modify: `C:\code\Buvinghausen\SequentialGuid\src\SequentialGuid\Extensions\GuidExtensions.cs`

**Context:** Currently `SequentialGuidByteOrder.TryDetect` is internal — consumers can only check if a `Guid` is recognised by try/catching the struct constructor. Add a public `IsSequentialGuid()` extension that delegates to the existing internal helper. Discards the SQL-byte-order flag (consumers who need that detail can convert through the struct types).

- [ ] **Step 1: Append failing tests to `GuidExtensionsTests.cs`**

Append inside the test class. Use a known-legacy guid string from `SequentialGuidStructTests.cs` for the legacy case:

```csharp
	[Fact]
	void IsSequentialGuidTrueForV7()
	{
		GuidV7.NewGuid().IsSequentialGuid().ShouldBeTrue();
	}

	[Fact]
	void IsSequentialGuidTrueForV8Time()
	{
		GuidV8Time.NewGuid().IsSequentialGuid().ShouldBeTrue();
	}

	[Fact]
	void IsSequentialGuidTrueForLegacy()
	{
		new Guid("08de7bf5-381d-cc8b-f24c-56e3580439dd").IsSequentialGuid().ShouldBeTrue();
	}

	[Fact]
	void IsSequentialGuidTrueForSqlOrderedV7()
	{
		GuidV7.NewSqlGuid().IsSequentialGuid().ShouldBeTrue();
	}

	[Fact]
	void IsSequentialGuidFalseForRandomV4()
	{
		// RFC 9562 §A.4 v4 vector
		new Guid("919108f7-52d1-4320-9bac-f847db4148a8").IsSequentialGuid().ShouldBeFalse();
	}

	[Fact]
	void IsSequentialGuidFalseForEmpty()
	{
		Guid.Empty.IsSequentialGuid().ShouldBeFalse();
	}

	[Fact]
	void IsSequentialGuidFalseForMaxValue()
	{
		Guid.MaxValue.IsSequentialGuid().ShouldBeFalse();
	}
```

- [ ] **Step 2: Run the new tests — expect compile failure**

```powershell
dotnet test test/SequentialGuid.Tests -- --filter-method "*IsSequentialGuid*"
```

Expected: build error `'Guid' does not contain a definition for 'IsSequentialGuid'`.

- [ ] **Step 3: Add `IsSequentialGuid` to `GuidExtensions.cs`**

Inside the existing `extension(Guid id)` block, after the `TryToDateTimeOffset` method's closing brace, append:

```csharp
		/// <summary>Returns true if <paramref name="id"/> is a recognised sequential GUID (V7, V8, or legacy format) in either standard or SQL Server byte order.</summary>
		public bool IsSequentialGuid() =>
			SequentialGuidByteOrder.TryDetect(id, out _);
```

The `SequentialGuidByteOrder` type lives in the `SequentialGuid.Extensions` namespace. The current `GuidExtensions.cs` file declares `namespace System;` (with an `IDE0130` suppression pragma) and already has `using SequentialGuid;` plus `using SequentialGuid.Extensions;` at the top — so the `SequentialGuidByteOrder.TryDetect` call resolves correctly with no additional imports needed.

- [ ] **Step 4: Build**

```powershell
dotnet build src/SequentialGuid/SequentialGuid.csproj
```

Expected: BUILD SUCCEEDED, 0 warnings.

- [ ] **Step 5: Run the tests**

```powershell
dotnet test test/SequentialGuid.Tests -- --filter-method "*IsSequentialGuid*"
```

Expected: 7 tests passing.

- [ ] **Step 6: Run the full test suite**

```powershell
dotnet test
```

Expected: all green (no regressions).

- [ ] **Step 7: Commit**

```bash
git add test/SequentialGuid.Tests/GuidExtensionsTests.cs src/SequentialGuid/Extensions/GuidExtensions.cs
git commit -m "feat: add public Guid.IsSequentialGuid predicate"
```

---

## Section 4 — Testing & verification

### Task 9: Extend AOT smoke test to cover new public surface

**Files:**
- Modify: `C:\code\Buvinghausen\SequentialGuid\test\SequentialGuid.AotSmokeTest\Program.cs`

**Context:** The AOT smoke test exercises every public entry point as a published Native AOT binary. The four new extensions need coverage so that any AOT-incompatibility regression is caught in CI.

- [ ] **Step 1: Open `test/SequentialGuid.AotSmokeTest/Program.cs`**

Find the existing block of `Check(...)` calls. The new checks need to land in the same flow, before the final `if (failures.Count == 0)` branch.

- [ ] **Step 2: Add the new check lines**

Add these `Check(...)` calls in the AOT smoke test, immediately before the final tally block. Place them next to the existing JSON-roundtrip check or at the end of the existing checks — anywhere before `if (failures.Count == 0)`:

```csharp
// v6.1 ergonomic extensions
Check("Guid.MaxValue non-empty", Guid.MaxValue != Guid.Empty);

Check("v7 TryToDateTime true", v7.TryToDateTime(out var v7Dt) && v7Dt > DateTime.MinValue);
Check("v4 TryToDateTime false", !v4.TryToDateTime(out _));

Check("v7 ToDateTimeOffset Utc", v7.ToDateTimeOffset() is { Offset.Ticks: 0 });
Check("v4 ToDateTimeOffset null", v4.ToDateTimeOffset() is null);

Check("v7 IsSequentialGuid true", v7.IsSequentialGuid());
Check("v4 IsSequentialGuid false", !v4.IsSequentialGuid());
```

(The `v4` and `v7` local variables are already declared earlier in the file from the v6.0 smoke test.)

- [ ] **Step 3: Run the JIT version of the smoke test**

```powershell
dotnet run --project test/SequentialGuid.AotSmokeTest
```

Expected: `AOT smoke test: PASS` and exit code 0.

If a check fails, the implementation in Tasks 5-8 has a bug — go fix it there, not here.

- [ ] **Step 4: (Optional, machine-dependent) Run AOT publish locally**

If your development machine has the Visual C++ Build Tools (or "Desktop Development for C++" workload), you can verify the AOT publish locally:

```powershell
dotnet publish test/SequentialGuid.AotSmokeTest -c Release -r win-x64 -o aot-out
.\aot-out\SequentialGuid.AotSmokeTest.exe
```

Expected: AOT publish produces zero `IL2xxx`/`IL3xxx` warnings; binary outputs `AOT smoke test: PASS`; exit code 0.

If the publish fails locally because the C++ linker is missing (`Platform linker not found`), that's expected — CI will run this step on `windows-latest` which ships the toolchain. Move to Step 5.

- [ ] **Step 5: Commit**

```bash
git add test/SequentialGuid.AotSmokeTest/Program.cs
git commit -m "test: extend AOT smoke test to cover v6.1 ergonomic extensions"
```

---

### Task 10: Verify zero-allocation benchmarks for name-based generators

**Files:**
- (no code changes — verification only)

**Context:** Run `NameBenchmarks` and confirm the `Allocated` column drops to 0 B for `GuidV5.Create(byte[])` and `GuidV8Name.Create(byte[])` at both input sizes.

- [ ] **Step 1: Run the name benchmarks**

```powershell
dotnet run -c Release --project util/Benchmarks -- --filter *Name*
```

Wait for BenchmarkDotNet to complete (typically 1-2 minutes).

Expected: a results table with `Method`, `Mean`, `Allocated` columns. For:
- `GuidV5.Create(byte[])` at both name lengths → `Allocated` column shows `-` or `0 B`
- `GuidV8Name.Create(byte[])` at both name lengths → `Allocated` column shows `-` or `0 B`
- `GuidV5.Create(string)` at both name lengths → still allocates (UTF-8 buffer is caller-controlled — that's expected)
- `GuidV8Name.Create(string)` at both name lengths → still allocates (same reason)

If the `(byte[])` rows still show non-zero allocation, Task 2's rewrite has a bug — investigate which path is taking heap (the unconditional `stackalloc byte[256]` should cover the test inputs; if it doesn't, verify the test inputs' lengths are <= 256).

- [ ] **Step 2: Save the benchmark output for the PR description**

```powershell
dotnet run -c Release --project util/Benchmarks -- --filter *Name* 2>&1 | Out-File -Encoding utf8 -FilePath bench-name-v6.1.txt
```

The file goes in the working directory root. **Do NOT commit it** — `bench-*.txt` is in `.gitignore`.

- [ ] **Step 3: Also re-run generation benchmarks to confirm no regression**

```powershell
dotnet run -c Release --project util/Benchmarks -- --filter *Generation*
```

Expected: still 0 B allocated for all generation rows (this should be unchanged from v6.0).

- [ ] **Step 4: No commit**

This task is verification only.

---

## Section 5 — Documentation

### Task 11: README updates

**Files:**
- Modify: `C:\code\Buvinghausen\SequentialGuid\src\SequentialGuid\README.md`

**Context:** Two updates: (1) sharpen the existing "Zero allocations on modern .NET" Highlights bullet to claim "every generation path" (now true after Task 2); (2) show the new ergonomic extensions in a snippet.

- [ ] **Step 1: Open `src/SequentialGuid/README.md` and find the existing "Zero allocations" Highlights bullet**

The current bullet says something like "**Zero allocations on modern .NET** — `stackalloc`, `Span<T>`, and `[SkipLocalsInit]` eliminate heap allocations on the hot path (.NET 8+)". Locate the exact wording in the Highlights section.

- [ ] **Step 2: Update the bullet to claim coverage on every generation path**

Change the wording to:

```markdown
- **Zero allocations on modern .NET** — `stackalloc`, `Span<T>`, and `[SkipLocalsInit]` eliminate heap allocations on **every** generation path (.NET 6+), including the deterministic v5/v8 name-based generators
```

(Adjust to fit the existing surrounding bullet style; keep the bold pattern consistent.)

- [ ] **Step 3: Add an example showing the new ergonomic extensions**

Find the existing "Extract the timestamp from any time-based UUID" section (around the `.ToDateTime()` example). After that block, add a new snippet showing the `TryToDateTime` / `ToDateTimeOffset` / `IsSequentialGuid` / `Guid.MaxValue` usage:

```markdown
### Helpers for common timestamp & predicate scenarios

```csharp
// Try-pattern variant — useful when you want to branch without nullable handling
if (id.TryToDateTime(out var created))
{
    Console.WriteLine($"Created at {created:O}");
}

// DateTimeOffset variant — explicit UTC offset
DateTimeOffset? created = id.ToDateTimeOffset();

// Predicate — "is this a recognised sequential GUID at all?"
bool isOurs = someGuid.IsSequentialGuid();

// RFC 9562 §5.10 max UUID constant — useful for range-scan upper bounds
Guid upper = Guid.MaxValue;
```
```

Place this block after the existing extraction example and before the next major section.

- [ ] **Step 4: Verify the README still packs cleanly**

```powershell
dotnet pack src/SequentialGuid/SequentialGuid.csproj --no-build
```

Expected: PACK SUCCEEDED.

- [ ] **Step 5: Commit**

```bash
git add src/SequentialGuid/README.md
git commit -m "docs: README — zero-alloc claim covers every generator + show v6.1 helpers"
```

---

## Section 6 — Release

### Task 12: Push branch and open PR

**Files:**
- (no code changes — branch push + PR open only)

- [ ] **Step 1: Run the full test suite one more time as a final sanity check**

```powershell
dotnet build
dotnet test
```

Expected: build clean (0 warnings), all tests pass.

- [ ] **Step 2: Push the branch**

```bash
git push -u origin v6.1/perf-and-ergonomics
```

Expected: branch pushed; GitHub returns the "Create a pull request" link.

- [ ] **Step 3: Open the PR (or output the URL for the user to open manually)**

If `gh` CLI is available:

```bash
gh pr create --base master --title "v6.1: zero-alloc deterministic generators + ergonomic helpers" --body "$(cat <<'EOF'
## Summary

SemVer minor release. No breaking changes.

- **Perf:** `GuidV5.Create(byte[])` and `GuidV8Name.Create(byte[])` drop from ~176 B/call to 0 B/call on .NET 6+. Rewrites `GuidNameBased.Create` to use one-shot `SHA1.HashData` / `SHA256.HashData` with a stackalloc/`ArrayPool` concat buffer.
- **Perf:** `[SkipLocalsInit]` applied to `GuidV4`, `GuidV7`, `GuidV8Time`, `GuidNameBased` generators — skips redundant JIT zero-init of stackalloc buffers.
- **Perf:** `GuidV7.NewGuid()` no-arg inlines unix-ms extraction (saves one struct copy).
- **Feature:** `Guid.MaxValue` static extension property (RFC 9562 §5.10).
- **Feature:** `Guid.TryToDateTime(out DateTime)` — BCL-style Try-pattern.
- **Feature:** `Guid.ToDateTimeOffset()` + `Guid.TryToDateTimeOffset(out DateTimeOffset)`.
- **Feature:** Public `Guid.IsSequentialGuid()` predicate.

## Test plan

- [x] \`dotnet build\` clean on all 5 TFMs (0 warnings under TreatWarningsAsErrors=true)
- [x] \`dotnet test\` — all tests pass (count grows by ~16 from new \`GuidExtensionsTests\`)
- [x] \`NameBenchmarks\` shows \`Allocated: 0 B\` for both \`(byte[])\` overloads
- [x] AOT smoke test (JIT mode locally; AOT publish in CI) exercises new APIs

## References

- Spec: \`docs/superpowers/specs/2026-05-12-sequentialguid-v6.1-design.md\`
- Plan: \`docs/superpowers/plans/2026-05-12-sequentialguid-v6.1.md\`
EOF
)"
```

If `gh` CLI is NOT available, the `git push` output already contains a URL like `https://github.com/buvinghausen/SequentialGuid/pull/new/v6.1/perf-and-ergonomics` — open that in a browser and paste the title and body from above.

- [ ] **Step 4: (No commit) — PR is now open. Wait for CI green + manual review before merging.**

---

## Open questions

None — scope is locked by the spec at
`docs/superpowers/specs/2026-05-12-sequentialguid-v6.1-design.md`.
