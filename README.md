# Missile Predictor

A self-hosted .NET service that listens to the Ukrainian Telegram channel **Ракетна Небезпека** ("Ракетна Небезпека" / missile danger), classifies each incoming message as dangerous or not with an ML.NET binary classification model, and fires a Home Assistant webhook when a dangerous message arrives — so my home automations (lights, speakers, notifications) react to a real air-raid threat within seconds of the post.

The model is trained on messages from that channel: every message the scraper sees is stored in a Google Sheet together with the model's own danger label, I correct the labels by hand where needed, and the service retrains itself nightly from the corrected sheet. The sheet is both the dataset and the labelling UI.

## How it works

```
Ракетна Небезпека (Telegram)
        │  real-time updates (WTelegramClient)
        ▼
  TgScraperService ──► PredictionDangerousMessageService ──► Home Assistant webhook
        │                    (ML.NET model)                       (if dangerous)
        │
        │  nightly 23:00 — SyncTelegramDatasToSheetsJob
        ▼
   Google Sheet  ◄── id | date | text | danger (0/1)
        │
        │  nightly 00:00 — ModelTrainingJob (only if the sheet grew)
        ▼
   SentimentModelPipeline ──► data/sentiment.zip  (hot-reloaded, no restart)
```

Two recurring Hangfire jobs keep the loop closed:

- **`sync-telegram`** (daily, 23:00) — reads the last synced message id from the sheet, pulls everything newer from the channel, scores it, and appends `id, date, text, danger` rows.
- **`model-training`** (daily, 00:00) — compares the sheet's row count against `data/sentiment.zip.meta`; if there is new data it retrains, prints AUC / accuracy / F1, and saves the model. `PredictionEnginePool` watches the file and picks up the new model without a restart.

## Features

- **Real-time Telegram ingestion** — a hosted service stays logged in via [WTelegramClient](https://github.com/wiz0u/WTelegramClient) and handles `UpdateNewMessage` for the configured channel, so alerts are not tied to the nightly poll.
- **Binary classification with ML.NET** — `FeaturizeText` over the raw Ukrainian message text into `SdcaLogisticRegression`, an 80/20 train/test split with a fixed seed, and AUC/accuracy/F1 logged on every training run.
- **Google Sheets as dataset + labelling UI** — messages land in the sheet pre-labelled by the current model; correcting a cell is all it takes to teach the next model.
- **Self-retraining** — nightly, incremental, and skipped entirely when no new rows arrived.
- **Home Assistant integration** — dangerous messages `POST` to `api/webhook/{WebhookId}` with a `{ "messages": [...] }` body, which is what my automations subscribe to.
- **Hot model reload** — new `sentiment.zip` is picked up in place.
- **Hangfire dashboard** at `/hangfire` for triggering jobs by hand and reading their console output; Swagger UI at `/swagger`.

## Projects

| Project | Contents |
| --- | --- |
| `MissilePredictor.AI` | ML.NET training pipeline and trainer, prediction service, Google Sheets client, ML/Google config |
| `MissilePredictor.API` | ASP.NET Core host, Telegram scraper, Home Assistant client, Hangfire jobs |
| `models/` | Exported model artifacts (`sentiment.zip`, `sentiment.onnx`) |

## Configuration

Settings bind from `appsettings.json` or environment variables (double-underscore notation, e.g. `Telegram__ApiId`).

| Section | Keys |
| --- | --- |
| `Telegram` | `ApiId`, `ApiHash`, `Channel`, `PhoneNumber`, `SessionPath` (default `data/tg.session`) |
| `Google` | `SpreadsheetId`, `Range`, `CredentialsFilePath` (service account JSON) |
| `HomeAssistant` | `BaseUrl`, `Token`, `WebhookId` (default `missile_predictor_alert`) |
| `ML` | `ModelPath` (default `data/sentiment.zip`) |

Get `ApiId` / `ApiHash` from [my.telegram.org](https://my.telegram.org). The Home Assistant token is a long-lived access token from your user profile. The Google service account needs edit access to the spreadsheet.

## Running

```bash
cp env.exmaple .env     # then fill it in
docker compose up --build
```

The compose file mounts a host directory at `/app/data` — that is where the Telegram session, `credentials.json`, and the trained model live, so they survive rebuilds. Put your Google service account JSON there as `credentials.json` before the first run.

Or locally:

```bash
dotnet run --project MissilePredictor.API
```

The **first run is interactive**: Telegram asks for the verification code (and 2FA password, if set) on stdin. After that the session file is reused, so run it attached the first time.

## Home Assistant side

Create an automation with a webhook trigger whose id matches `HomeAssistant__WebhookId`:

```yaml
automation:
  - alias: Missile alert
    trigger:
      - platform: webhook
        webhook_id: missile_predictor_alert
    action:
      - service: notify.mobile_app_phone
        data:
          title: "🚨 Ракетна небезпека"
          message: "{{ trigger.json.messages | join('\n') }}"
```
