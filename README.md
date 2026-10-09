# FB Outlet Conditions Housekeeping

A timer-triggered Azure Function (.NET 10, isolated worker) that automatically
deletes aged SharePoint "Report Log" entries and their referenced Cloudinary
images — deleting every Cloudinary image **first**, and removing the
SharePoint list item only once all of its images are confirmed deleted.

Runs daily at 02:30, supports a safe `DryRun` mode, and sends an HTML
summary/failure email after every run.

---

## Why this exists

Report Log entries and the Cloudinary images attached to them accumulate
indefinitely in SharePoint and Cloudinary, with no existing automated
cleanup process. Manual cleanup risks leaving orphaned Cloudinary images
behind if a SharePoint item is deleted before its images are. This function
automates that cleanup with a strict, enforced deletion order and a
dry-run safety net.

See `docs/SPECIFICATION.md` for the full business problem, requirements,
and acceptance criteria, or `docs/EXECUTIVE-SUMMARY.docx` for a
management-level summary.

---

## How it works

```text
Timer fires (02:30 daily)
        │
        ▼
Identify Report Log entries older than EntryAgeDays
        │
        ▼
For each eligible entry:
    delete every referenced Cloudinary image
        │  (only if ALL images deleted successfully)
        ▼
    delete the SharePoint list item
        │
        ▼
Send summary email (counts, failures, dry-run status)
```

A run-level failure (e.g. SharePoint/Graph authentication failure) aborts
the whole run and sends a separate failure email; a single entry's failure
(e.g. one bad image URL) is recorded and skipped without affecting the
rest of the run.

---

## Project layout

```text
FBOutletConditionsHousekeeping.slnx
├── src/
│   ├── FBOutletConditionsHousekeeping.Core/        # Business logic — no Azure Functions dependency
│   │   ├── Configuration/                          # Options classes + startup validators
│   │   ├── Models/                                 # ReportLogEntry, CleanupSummary, etc.
│   │   └── Services/                                # SharePoint/Cloudinary/Email service interfaces + implementations, CleanupOrchestrator
│   └── FBOutletConditionsHousekeeping.Functions/    # Azure Functions isolated-worker host (timer trigger + DI wiring)
├── tests/
│   └── FBOutletConditionsHousekeeping.Core.Tests/   # xUnit + Moq unit tests (25 tests, all mocked — no live services required)
├── .github/workflows/deploy-function-app.yml        # CI/CD: build → test → deploy (Azure OIDC, no stored secrets)
└── docs/
    ├── ARCHITECTURE.md        # Technology stack, architecture decisions, diagrams
    ├── SPECIFICATION.md       # Requirements, user stories, acceptance criteria, test summary
    ├── SETUP-GUIDE.md         # Full configuration reference + Azure Portal / GitHub Actions deployment walkthroughs
    ├── SCHEMA.md               # SharePoint list field mapping, Cloudinary public_id derivation
    └── EXECUTIVE-SUMMARY.docx # Management-level summary
```

`Core` has no dependency on the Azure Functions runtime, so the entire
cleanup/ordering logic is unit-testable with mocked service interfaces —
no live SharePoint, Cloudinary, or SMTP connection is needed to run the
test suite.

---

## Configuration

All behavior is controlled by configuration — nothing is hardcoded. Key
settings (full reference in `docs/SETUP-GUIDE.md` Section 6):

| Setting                                   | Purpose                                                                      |
| --------------------------------------------| ---------------------------------------------------------------------------- |
| `Cleanup:EntryAgeDays`                     | How old (days) an entry must be before it's eligible for deletion.          |
| `Cleanup:DryRun`                           | When `true` (the default), evaluates and logs/emails without deleting anything. |
| `Cleanup:TimerSchedule`                    | NCRONTAB schedule for the timer trigger (default `0 30 2 * * *` = 02:30 daily). |
| `SharePoint:TenantId` / `ClientId` / `ClientSecret` | Azure AD App Registration used for Microsoft Graph access.         |
| `SharePoint:SiteId` / `ListId`             | The Report Log list's Graph site/list IDs.                                  |
| `SharePoint:DateFieldInternalName`         | Internal column name used to determine entry age.                          |
| `SharePoint:ImageUrlFieldInternalNames`    | Up to 5 internal column names holding Cloudinary image URLs.              |
| `Cloudinary:CloudName` / `ApiKey` / `ApiSecret` | Cloudinary account used to delete referenced images.                   |
| `Email:EmailEnabled`                       | Controls whether summary/failure emails are sent (default `false`).        |
| `Email:EmailRecipients`                    | Comma-separated recipient addresses.                                       |
| `Email:EmailFromAddress`                   | The "From" address used for sent emails.                                    |
| `Email:Smtp*`                              | SMTP host/port/credentials used by MailKit to send email.                 |

For local development, copy
`src/FBOutletConditionsHousekeeping.Functions/local.settings.json.example`
to `local.settings.json` in the same folder and fill in real values — that
file is gitignored and never committed.

---

## Getting started locally

```text
dotnet restore FBOutletConditionsHousekeeping.slnx
dotnet build FBOutletConditionsHousekeeping.slnx --configuration Release
dotnet test FBOutletConditionsHousekeeping.slnx --configuration Release

cd src/FBOutletConditionsHousekeeping.Functions
cp local.settings.json.example local.settings.json   # then edit with real values
func start
```

Full prerequisites, Azure AD / Cloudinary / SMTP setup, and troubleshooting
are in `docs/SETUP-GUIDE.md`.

---

## Deployment

Two documented paths (`docs/SETUP-GUIDE.md` Section 16):

- **Azure Portal** (Section 16.2) — step-by-step creation of the Resource
  Group, Storage Account, Function App, and Application Settings, plus
  manual code deployment options.
- **GitHub Actions** (Section 16.3) — the included
  `.github/workflows/deploy-function-app.yml` builds, tests, and deploys on
  every push to `main`, authenticating to Azure via OIDC federated
  credentials (no client secret or publish profile stored in GitHub).

Always verify a run with `Cleanup:DryRun=true` before disabling dry run in
any environment.

---

## Testing

```text
dotnet test FBOutletConditionsHousekeeping.slnx
```

25 unit tests cover: age-based eligibility, the Cloudinary-before-SharePoint
deletion ordering rule, partial-failure isolation, dry-run behavior,
summary/failure email content (including run duration, age threshold, and
branding) and the email-enabled/disabled switch, and Cloudinary `public_id`
parsing. See `docs/SPECIFICATION.md` Section 22 for the full
test-to-requirement traceability.

Live end-to-end testing against a real SharePoint tenant, Cloudinary
account, and SMTP server has not been performed — see
`docs/SPECIFICATION.md` Section 23 (Known Limitations).

---

## Documentation

| Document                      | Contents                                                              |
| ----------------------------------| ---------------------------------------------------------------------- |
| `docs/SPECIFICATION.md`         | Requirements, user stories, acceptance criteria, business rules, test summary. |
| `docs/ARCHITECTURE.md`           | Technology stack, architecture decisions (ADRs), security architecture, diagrams. |
| `docs/SETUP-GUIDE.md`            | Full configuration reference, Azure Portal & GitHub Actions deployment walkthroughs, troubleshooting. |
| `docs/SCHEMA.md`                 | SharePoint list field mapping, Cloudinary `public_id` derivation. |
| `docs/EXECUTIVE-SUMMARY.docx`    | Management-level overview, business benefits, risks, implementation status. |
