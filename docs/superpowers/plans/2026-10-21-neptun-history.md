# Neptun History, Configurable Areas & Telegram/AI Kill Switch — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the service run Neptun-only (one flag kills the Telegram + AI side), read all watched areas from config, persist the full track of every target that triggered an alert in SQLite, and browse that history on a table + map page.

**Architecture:** Everything stays in `src/MissilePredictor.API` as feature folders (CLAUDE.md conventions: one type per file, namespace == folder). Areas move into `NeptunConfig.Areas` (options pattern + validator); `NeptunStreamService` runs one `AreaThreatTracker` per area. A channel-backed recorder decouples the socket loop from EF Core writes; an hourly Hangfire job purges tracks of targets that never alerted. Razor Pages + Leaflet render the UI from a shared query service that also backs JSON endpoints.

**Tech Stack:** .NET 8, EF Core 8 (Sqlite provider, migrations via local `dotnet-ef` tool), Hangfire (in-memory, existing), Razor Pages, Leaflet 1.9.4 (cdnjs), xUnit 2.4.2, Microsoft.AspNetCore.Mvc.Testing 8.0.19.

**Spec:** `docs/superpowers/specs/2026-10-21-neptun-history-design.md` (also inlined below).

## Spec (agreed with the user)
1. **Kill switch** `TelegramAi:Enabled` (default `true` = today's behaviour). `false` turns off the Telegram scraper, the ML model, Google Sheets and the three nightly jobs (`sync-telegram`, `model-training`, `restart-app`); their config sections are then not required. Neptun stream + Home Assistant alerts keep running.
2. **Areas** are a configurable list `Neptun:Areas[] = { Title, From?, Latitude, Longitude, RadiusKm }` bound via the options pattern and validated on start. Default (in `appsettings.json`) is only Lviv `49.8397, 24.0297, 10 km`, `From = "Львову"`, so alert text stays byte-identical: `🚨 {threat.Title} — {area.Title}, {km:F0} км від {area.From}.` (+ ` {ExplanationShort}` when present). A target entering two areas alerts once per area.
3. **History (EF Core + SQLite):** record every positioned target update (full track). One alert row per (area, target) alert, with whether the HA call succeeded. Tracks of targets that never alerted are deleted by a recurring job after a retention window; alerted tracks and alerts are kept.
4. **UI:** `/history` page — filterable, paged alerts table (date range, area, type) + Leaflet map with area circles, alert markers and the selected target's track.

## Global Constraints
- .NET 8 / `net8.0`; EF Core packages pinned to one `8.0.x` version (use the latest 8.0 patch available at implementation time, same for `dotnet-ef`).
- One top-level type per file; namespace == folder path; file-scoped namespaces (EF-generated migration files exempt).
- Config keys exactly: `TelegramAi:Enabled`, `Neptun:Areas`, `History:ConnectionString` (default `Data Source=data/history.db`), `History:UnalertedTrackRetention` (default `06:00:00`).
- Recurring job id `history-cleanup`, `Cron.Hourly()`; existing job ids/crons unchanged.
- Timestamps stored as UTC `DateTime` (EF SQLite cannot order/compare `DateTimeOffset`); convert `NeptunThreat.UpdatedAt.UtcDateTime`.
- Alert text format above must not change for the default config (the real-HA integration test asserts it).
- **Never run `tests/MissilePredictor.IntegrationTests`** (fires the real alarm; hook blocks it). Verify with `dotnet test tests/MissilePredictor.Tests` only. Any test that boots the app must set `HomeAssistant:Mock=true` and assert `IAlertClient is ConsoleAlertClient` before feeding threats.
- Start from a branch that contains the still-uncommitted src/tests refactor (ask the user to commit it, or commit it as `refactor: src/tests layout` with their OK, before Task 1).

## Review Focus
1. History DB write fails (locked file, disk full) → stream and HA alerts keep working; writer logs and continues. → Task 4 `Writer_WhenSaveFails_KeepsRunningAndWritesLaterBatches`.
2. `data/` directory missing on a fresh machine/container → created before migrating. → Task 3 `Migrate_CreatesMissingDataDirectory`.
3. Bad area config (empty list, radius ≤ 0, lat out of range, blank/duplicate title) → app refuses to start with a message naming the area. → Task 2 validator tests.
4. Reconnect snapshot re-sends every target → no duplicate alert rows and no duplicate identical track points. → Task 4 `Recorder_SkipsUnchangedPosition`.
5. Empty history / unknown threat id → page renders an empty state; track endpoint returns `200 []`. → Task 6 `TrackEndpoint_UnknownThreat_ReturnsEmptyArray`, Task 7 `HistoryPage_WithNoData_RendersEmptyState`.

---

### Task 0: Save spec and plan into the repo
- [ ] Copy the Spec section to `docs/superpowers/specs/2026-10-21-neptun-history-design.md` and this whole plan to `docs/superpowers/plans/2026-10-21-neptun-history.md`.
- [ ] Commit: `git add docs/superpowers && git commit -m "docs: neptun history plan"`

### Task 1: `TelegramAi:Enabled` kill switch + offline app-boot harness

**Files:**
- Create: `src/MissilePredictor.API/Telegram/TelegramAiSwitch.cs`
- Modify: `src/MissilePredictor.API/Program.cs`, `src/MissilePredictor.API/Extensions/JobsWebApplicationExtensions.cs`, `docker-compose.yaml`, `env.exmaple`
- Modify: `tests/MissilePredictor.Tests/MissilePredictor.Tests.csproj` (add `Microsoft.AspNetCore.Mvc.Testing` 8.0.19)
- Create: `tests/MissilePredictor.Tests/App/OfflineAppFactory.cs`, `tests/MissilePredictor.Tests/App/AppBootTests.cs`

**Interfaces:**
- Produces: `public static class TelegramAiSwitch { public const string Key = "TelegramAi:Enabled"; public static bool IsEnabled(IConfiguration c) => c.GetValue(Key, true); }`
- Produces: `JobsWebApplicationExtensions.UseJobs(this WebApplication app, bool telegramAiEnabled)`
- Produces: `OfflineAppFactory : WebApplicationFactory<Program>`; ctor `(string neptunUrl)`; sets `HomeAssistant:Mock=true`, `Telegram:Mock=true`, `TelegramAi:Enabled=false`, `Neptun:StreamUrl=neptunUrl`, and (from Task 3 on) `History:ConnectionString` to a per-instance temp file deleted on dispose.

- [ ] **Step 1: Write failing tests** in `AppBootTests` (each starts `FakeNeptunServer`, creates `OfflineAppFactory`, resolves services):
  - `Disabled_StartsWithoutTelegramOrAiServices`: `Assert.IsType<ConsoleAlertClient>(sp.GetRequiredService<IAlertClient>())`; `Assert.Null(sp.GetService<ITgScraperService>())`; `Assert.Null(sp.GetService<PredictionDangerousMessageService>())`; `Assert.Null(sp.GetService<GoogleSheetsClient>())`.
  - `Disabled_DoesNotScheduleTelegramJobs`: via `JobStorage.Current.GetConnection().GetRecurringJobs()` ids contain none of `sync-telegram`, `model-training`, `restart-app`.
- [ ] **Step 2:** `dotnet test tests/MissilePredictor.Tests --filter AppBootTests` → FAIL (Telegram services registered / jobs present).
- [ ] **Step 3: Implement.** `Program.cs`: `var telegramAi = TelegramAiSwitch.IsEnabled(builder.Configuration);` only then chain `AddTelegramScraper/AddSentimentModel/AddGoogleSheets`; always `AddAlerts…AddNeptunStream…AddJobs`. After `Build()`, if disabled log `app.Logger.LogWarning("Telegram/AI pipeline disabled ({Key}=false): no Telegram scraper, ML model, Google Sheets or nightly jobs.", TelegramAiSwitch.Key)`. `UseJobs(telegramAi)`: register the three jobs only when enabled, else `RecurringJob.RemoveIfExists(id)` for each. Compose: `- TelegramAi__Enabled=${TELEGRAM_AI_ENABLED:-true}`; `env.exmaple`: `TELEGRAM_AI_ENABLED=true` with a one-line comment.
- [ ] **Step 4:** `dotnet test tests/MissilePredictor.Tests` → all PASS (10 existing + 2).
- [ ] **Step 5:** Commit `feat: TelegramAi:Enabled switch to run Neptun-only`.

### Task 2: Areas from configuration (options pattern), multi-area stream

**Files:**
- Create: `src/MissilePredictor.API/Areas/AreaOptions.cs`, `src/MissilePredictor.API/Neptun/NeptunConfigValidator.cs`, `tests/MissilePredictor.TestKit/TestAreas.cs`, `tests/MissilePredictor.Tests/Neptun/NeptunConfigValidationTests.cs`
- Modify: `Areas/Area.cs` (add `From`, remove `Area.Lviv`), `Neptun/NeptunConfig.cs`, `Neptun/NeptunStreamService.cs`, `Extensions/NeptunServiceCollectionExtensions.cs`, `appsettings.json`, `tests/MissilePredictor.Tests/Areas/AreaThreatTrackerTests.cs`, `tests/MissilePredictor.Tests/Neptun/NeptunStreamServiceTests.cs`, `README.md` ("watched area" paragraph → config example)

**Interfaces:**
- Produces: `public sealed record Area(string Title, double Latitude, double Longitude, double RadiusKm, string From)` (haversine members unchanged).
- Produces: `AreaOptions { [Required] string Title; string? From; [Range(-90,90)] double Latitude; [Range(-180,180)] double Longitude; [Range(0.1, 1000)] double RadiusKm; Area ToArea() /* From ?? Title */ }`
- Produces: `NeptunConfig.Areas : List<AreaOptions>` initialised to `[]` (default lives in appsettings only).
- Produces: `NeptunConfigValidator : IValidateOptions<NeptunConfig>` — fails if `Areas` empty, any item fails `Validator.TryValidateObject(validateAllProperties: true)` (message prefixed `Neptun:Areas[i] ({Title})`), or titles duplicate (case-insensitive).
- Produces: DI registration `IReadOnlyList<AreaThreatTracker>` built from `IOptions<NeptunConfig>` (one tracker per area).
- Produces: `NeptunStreamService(IOptions<NeptunConfig>, IReadOnlyList<AreaThreatTracker>, IAlertClient, ILogger<NeptunStreamService>)`; per frame, every tracker sees the same snapshot/upsert/remove; entered pairs `(Area, NeptunThreat)` go out in one `TriggerAlertAutomationAsync` call; `FormatAlert(Area, NeptunThreat)` uses `area.Title` and `area.From`.
- Produces (TestKit): `public static class TestAreas { public static readonly Area Lviv = new("Львів", 49.8397, 24.0297, 10, "Львову"); public static readonly Area Kyiv = new("Київ", 50.4501, 30.5234, 10, "Києва"); }`

- [ ] **Step 1: Write failing tests.**
  - `NeptunConfigValidationTests` (bind via `ConfigurationBuilder().AddInMemoryCollection` + `services.AddOptions<NeptunConfig>().Bind(...)` + validator; resolve `.Value` and catch `OptionsValidationException`): `Binds_ListOfAreas` (2 areas → `Areas.Count == 2`, Kyiv radius 15), `Fails_WhenNoAreas`, `Fails_WhenRadiusNotPositive` (message contains `Neptun:Areas[0]`), `Fails_WhenLatitudeOutOfRange`, `Fails_WhenTitlesDuplicate`, `From_DefaultsToTitle`.
  - `AreaThreatTrackerTests`: replace `Area.Lviv` with `TestAreas.Lviv` (no behaviour change).
  - `NeptunStreamServiceTests`: helper builds `[new AreaThreatTracker(TestAreas.Lviv)]`; add `TargetEnteringLviv_AlertTextMatchesFormat` asserting exactly `"🚨 БпЛА — Львів, 4 км від Львову. trk_1"`; add `TargetInsideTwoAreas_AlertsOncePerArea` using Lviv + a second area `new("Сихів", InsideLat, InsideLon, 3, "Сихова")` → one call with 2 messages, one containing `Львів`, other containing `Сихів`.
- [ ] **Step 2:** `dotnet test tests/MissilePredictor.Tests` → compile errors / FAIL.
- [ ] **Step 3: Implement** the interfaces above. `AddNeptunStream`: bind + `ValidateDataAnnotations` + `services.AddSingleton<IValidateOptions<NeptunConfig>, NeptunConfigValidator>()` + `ValidateOnStart`; tracker list from `IOptions`. Startup log lists every area (`'{Title}' ({Lat}, {Lon}, r={Radius} km)`). `appsettings.json` `"Neptun": { "Areas": [ { "Title": "Львів", "From": "Львову", "Latitude": 49.8397, "Longitude": 24.0297, "RadiusKm": 10 } ] }`.
- [ ] **Step 4:** `dotnet test tests/MissilePredictor.Tests` → PASS; `dotnet build tests/MissilePredictor.IntegrationTests` builds (do not run it).
- [ ] **Step 5:** Commit `feat: configurable Neptun areas via options`.

### Task 3: History schema (EF Core + SQLite) and startup migration

**Files:**
- Modify: `src/MissilePredictor.API/MissilePredictor.API.csproj` (`Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design` with `PrivateAssets=all`), `.gitignore` (`*.db`, `*.db-shm`, `*.db-wal`), `Program.cs`
- Create: `.config/dotnet-tools.json` (`dotnet tool install dotnet-ef --version 8.0.x --create-manifest-if-needed`... run from repo root)
- Create in `src/MissilePredictor.API/History/`: `HistoryOptions.cs`, `NeptunAlert.cs`, `NeptunTrackPoint.cs`, `HistoryDbContext.cs`, `HistoryDbContextDesignTimeFactory.cs`, `HistoryMigrationService.cs`, `Migrations/*` (generated)
- Create: `src/MissilePredictor.API/Extensions/HistoryServiceCollectionExtensions.cs`
- Create: `tests/MissilePredictor.TestKit/History/InMemoryHistoryDb.cs`, `tests/MissilePredictor.Tests/History/HistoryMigrationTests.cs`

**Interfaces:**
- Produces: `HistoryOptions { [Required] string ConnectionString = "Data Source=data/history.db"; TimeSpan UnalertedTrackRetention = 6h }` (section `History`).
- Produces: `NeptunAlert { long Id; DateTime OccurredAt; string AreaTitle; string ThreatId; string Type; string Title; string? Region; string? Locality; double Lat; double Lon; double DistanceKm; string? ExplanationShort; bool AlertSent; string? Error }` — indexes `OccurredAt`, `ThreatId`.
- Produces: `NeptunTrackPoint { long Id; string ThreatId; DateTime ReceivedAt; DateTime UpdatedAt; string Type; string Title; string Status; double Lat; double Lon; double? Heading }` — index `(ThreatId, ReceivedAt)`.
- Produces: `HistoryDbContext(DbContextOptions<HistoryDbContext>)` with `DbSet<NeptunAlert> Alerts`, `DbSet<NeptunTrackPoint> TrackPoints` (tables `NeptunAlerts`, `NeptunTrackPoints`).
- Produces: `HistoryMigrationService : IHostedService` — creates the directory of `SqliteConnectionStringBuilder(...).DataSource` if missing, then `Database.MigrateAsync()`.
- Produces: `AddHistory(this IServiceCollection, IConfiguration)` — options (validated on start), `AddDbContextFactory<HistoryDbContext>` (Sqlite from options), `AddHostedService<HistoryMigrationService>()`. Program registers `AddHistory` **before** `AddNeptunStream` (hosted services start in registration order).
- Produces (TestKit): `InMemoryHistoryDb : IDbContextFactory<HistoryDbContext>, IDisposable` — keeps an open `SqliteConnection("DataSource=:memory:")`, runs `Database.Migrate()` in ctor.

- [ ] **Step 1: Write failing tests** `HistoryMigrationTests`: `Migrate_CreatesTables` (InMemoryHistoryDb; add one `NeptunAlert` and one `NeptunTrackPoint`, read back, fields round-trip incl. `DateTime` kind UTC value); `Migrate_CreatesMissingDataDirectory` (temp dir + `sub/history.db`, run `HistoryMigrationService.StartAsync`, assert file exists). Extend `OfflineAppFactory` to set `History:ConnectionString` to a temp file; add `AppBootTests.Boot_CreatesHistoryDatabase` (file exists after host start).
- [ ] **Step 2:** run → FAIL (types missing).
- [ ] **Step 3: Implement** entities, context (`OnModelCreating` indexes, table names), design-time factory (default connection string), migration service, `AddHistory`; then from repo root `dotnet tool run dotnet-ef migrations add InitialHistory --project src/MissilePredictor.API --output-dir History/Migrations`.
- [ ] **Step 4:** `dotnet test tests/MissilePredictor.Tests` → PASS.
- [ ] **Step 5:** Commit `feat: SQLite history schema with startup migration`.

### Task 4: Record tracks and alerts from the Neptun stream

**Files:**
- Create in `src/MissilePredictor.API/History/`: `INeptunHistoryRecorder.cs`, `NeptunHistoryRecorder.cs`, `HistoryWriterService.cs`
- Modify: `Neptun/NeptunStreamService.cs`, `Extensions/HistoryServiceCollectionExtensions.cs`
- Create: `tests/MissilePredictor.TestKit/Fakes/RecordingHistoryRecorder.cs`, `tests/MissilePredictor.Tests/History/NeptunHistoryRecorderTests.cs`, `tests/MissilePredictor.Tests/History/HistoryWriterServiceTests.cs`
- Modify: `tests/MissilePredictor.Tests/Neptun/NeptunStreamServiceTests.cs`, `AppBootTests.cs`

**Interfaces:**
- Produces: `INeptunHistoryRecorder { void RecordPosition(NeptunThreat threat); void RecordAlert(Area area, NeptunThreat threat, bool alertSent, string? error); void Forget(string threatId); }`
- Produces: `NeptunHistoryRecorder : INeptunHistoryRecorder` — unbounded `Channel<object>` of `NeptunTrackPoint`/`NeptunAlert` entities, exposed as `ChannelReader<object> Pending`; skips `AreaOnly` threats; skips a position whose `(Lat, Lon, Status)` equals the last one recorded for that id; `Forget` drops the remembered last position. Single-reader, not thread-safe (same note as `AreaThreatTracker`). Registered as singleton and forwarded as `INeptunHistoryRecorder`.
- Produces: `HistoryWriterService(NeptunHistoryRecorder, IDbContextFactory<HistoryDbContext>, ILogger<HistoryWriterService>) : BackgroundService` — waits for items, drains up to 500, `AddRange` + `SaveChangesAsync`; on exception logs `LogError` and drops that batch, keeps running; drains what's left on stop.
- Consumes/changes: `NeptunStreamService` ctor gains `INeptunHistoryRecorder history` (before the logger). Every snapshot/upsert threat → `RecordPosition`; `remove` → `Forget`; after the HA call, `RecordAlert(area, threat, alertSent, ex?.Message)` for each entered pair.
- Produces (TestKit): `RecordingHistoryRecorder : INeptunHistoryRecorder` with public `List<NeptunThreat> Positions`, `List<(Area Area, NeptunThreat Threat, bool Sent, string? Error)> Alerts` (thread-safe via lock).

- [ ] **Step 1: Write failing tests.**
  - Recorder: `Recorder_EmitsTrackPointForPositionedThreat`, `Recorder_SkipsAreaOnlyThreat`, `Recorder_SkipsUnchangedPosition` (same threat twice → 1 item; moved → 2; after `Forget` same position → recorded again), `Recorder_EmitsAlertWithDistance` (`DistanceKm` ≈ `TestAreas.Lviv.DistanceKm(...)`, `AreaTitle == "Львів"`).
  - Writer (InMemoryHistoryDb): `Writer_PersistsRecordedItems` (poll ≤5 s until 1 alert + 2 points), `Writer_WhenSaveFails_KeepsRunningAndWritesLaterBatches` (factory wrapper whose first `CreateDbContextAsync` throws).
  - Stream (`RecordingHistoryRecorder`): `Stream_RecordsPositionsForAllThreats` (Kyiv + Lviv upserts → 2 positions), `Stream_RecordsAlertAsSent`, `Stream_RecordsAlertAsFailed_WhenHomeAssistantThrows` (alert client fake that throws → `Sent == false`, `Error` set, stream keeps processing the next frame).
  - `AppBootTests.DroneOverLviv_IsStoredInHistory`: send `Upsert(Threat("trk_boot", InsideLat, InsideLon))`; poll the temp DB until `Alerts.Single().ThreatId == "trk_boot"` and `TrackPoints.Any()`.
- [ ] **Step 2:** run → FAIL.
- [ ] **Step 3: Implement** the interfaces above; register recorder + `AddHostedService<HistoryWriterService>()` in `AddHistory` (after the migration service).
- [ ] **Step 4:** `dotnet test tests/MissilePredictor.Tests` → PASS.
- [ ] **Step 5:** Commit `feat: record Neptun tracks and alerts to SQLite`.

### Task 5: Purge tracks of targets that never alerted

**Files:**
- Create: `src/MissilePredictor.API/Jobs/HistoryCleanupJob.cs`, `tests/MissilePredictor.Tests/Jobs/HistoryCleanupJobTests.cs`
- Modify: `Extensions/JobsWebApplicationExtensions.cs` (always register `history-cleanup`, `Cron.Hourly()`), `AppBootTests.cs`

**Interfaces:**
- Produces: `HistoryCleanupJob(IDbContextFactory<HistoryDbContext>, IOptions<HistoryOptions>, ILogger<HistoryCleanupJob>)` with `Task<int> PurgeAsync(CancellationToken ct = default)` (deletes `TrackPoints` where `ReceivedAt < UtcNow - UnalertedTrackRetention` and no `Alerts` row has the same `ThreatId`, via `ExecuteDeleteAsync`; logs the count) and `Task Execute(PerformContext context)` (Hangfire entry; calls `PurgeAsync`, writes count to console).

- [ ] **Step 1: Write failing tests** (InMemoryHistoryDb, retention 6 h): `Purge_DeletesOldUnalertedPoints` (point at now-7h, no alert → deleted, returns 1), `Purge_KeepsRecentUnalertedPoints` (now-1h → kept), `Purge_KeepsAlertedTrackOfAnyAge` (now-30d point + alert for same id → kept). `AppBootTests.Disabled_DoesNotScheduleTelegramJobs` also asserts `history-cleanup` **is** present.
- [ ] **Step 2:** run → FAIL. **Step 3:** implement. **Step 4:** `dotnet test tests/MissilePredictor.Tests` → PASS.
- [ ] **Step 5:** Commit `feat: hourly purge of tracks without alerts`.

### Task 6: History queries + JSON endpoints

**Files:**
- Create in `src/MissilePredictor.API/History/`: `AlertFilter.cs`, `AlertPage.cs`, `HistoryQueries.cs`, `HistoryEndpoints.cs`
- Create: `src/MissilePredictor.API/Extensions/HistoryWebApplicationExtensions.cs`
- Modify: `Program.cs` (`app.MapHistory()`), `Extensions/HistoryServiceCollectionExtensions.cs` (register `HistoryQueries` singleton)
- Create: `tests/MissilePredictor.Tests/History/HistoryQueriesTests.cs`; Modify `AppBootTests.cs`

**Interfaces:**
- Produces: `record AlertFilter(DateTime? From = null, DateTime? To = null, string? Area = null, string? Type = null, int Page = 1, int PageSize = 50)` (PageSize clamped 1..200, Page ≥ 1).
- Produces: `record AlertPage(IReadOnlyList<NeptunAlert> Items, int Total, int Page, int PageSize)`.
- Produces: `HistoryQueries(IDbContextFactory<HistoryDbContext>)`: `Task<AlertPage> GetAlertsAsync(AlertFilter, CancellationToken)` (newest first, `From` inclusive, `To` exclusive), `Task<IReadOnlyList<NeptunTrackPoint>> GetTrackAsync(string threatId, CancellationToken)` (oldest first), `Task<IReadOnlyList<string>> GetThreatTypesAsync(CancellationToken)` (distinct, sorted).
- Produces: `HistoryEndpoints.MapHistoryApi(this IEndpointRouteBuilder)` → `GET /api/history/alerts` (`[AsParameters] AlertFilter`), `GET /api/history/threats/{threatId}/track`, `GET /api/history/areas` (configured `AreaOptions` list). `MapHistory(this WebApplication)` calls `MapHistoryApi` (Task 7 adds pages).

- [ ] **Step 1: Write failing tests.** `HistoryQueriesTests`: `GetAlerts_FiltersByAreaTypeAndDate`, `GetAlerts_PagesNewestFirst` (60 rows, page 2 size 50 → 10 items, `Total == 60`), `GetTrack_ReturnsPointsInOrder`. `AppBootTests`: `AlertsEndpoint_ReturnsOk`, `TrackEndpoint_UnknownThreat_ReturnsEmptyArray` (body `[]`), `AreasEndpoint_ReturnsConfiguredLviv`.
- [ ] **Step 2:** FAIL. **Step 3:** implement. **Step 4:** `dotnet test tests/MissilePredictor.Tests` → PASS.
- [ ] **Step 5:** Commit `feat: history query API`.

### Task 7: `/history` page (table + Leaflet map) and docs

**Files:**
- Create: `src/MissilePredictor.API/Pages/_ViewImports.cshtml`, `src/MissilePredictor.API/Pages/History/Index.cshtml`, `src/MissilePredictor.API/Pages/History/Index.cshtml.cs`
- Modify: `Extensions/HistoryServiceCollectionExtensions.cs` (`AddRazorPages()`), `Extensions/HistoryWebApplicationExtensions.cs` (`MapRazorPages()`), `README.md` (History + kill switch + areas sections), `CLAUDE.md` (layout: `History/`, `Pages/`)
- Modify: `AppBootTests.cs`

**Interfaces:**
- Produces: `IndexModel : PageModel` (namespace `MissilePredictor.API.Pages.History`), ctor `(HistoryQueries, IOptions<NeptunConfig>)`, `[BindProperty(SupportsGet = true)]` `From`, `To`, `Area`, `Type`, `PageNumber`; exposes `AlertPage Alerts`, `IReadOnlyList<AreaOptions> Areas`, `IReadOnlyList<string> Types`; `OnGetAsync(CancellationToken)`.
- Page (standalone HTML, `Layout = null`): filter form (GET), table (time, area, type, title, region/locality, distance km, explanation, HA sent ✓/✗ with error tooltip), prev/next paging, empty state text `Ще немає тривог.`; Leaflet 1.9.4 from cdnjs (exact version pinned), circles for `Areas`, markers for the alerts on the current page; clicking a row/marker fetches `/api/history/threats/{id}/track` and draws a polyline. Times rendered client-side from UTC ISO with `toLocaleString('uk-UA')`.

- [ ] **Step 1: Write failing tests** in `AppBootTests`: `HistoryPage_WithNoData_RendersEmptyState` (GET `/history` → 200, contains `Ще немає тривог.` and `leaflet`), `HistoryPage_ShowsStoredAlert` (after the Lviv drone is stored, page contains `trk_boot`'s title and `Львів`).
- [ ] **Step 2:** FAIL. **Step 3:** implement page + docs. **Step 4:** `dotnet test tests/MissilePredictor.Tests` → PASS; `dotnet build MissilePredictor.sln` no new warnings.
- [ ] **Step 5: Manual smoke** (offline): `TelegramAi__Enabled=false HomeAssistant__Mock=true dotnet run --project src/MissilePredictor.API` → log shows the disabled warning and the Lviv area; `src/MissilePredictor.API/data/history.db` exists; open `http://localhost:5102/history` (map with Lviv circle) and `/hangfire` (only `history-cleanup`). `docker build .` succeeds.
- [ ] **Step 6:** Commit `feat: history page with table and map`.

---

## Execution recommendation
Subagent-driven: 7 sequential tasks whose interfaces chain tightly (stream ctor changes in Tasks 2 and 4, `AddHistory` grows across 3–7), and a shipped mistake here can silence or spam a real home alarm, so a fresh reviewer per task is worth the cost.

## Calls I made (flag if wrong)
- Flag name `TelegramAi:Enabled`, default `true`; it also stops the daily `restart-app` job.
- `From` field per area keeps the Ukrainian "км від Львову" wording.
- Un-alerted tracks kept 6 h, purge hourly; alerts and alerted tracks never deleted.
- `/history` has no auth (same as `/hangfire` today); history lives in the API project.
