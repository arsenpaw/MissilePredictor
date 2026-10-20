# Neptun history, configurable areas & Telegram/AI kill switch — design

Agreed 2026-10-21. Implementation plan: `docs/superpowers/plans/2026-10-21-neptun-history.md`.

## Requirements
1. **Kill switch** `TelegramAi:Enabled` (default `true` = today's behaviour). `false` turns off the Telegram scraper, the ML model, Google Sheets and the three nightly jobs (`sync-telegram`, `model-training`, `restart-app`); their config sections are then not required. Neptun stream + Home Assistant alerts keep running.
2. **Areas** are a configurable list `Neptun:Areas[] = { Title, From?, Latitude, Longitude, RadiusKm }` bound via the options pattern and validated on start. Default (in `appsettings.json`) is only Lviv `49.8397, 24.0297, 10 km`, `From = "Львову"`, so alert text stays byte-identical: `🚨 {threat.Title} — {area.Title}, {km:F0} км від {area.From}.` (+ ` {ExplanationShort}` when present). A target entering two areas alerts once per area.
3. **History (EF Core + SQLite):** record every positioned target update (full track). One alert row per (area, target) alert, with whether the HA call succeeded. Tracks of targets that never alerted are deleted by a recurring job after a retention window; alerted tracks and alerts are kept.
4. **UI:** `/history` page — filterable, paged alerts table (date range, area, type) + Leaflet map with area circles, alert markers and the selected target's track.

## Calls made by Claude (user may revise)
- Flag name `TelegramAi:Enabled`, default `true`; it also stops the daily `restart-app` job.
- `From` field per area keeps the Ukrainian "км від Львову" wording.
- Un-alerted tracks kept 6 h, purge hourly; alerts and alerted tracks never deleted.
- `/history` has no auth (same as `/hangfire` today); history lives in the API project.
