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

Three recurring Hangfire jobs keep the loop closed:

- **`sync-telegram`** (daily, 23:00) — reads the last synced message id from the sheet, pulls everything newer from the channel, scores it, and appends `id, date, text, danger` rows.
- **`model-training`** (daily, 00:00) — compares the sheet's row count against `data/sentiment.zip.meta`; if there is new data it retrains, prints AUC / accuracy / F1, and saves the model. `PredictionEnginePool` watches the file and picks up the new model without a restart.
- **`restart-app`** (daily, 01:00) — waits for any still-running job to finish, then stops the app so Docker's `restart: unless-stopped` policy starts a fresh instance.

## What works today

Everything below runs unattended once the service is up.

- ✅ **Real-time Telegram alerts** — stays logged in via [WTelegramClient](https://github.com/wiz0u/WTelegramClient) and reacts to each new post in the channel as it lands, not on a poll timer.
- ✅ **Danger classification** — ML.NET binary classification (`FeaturizeText` → `SdcaLogisticRegression`) over the raw Ukrainian message text, with an 80/20 train/test split and AUC / accuracy / F1 logged on every training run.
- ✅ **Home Assistant trigger** — dangerous messages `POST` to `api/webhook/{WebhookId}` as `{ "messages": [...] }`, which is what my automations subscribe to.
- ✅ **Google Sheet as dataset and labelling UI** — messages arrive pre-labelled by the current model; fixing a cell is all it takes to teach the next one.
- ✅ **Nightly self-retraining** — retrains only when the sheet has grown, and the running service swaps in the new `sentiment.zip` without a restart.
- ✅ **Hangfire dashboard** at `/hangfire` — run either job by hand and read its console output.

## In development

- 🚧 **HTTP API** — Swagger is wired up but there are no endpoints yet, so `/swagger` is currently empty. The intent is to expose scoring (`AlertMessageDto` is the placeholder request shape) so other tools can classify text without going through Telegram.
- 🚧 **ONNX runtime scoring** — `models/sentiment.onnx` is exported but nothing loads it; scoring still runs through the ML.NET `.zip` model.
- 🚧 **Configurable danger threshold** — `PredictionDangerousMessageService.IsDangerous` supports a threshold, but the alert path uses the model's default 0.5 decision. Making it a setting would let me trade false alarms against missed ones.
- 🚧 **Unattended first start** — the initial Telegram login prompts for a code on stdin, so a cold start still needs a human.

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
