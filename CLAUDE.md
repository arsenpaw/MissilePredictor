# MissilePredictor

## Layout
- `src/MissilePredictor.API` — ASP.NET Core host: Telegram scraper, Neptun stream, alerts, Hangfire jobs.
  Feature folders (`Alerts`, `Areas`, `Neptun`, `Telegram`, `Jobs`, `History`), DI wiring in `Extensions/`.
  `History/` is the SQLite (EF Core) record of Neptun tracks and alerts plus its JSON API; `Pages/` holds the Razor
  Pages UI (`/history` table + Leaflet map).
- `src/MissilePredictor.AI` — ML.NET sentiment model (`Sentiment/`) and Google Sheets client (`GoogleSheets/`).
- `tests/MissilePredictor.Tests` — offline unit/component tests.
- `tests/MissilePredictor.TestKit` — shared fakes (fake Neptun WebSocket server, recording alert client).
- `tests/MissilePredictor.IntegrationTests` — tests against the **real** Home Assistant.

## Conventions
- One top-level type per file; file name == type name.
- Namespace == folder path (`MissilePredictor.API.Alerts.HomeAssistant`, ...), file-scoped namespaces.

## Tests
- Run unit tests with `dotnet test tests/MissilePredictor.Tests`.
- **Never run `tests/MissilePredictor.IntegrationTests`.** They fire the real home air-raid alarm.
  Only the user runs them. A PreToolUse hook (`.claude/hooks/block-integration-tests.sh`) denies them,
  including bare or solution-wide `dotnet test`, and the tests skip themselves when `CLAUDECODE=1`.
