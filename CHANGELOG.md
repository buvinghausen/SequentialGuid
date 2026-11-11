# Changelog

All notable changes to the SequentialGuid packages (`SequentialGuid`, `SequentialGuid.EntityFrameworkCore`,
`SequentialGuid.MongoDB`, `SequentialGuid.NodaTime`) are documented here. All four packages share one version number.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). The package version comes from the GitHub release tag;
see each [GitHub release](https://github.com/buvinghausen/SequentialGuid/releases) for full notes and benchmarks.

## [Unreleased]

Planned as **7.0.0**: removing a target framework is a breaking change.

### Removed
- **`net462` target.** .NET Framework 4.6.2 reaches end of support in January 2027. .NET Framework apps now get the
  `netstandard2.0` build. **.NET Framework 4.7.2+** is the tested, supported floor.

### Changed
- On .NET Framework, name-based UUIDs (`GuidV5`, `GuidV8Name`) now hash with `IncrementalHash`, the same path as
  `netstandard2.0`. Generated values are byte-for-byte identical; the RFC 9562 test vectors pass on every target.
- The `netstandard2.0` build is now covered by the full test suite (run on .NET Framework 4.7.2). Previously that build
  shipped untested.

### Using 7.x on .NET Framework 4.6.2–4.7.1
These versions resolve the `netstandard2.0` build, which depends on .NET Standard facade assemblies (`netstandard.dll`,
`System.Runtime.dll`, …) copied next to your app. Those facades need **binding redirects**:

- **SDK-style or `PackageReference` projects (console, WinForms, WPF, services):** redirects are generated
  automatically. Make sure you haven't turned off `<AutoGenerateBindingRedirects>`.
- **Classic ASP.NET / web projects:** add this to the project file so the redirects go into `web.config`:
  ```xml
  <PropertyGroup>
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
    <GenerateBindingRedirectsOutputType>true</GenerateBindingRedirectsOutputType>
  </PropertyGroup>
  ```
- **`packages.config` projects:** run `Add-BindingRedirect` in the Package Manager Console after upgrading, or migrate
  to `PackageReference`.

A `FileLoadException` or `FileNotFoundException` for `netstandard`, `System.Runtime` or a `System.*` facade at startup
almost always means a redirect is missing. .NET Framework 4.7.2+ has these facades built in and needs none of this.
Once your redirects are set up for 7.0, later 7.x releases won't need any new ones: the target frameworks and public
API are expected to stay the same across the major version.

## [6.2.0] - 2026-06-10

### Added
- Bulk generation: `GuidV7.Fill(Span<Guid>)` / `FillSql` / `NewGuids(int)` / `NewSqlGuids(int)`, plus the `GuidV8Time`
  equivalents. Each claims one counter block, one timestamp, and one RNG call for the whole batch. Batches that would
  exceed the counter space throw `ArgumentOutOfRangeException`.
- `TimeProvider` overloads on every generation path, single-call and bulk (.NET 8+).
- EF Core value generation: `UseSequentialGuidValueGeneration()` (optionally `sqlServerByteOrder: true`) and four
  public value generators for explicit `HasValueGenerator<T>()` wiring.
- MongoDB: `new MongoSequentialGuidGenerator(SequentialGuidType.Rfc9562V7)` emits v7 ids. `Instance` still emits v8.

### Fixed
- The legacy-TFM `GetInt32` counter-seeding helper now uses unbiased mask-and-reject sampling.

## [6.1.0] - 2026-05-13

### Added
- `net11.0` target.
- `Guid.MaxValue`, `Guid.IsSequentialGuid()`, `Guid.TryToDateTime(out DateTime)`, `Guid.ToDateTimeOffset()`, and
  `Guid.TryToDateTimeOffset(out DateTimeOffset)`.

### Changed
- Zero allocations on every generator and conversion path, including the `string` overloads and `GuidNameBased.Create`
  (net6.0+).
- `[SkipLocalsInit]` on every generator hot path.

## [6.0.0] - 2026-05-12

### Removed
- **Breaking:** the obsolete `SequentialGuidGenerator` / `SequentialSqlGuidGenerator` classes (use `GuidV8Time`), the
  matching NodaTime shim extensions, and the `GuidV7.Timestamp` / `GuidV8Time.Timestamp` static properties (use
  `DateTime.UtcNow`).

### Added
- Native AOT compatibility (`IsAotCompatible`) on all packages, verified by a published AOT smoke test in CI.

### Changed
- Zero-allocation generation on .NET 8+ for `GuidV4`, `GuidV7`, and `GuidV8Time` (`stackalloc` on the hot path).
- `SequentialGuid` / `SequentialSqlGuid` timestamp validation tolerates up to 1 s of forward clock skew.
- `GuidV8Time` machine fingerprint hash switched from SHA-512 to SHA-256.

## [5.0.7] - 2026-03-21

### Added
- `ISequentialGuid<TSelf>`, plus BCL interfaces on `SequentialGuid` / `SequentialSqlGuid` (`IComparable`,
  `IEquatable`, `IFormattable`, `ISpanFormattable`, `ISpanParsable`, `IUtf8SpanFormattable`, `IUtf8SpanParsable` as
  the TFM allows), comparison operators, and implicit conversions to and from `Guid` and `string`.

## [5.0.6] - 2026-03-18

### Fixed
- `ToString()` on the structs returns the `Guid` string instead of the record-struct default.

## [5.0.5] - 2026-03-17

### Added
- System.Text.Json support: `new JsonSerializerOptions().AddSequentialGuidConverters()`.

## [5.0.4] - 2026-03-17

### Added
- EF Core value converter and wire-up.

### Changed
- Byte-order lookup checks all six modes.

## [5.0.3] - 2026-03-13

### Added
- `SequentialGuid` and `SequentialSqlGuid` structs.

### Fixed
- Timestamp extraction now returns the correct timestamp for every previously generated GUID; some flags previously
  short-circuited to a wrong value.

## [5.0.2] - 2026-03-09

### Added
- Full RFC 9562 compliance: `GuidV4`, `GuidV5`, `GuidV7` (26-bit monotonic counter, §6.2 Method 1), `GuidV8Time`, and
  `GuidV8Name`, with a backwards-compatible sort order.

## [4.0.6] - 2025-02-19

### Added
- .NET 9 support.

### Fixed
- XML docs.

## [4.0.5] - 2024-03-20

### Added
- .NET 8 target.

## [4.0.4] - 2023-02-27

### Removed
- `System.Net.Http` reference on legacy .NET.

## [4.0.3] - 2022-10-25

### Changed
- Back to publishing symbols to a symbol server.

## [4.0.2] - 2022-10-20

### Changed
- Use `RandomNumberGenerator` on .NET Standard 2.1 and above.

## [4.0.1] - 2022-10-10

### Changed
- `ToGuid` / `ToSqlGuid` use array lookups instead of dictionaries, removing the static constructor.

## [4.0.0] - 2022-10-05

### Added
- .NET 7 support; `netstandard` targets restored.

### Changed
- **Breaking:** the `ToDateTime` extensions moved to the `System` namespace.

## [3.0.1] - 2022-02-22

### Added
- Browser (Blazor WebAssembly) support. The MD5 machine-name hash was replaced, since MD5 isn't available in the browser.

## [3.0.0] - 2021-09-15

### Changed
- **Breaking:** dropped `netstandard` targets in favour of runtime-specific TFMs; uses `Environment.ProcessId` on
  .NET 5+.

## [2.5.0] - 2020-11-18

### Added
- `SequentialGuid.MongoDB` and `SequentialGuid.NodaTime` packages.

## [2.4.2] - 2020-07-31

### Changed
- Releases publish to NuGet automatically. No code changes.

[Unreleased]: https://github.com/buvinghausen/SequentialGuid/compare/6.2.0...HEAD
[6.2.0]: https://github.com/buvinghausen/SequentialGuid/compare/6.1.0...6.2.0
[6.1.0]: https://github.com/buvinghausen/SequentialGuid/compare/6.0.0...6.1.0
[6.0.0]: https://github.com/buvinghausen/SequentialGuid/compare/5.0.7...6.0.0
[5.0.7]: https://github.com/buvinghausen/SequentialGuid/compare/5.0.6...5.0.7
[5.0.6]: https://github.com/buvinghausen/SequentialGuid/compare/5.0.5...5.0.6
[5.0.5]: https://github.com/buvinghausen/SequentialGuid/compare/5.0.4...5.0.5
[5.0.4]: https://github.com/buvinghausen/SequentialGuid/compare/5.0.3...5.0.4
[5.0.3]: https://github.com/buvinghausen/SequentialGuid/compare/5.0.2...5.0.3
[5.0.2]: https://github.com/buvinghausen/SequentialGuid/compare/4.0.6...5.0.2
[4.0.6]: https://github.com/buvinghausen/SequentialGuid/compare/4.0.5...4.0.6
[4.0.5]: https://github.com/buvinghausen/SequentialGuid/compare/4.0.4...4.0.5
[4.0.4]: https://github.com/buvinghausen/SequentialGuid/compare/4.0.3...4.0.4
[4.0.3]: https://github.com/buvinghausen/SequentialGuid/compare/4.0.2...4.0.3
[4.0.2]: https://github.com/buvinghausen/SequentialGuid/compare/4.0.1...4.0.2
[4.0.1]: https://github.com/buvinghausen/SequentialGuid/compare/4.0.0...4.0.1
[4.0.0]: https://github.com/buvinghausen/SequentialGuid/compare/3.0.1...4.0.0
[3.0.1]: https://github.com/buvinghausen/SequentialGuid/compare/3.0.0...3.0.1
[3.0.0]: https://github.com/buvinghausen/SequentialGuid/compare/2.5.0...3.0.0
[2.5.0]: https://github.com/buvinghausen/SequentialGuid/compare/2.4.2...2.5.0
[2.4.2]: https://github.com/buvinghausen/SequentialGuid/releases/tag/2.4.2
