# Setup and Deployment Guide

**Project:** FB Outlet Conditions Housekeeping
**Version:** 1.2
**Last Updated:** 2026-10-08
**Document Owner:** Leon Small

---

# 1. Overview

This guide allows a developer or administrator to configure, build, test, and
deploy the FB Outlet Conditions Housekeeping Azure Function from a clean
environment, including the required Azure AD, Cloudinary, and SMTP
configuration.

---

# 2. Prerequisites

| Requirement                          | Version                          | Purpose                                                      |
| ---------------------------------------| -----------------------------------| ----------------------------------------------------------------|
| .NET SDK                             | 10.0.x                           | Builds and runs the solution (isolated worker Azure Function). |
| Azure Functions Core Tools            | v4 (4.15.x or later)             | Local run/debug of the Function App.                         |
| Git                                   | Any current version              | Source control.                                              |
| Azure subscription                    | n/a                               | Hosting the Function App, Application Insights, and (recommended) Key Vault. |
| Microsoft Entra ID (Azure AD) admin access | n/a                          | Creating the App Registration and granting Graph admin consent. |
| Cloudinary account                    | n/a                               | Account/cloud that hosts the images referenced by Report Log entries. |
| SMTP account/relay                    | n/a                               | Used by MailKit to send summary/failure emails.              |
| IDE (Visual Studio 2022 17.14+ or VS Code with C# Dev Kit) | Current | Recommended for local development. |

---

# 3. Source Code

```text
git clone <repository-url>
cd FBOutletConditionsHousekeeping
```

Repository structure is described in Section 4. No credentials or access
tokens are embedded in the repository.

---

# 4. Project Structure

```text
FBOutletConditionsHousekeeping/
├── FBOutletConditionsHousekeeping.slnx   # Solution file (.NET 10 slnx format)
├── src/
│   ├── FBOutletConditionsHousekeeping.Core/        # Business logic, models, options, services
│   └── FBOutletConditionsHousekeeping.Functions/   # Azure Functions isolated worker host (timer trigger)
├── tests/
│   └── FBOutletConditionsHousekeeping.Core.Tests/  # xUnit + Moq unit tests
└── docs/
    ├── ARCHITECTURE.md
    ├── SPECIFICATION.md
    ├── SETUP-GUIDE.md (this document)
    └── SCHEMA.md
```

- `FBOutletConditionsHousekeeping.Core` has no dependency on the Azure
  Functions runtime and can be built/tested independently.
- `FBOutletConditionsHousekeeping.Functions` contains only the timer trigger
  (`Functions/ReportLogCleanupFunction.cs`) and DI composition (`Program.cs`).

---

# 5. Local Development Setup

## Step 1 — Clone the Repository

```text
git clone <repository-url>
cd FBOutletConditionsHousekeeping
```

## Step 2 — Restore Dependencies

```text
dotnet restore FBOutletConditionsHousekeeping.slnx
```

## Step 3 — Configure the Application

```text
cp src/FBOutletConditionsHousekeeping.Functions/local.settings.json.example src/FBOutletConditionsHousekeeping.Functions/local.settings.json
```

Edit the new `local.settings.json` and fill in real values per Section 6.
This file is excluded from source control (`.gitignore`) because it holds
secrets for local development.

## Step 4 — Configure External Services

Complete Sections 11–13 (SharePoint/Graph App Registration, Cloudinary
account, SMTP account) before running the function against real data.

## Step 5 — Run the Application Locally

```text
cd src/FBOutletConditionsHousekeeping.Functions
func start
```

The timer trigger fires on the configured schedule (default 02:30 daily).
To exercise it immediately during local development, temporarily set
`Cleanup:TimerSchedule` to a near-future CRON expression (for example,
`0 */2 * * * *` to run every 2 minutes), and leave `Cleanup:DryRun` set to
`true` until you are ready to verify real deletions.

---

# 6. Configuration

All settings below bind to strongly typed options classes in
`FBOutletConditionsHousekeeping.Core.Configuration` and are validated at
startup (`docs/SPECIFICATION.md` Section 8). None are hardcoded.

| Configuration                                  | Required                  | Description                                                                 | Example                          |
| ---------------------------------------------------| ----------------------------| ---------------------------------------------------------------------------------| -------------------------------------|
| `Cleanup:EntryAgeDays`                         | Yes                       | How old (days) a Report Log entry must be before it is eligible for deletion. | `90`                              |
| `Cleanup:DryRun`                               | Yes                       | When `true`, evaluates/logs only — no deletions are performed. **Defaults to `true`.** | `true`                     |
| `Cleanup:TimerSchedule`                        | Yes                       | NCRONTAB expression for the timer trigger.                                 | `0 30 2 * * *` (02:30 daily)      |
| `Cleanup:MaxPageSize`                          | No (defaults to 200)      | Graph list-item page size.                                                 | `200`                             |
| `SharePoint:TenantId`                          | Yes                       | Azure AD tenant ID.                                                         | `<configure securely>`           |
| `SharePoint:ClientId`                          | Yes                       | App Registration (application) ID.                                        | `<configure securely>`           |
| `SharePoint:ClientSecret`                      | Yes                       | App Registration client secret.                                           | `<configure securely>`           |
| `SharePoint:SiteId`                            | Yes                       | Graph site ID hosting the Report Log list.                                | `<configure securely>`           |
| `SharePoint:ListId`                            | Yes                       | Graph list ID for the Report Log list.                                    | `<configure securely>`           |
| `SharePoint:DateFieldInternalName`             | Yes                       | Internal column name used for age eligibility.                            | `Created`                         |
| `SharePoint:ImageUrlFieldInternalNames`        | Yes                       | Comma-separated internal column names (up to 5) holding Cloudinary image URLs. | `CloudinaryImageUrl1,CloudinaryImageUrl2,CloudinaryImageUrl3,CloudinaryImageUrl4,CloudinaryImageUrl5` |
| `Cloudinary:CloudName`                         | Yes                       | Cloudinary cloud name.                                                      | `<configure securely>`           |
| `Cloudinary:ApiKey`                            | Yes                       | Cloudinary API key.                                                        | `<configure securely>`           |
| `Cloudinary:ApiSecret`                         | Yes                       | Cloudinary API secret.                                                     | `<configure securely>`           |
| `Email:EmailEnabled`                           | Yes                       | Controls whether summary/failure emails are sent. **Defaults to `false`.** | `true`                             |
| `Email:EmailRecipients`                        | Required if `EmailEnabled=true` | Comma-separated recipient email addresses.                          | `admin1@example.com,admin2@example.com` |
| `Email:EmailFromAddress`                       | Required if `EmailEnabled=true` | The "From" address used for cleanup emails.                         | `noreply@example.com`             |
| `Email:SmtpHost`                               | Required if `EmailEnabled=true` | SMTP server hostname.                                                | `smtp.office365.com`              |
| `Email:SmtpPort`                               | No (defaults to 587)      | SMTP server port.                                                          | `587`                              |
| `Email:SmtpUseSsl`                             | No (defaults to `false`)  | `true` for implicit SSL (e.g. port 465); `false` to negotiate STARTTLS (e.g. port 587). | `false`                   |
| `Email:SmtpUsername`                           | Required if `EmailEnabled=true` | SMTP authentication username.                                        | `<configure securely>`           |
| `Email:SmtpPassword`                           | Required if `EmailEnabled=true` | SMTP authentication password.                                       | `<configure securely>`           |

Never include actual secrets in this document or in source control. Use
placeholders:

```text
SharePoint__ClientSecret=<configure securely>
Cloudinary__ApiSecret=<configure securely>
Email__SmtpPassword=<configure securely>
```

---

# 7. Environment Variables

In Azure, Application Settings are exposed to the Functions host as
environment variables. Nested configuration keys (e.g. `Cleanup:EntryAgeDays`)
may be set using either colon or double-underscore syntax, depending on
where the setting is configured:

```text
Cleanup__EntryAgeDays=90
Cleanup__DryRun=true
Cleanup__TimerSchedule=0 30 2 * * *
SharePoint__TenantId=<configure securely>
SharePoint__ClientId=<configure securely>
SharePoint__ClientSecret=<configure securely>
SharePoint__SiteId=<configure securely>
SharePoint__ListId=<configure securely>
SharePoint__DateFieldInternalName=Created
SharePoint__ImageUrlFieldInternalNames=CloudinaryImageUrl1,CloudinaryImageUrl2,CloudinaryImageUrl3,CloudinaryImageUrl4,CloudinaryImageUrl5
Cloudinary__CloudName=<configure securely>
Cloudinary__ApiKey=<configure securely>
Cloudinary__ApiSecret=<configure securely>
Email__EmailEnabled=true
Email__EmailRecipients=admin1@example.com,admin2@example.com
Email__EmailFromAddress=noreply@example.com
Email__SmtpHost=smtp.office365.com
Email__SmtpPort=587
Email__SmtpUseSsl=false
Email__SmtpUsername=<configure securely>
Email__SmtpPassword=<configure securely>
```

For local development, use `local.settings.json` (`Values` section) with
colon-separated keys instead (see `local.settings.json.example`).

---

# 8. Secrets Management

- **Local development:** secrets live only in the gitignored
  `local.settings.json`; never commit this file.
- **Azure (recommended):** store `SharePoint:ClientSecret`,
  `Cloudinary:ApiSecret`, and `Email:SmtpPassword` in Azure Key Vault, and
  reference them from Function App Application Settings using Key Vault
  references:

  ```text
  @Microsoft.KeyVault(SecretUri=https://<vault-name>.vault.azure.net/secrets/<secret-name>/)
  ```

- Grant the Function App's managed identity `Key Vault Secrets User` (or
  equivalent) access to read the referenced secrets.
- Never commit secrets to source control, this document, or any other
  project documentation.

---

# 9. Database Setup

Not applicable — this application has no application-owned database. See
`docs/SCHEMA.md`.

---

# 10. Database Migration

Not applicable.

---

# 11. External Services

| Service              | Purpose                                             | Configuration Required                                          |
| -----------------------| --------------------------------------------------------| -------------------------------------------------------------------|
| SharePoint Online / Microsoft Graph | Read/delete Report Log list items.      | Azure AD App Registration (Section 12); `SharePoint:*` settings. |
| Cloudinary           | Delete images referenced by Report Log entries.     | Cloudinary account API key/secret (Section 13); `Cloudinary:*` settings. |
| SMTP server          | Deliver summary/failure notification emails via MailKit. | SMTP account credentials; `Email:Smtp*` settings.          |

---

# 12. Authentication Setup (Azure AD / Microsoft Graph)

1. In the Azure Portal, go to **Microsoft Entra ID → App registrations → New registration**.
   - Name: e.g. `FBOutletConditionsHousekeeping-Function`.
   - Supported account types: single tenant (this organization only).
   - No redirect URI is required (this is an unattended/daemon app).
2. Record the **Application (client) ID** and **Directory (tenant) ID** —
   these become `SharePoint:ClientId` and `SharePoint:TenantId`.
3. Go to **Certificates & secrets → New client secret**. Record the secret
   value immediately (it is not shown again) — this becomes
   `SharePoint:ClientSecret`. Track its expiry date for rotation.
4. Go to **API permissions → Add a permission → Microsoft Graph →
   Application permissions**, and add `Sites.ReadWrite.All`.
5. Click **Grant admin consent** for the tenant (requires a Global
   Administrator or Privileged Role Administrator).
6. **Recommended hardening:** rather than granting `Sites.ReadWrite.All`
   tenant-wide, configure a
   [Graph application access policy](https://learn.microsoft.com/en-us/graph/auth-limit-mailbox-access)-equivalent
   site-scoped restriction for SharePoint (via
   `Add-SPOSiteRestrictedAccessApp` / Graph site permission scoping) so the
   app can only access the specific site hosting the Report Log list.
7. Identify the Graph **Site ID** and **List ID** for the Report Log list:
   - Site ID: `GET https://graph.microsoft.com/v1.0/sites/{hostname}:/{site-path}`
   - List ID: `GET https://graph.microsoft.com/v1.0/sites/{site-id}/lists`
   Record these as `SharePoint:SiteId` and `SharePoint:ListId`.
8. Identify the **internal names** (not display names) of the date column
   used for age eligibility and of up to 5 columns holding Cloudinary image
   URLs:
   `GET https://graph.microsoft.com/v1.0/sites/{site-id}/lists/{list-id}/columns`
   Record these as `SharePoint:DateFieldInternalName` and
   `SharePoint:ImageUrlFieldInternalNames`.

Never document actual client secrets, tenant IDs, site IDs, or list IDs in
this file — use placeholders as shown in Section 6.

---

# 13. Authorization Setup

- **Roles/groups:** none required — this is a single unattended service
  identity.
- **Cloudinary:** create (or reuse) an API key/secret pair scoped to the
  cloud that hosts the images referenced by Report Log entries; record as
  `Cloudinary:CloudName` / `Cloudinary:ApiKey` / `Cloudinary:ApiSecret`.
- **SMTP:** create (or reuse) an account/app password authorized to send
  mail through the organization's SMTP relay; record as
  `Email:SmtpUsername` / `Email:SmtpPassword`. Some providers (e.g.
  Microsoft 365) require an app password or OAuth2 SMTP depending on
  Conditional Access/tenant policy — consult your mail administrator if
  basic SMTP AUTH is blocked.
- **Administrative access:** managing the App Registration's credentials
  and Graph admin consent requires Azure AD administrator privileges, held
  by the organization's identity team, not by this application.

---

# 14. Local Testing

## Unit Tests

```text
dotnet test tests/FBOutletConditionsHousekeeping.Core.Tests/FBOutletConditionsHousekeeping.Core.Tests.csproj
```

Or, to run every test project in the solution:

```text
dotnet test FBOutletConditionsHousekeeping.slnx
```

As of this writing, this executes 23 tests (see `docs/SPECIFICATION.md`
Section 22 for the mapping to TEST-001 through TEST-011), all passing.

## Integration Tests

Not implemented in this version — no live SharePoint/Cloudinary/SMTP
credentials were available in the development environment. See
`docs/SPECIFICATION.md` Section 23 (Known Limitations).

## End-to-End Tests

Not applicable — this is a headless, timer-triggered function with no
user-facing interface to drive end-to-end.

## Linting

No additional linter is configured beyond the C# compiler's built-in
warnings. Confirm a clean build with:

```text
dotnet build FBOutletConditionsHousekeeping.slnx --warnaserror
```

## Static Analysis

No separate static analysis tool (e.g. SonarQube, Roslyn analyzers package)
is configured in this version.

---

# 15. Build

```text
dotnet restore FBOutletConditionsHousekeeping.slnx
dotnet build FBOutletConditionsHousekeeping.slnx --configuration Release
```

Expected output: all three projects (`FBOutletConditionsHousekeeping.Core`,
`FBOutletConditionsHousekeeping.Functions`,
`FBOutletConditionsHousekeeping.Core.Tests`) build with 0 errors.

---

# 16. Deployment

## 16.1 Development

Run locally via `func start` (Section 5) against a non-production
SharePoint site/Cloudinary cloud if available, with `Cleanup:DryRun=true`.

## Test / Staging (Not Currently Provisioned)

Only a single (production) Azure Function App environment is described by
this project (see `docs/ARCHITECTURE.md` Section 19). Provisioning
additional environments is a future consideration.

Two deployment paths to that single production environment are documented
below:

- **Section 16.2** — creating the Azure resources and deploying manually
  through the Azure Portal. Do this once regardless of which ongoing
  deployment method you choose, since the Function App itself must exist
  before either method can deploy code into it.
- **Section 16.3** — setting up the GitHub Actions workflow
  (`.github/workflows/deploy-function-app.yml`, already included in this
  repository) so that every push to `main` automatically builds, tests, and
  deploys the function. Recommended for ongoing deployments once the
  Function App exists.

---

## 16.2 Deploying via the Azure Portal

This section creates the Azure resources and performs a first, manual
deployment entirely through the Azure Portal UI — useful for an initial
setup, for environments without GitHub Actions access, or simply to confirm
the resource configuration before automating it.

### Step 1 — Create a Resource Group

1. Sign in to [portal.azure.com](https://portal.azure.com).
2. Search for **Resource groups → + Create**.
3. Choose your **Subscription**, enter a **Resource group** name (e.g.
   `rg-fboutlet-housekeeping-prod`), choose a **Region**, then
   **Review + create → Create**.

### Step 2 — Create a Storage Account

Azure Functions requires a Storage Account for the `AzureWebJobsStorage`
setting (timer trigger scheduling/locking — see Section 17).

1. In the resource group, **+ Create → Storage account**.
2. Enter a globally unique **Storage account name** (e.g.
   `stfboutlethousekeep`), same **Region** as the resource group.
3. **Redundancy**: `LRS` is sufficient for this workload (no application
   data is stored here — see `docs/ARCHITECTURE.md` Section 21).
4. **Review + create → Create**.

### Step 3 — Create the Function App

1. In the resource group, **+ Create → Function App**.
2. **Basics** tab:
   - **Function App name**: e.g. `func-fboutlet-housekeeping-prod` (this
     becomes part of the app's URL and is the value you will later set as
     the `AZURE_FUNCTIONAPP_NAME` GitHub repository variable, Section 16.3).
   - **Publish**: `Code`.
   - **Runtime stack**: `.NET`.
   - **Version**: `.NET 10 Isolated` (select the isolated worker option —
     this project does not use the in-process model; see
     `docs/ARCHITECTURE.md` Section 6.2 and Section 7 ADR on .NET 10).
   - **Region**: same as the resource group.
   - **Operating System**: `Windows` or `Linux` (either is supported by the
     isolated worker model; Linux Consumption is the lower-cost default
     choice if you have no other constraint).
   - **Plan type**: `Consumption` (pay-per-execution, sufficient for a
     once-daily timer job) or `Premium` if you need no cold start / VNet
     integration.
3. **Storage** tab: select the Storage Account created in Step 2.
4. **Monitoring** tab: **Enable Application Insights** → `Yes`, create a new
   Application Insights resource (or select an existing one).
5. **Networking** tab: leave defaults unless your organization requires
   private networking (not required by this project — see
   `docs/ARCHITECTURE.md` Section 25).
6. **Review + create → Create**. Wait for deployment to complete, then
   **Go to resource**.

### Step 4 — Create an Azure Key Vault

Key Vault is the recommended store for the three secret configuration
values (`SharePoint:ClientSecret`, `Cloudinary:ApiSecret`,
`Email:SmtpPassword` — see Section 8). Create it now, before configuring
Application Settings, so the Function App can reference it directly.

1. In the resource group, **+ Create → Key Vault**.
2. **Basics** tab:
   - **Key vault name**: globally unique, 3–24 characters, letters/digits/
     hyphens only (e.g. `kv-fboutlet-housekeep`, following the same naming
     pattern as the other resources created in this guide).
   - **Region**: same as the resource group.
   - **Pricing tier**: `Standard`.
3. **Access configuration** tab:
   - **Permission model**: `Azure role-based access control` (RBAC) —
     recommended over the legacy `Vault access policy` model, and the
     model assumed by Step 6 below.
   - Leave **Resource access** (deployment/disk encryption) switches off —
     not needed by this project.
4. Leave **Recovery options** (soft delete, typically 90 days) at their
   defaults — new vaults cannot disable soft delete. For a production
   vault, consider enabling **Purge protection** as well, which prevents
   the vault (and its secrets) from being permanently deleted before the
   soft-delete retention period expires, even by someone with delete
   permissions.
5. **Networking** tab: leave **Public access** enabled unless your
   organization requires private endpoints (not required by this project).
6. **Review + create → Create**.

**Equivalent via Azure CLI:**

```text
az keyvault create \
  --resource-group rg-fboutlet-housekeeping-prod \
  --name kv-fboutlet-housekeep \
  --location <region> \
  --sku standard \
  --enable-rbac-authorization true
```

### Step 5 — Add Secrets to the Key Vault

1. In the Key Vault resource, go to **Objects → Secrets → + Generate/Import**.
2. Create one secret per value, using a hyphenated secret **name** (Key
   Vault secret names only allow letters, digits, and hyphens — they
   cannot contain the colons or double-underscores used by the
   application's own configuration keys, even though each secret holds the
   same value):

   | Key Vault secret name     | Value                                 | Corresponds to config key     |
   | ------------------------------| ------------------------------------------| ------------------------------------|
   | `SharePoint-ClientSecret`    | The App Registration client secret (Section 12). | `SharePoint:ClientSecret`    |
   | `Cloudinary-ApiSecret`       | The Cloudinary API secret (Section 13).          | `Cloudinary:ApiSecret`       |
   | `Email-SmtpPassword`        | The SMTP account password (Section 13).          | `Email:SmtpPassword`        |

3. For each: **Upload options**: `Manual`, enter the **Name** and **Value**,
   leave **Activation/Expiration date** blank unless your organization
   requires secret expiry, **Create**.
4. Open each created secret and record its **Secret Identifier** (shown
   under the current version) — it has the form
   `https://<vault-name>.vault.azure.net/secrets/<secret-name>/<version>`.
   You will use the base URI (without the version, to always resolve the
   latest) in Step 7.

**Equivalent via Azure CLI:**

```text
az keyvault secret set --vault-name kv-fboutlet-housekeep \
  --name SharePoint-ClientSecret --value "<the-client-secret-value>"
az keyvault secret set --vault-name kv-fboutlet-housekeep \
  --name Cloudinary-ApiSecret --value "<the-cloudinary-api-secret-value>"
az keyvault secret set --vault-name kv-fboutlet-housekeep \
  --name Email-SmtpPassword --value "<the-smtp-password-value>"
```

Never record actual secret values in this document or any other committed
file — the placeholders above illustrate the command shape only.

### Step 6 — Grant the Function App Access to the Key Vault

1. In the Function App, go to **Settings → Identity**, switch
   **System assigned** to **On**, **Save**. Record the generated **Object
   (principal) ID**.
2. In the Key Vault resource, go to **Access control (IAM) → + Add → Add
   role assignment**, select the **Key Vault Secrets User** role, assign it
   to the Function App's managed identity (search by the Function App's
   name), **Review + assign**.
3. (If the Key Vault instead uses the legacy **Access policies** permission
   model rather than Azure RBAC): **Access policies → + Create**, grant
   **Get** and **List** secret permissions to the Function App's managed
   identity.
4. Role/policy assignments can take a few minutes to propagate; if Step 7's
   verification shows a resolution error immediately afterward, wait and
   retry before troubleshooting further.

### Step 7 — Configure Application Settings

1. In the Function App, go to **Settings → Environment variables**
   (older portal versions label this **Configuration → Application
   settings**).
2. Click **+ Add** for each setting listed in Section 6 of this guide
   (`Cleanup:EntryAgeDays`, `Cleanup:DryRun`, `SharePoint:TenantId`,
   `Email:EmailEnabled`, etc.), using the **double-underscore** form of the
   key (e.g. `Cleanup__EntryAgeDays`) as shown in Section 7 — the Portal's
   Application Settings grid does not accept a literal colon in the name.
3. For the three secret values, set the setting's **value** to a Key Vault
   reference pointing at the matching secret created in Step 5, instead of
   a plain value:

   | Application setting name     | Value                                                                              |
   | ---------------------------------| ---------------------------------------------------------------------------------------|
   | `SharePoint__ClientSecret`     | `@Microsoft.KeyVault(SecretUri=https://kv-fboutlet-housekeep.vault.azure.net/secrets/SharePoint-ClientSecret/)` |
   | `Cloudinary__ApiSecret`        | `@Microsoft.KeyVault(SecretUri=https://kv-fboutlet-housekeep.vault.azure.net/secrets/Cloudinary-ApiSecret/)`    |
   | `Email__SmtpPassword`          | `@Microsoft.KeyVault(SecretUri=https://kv-fboutlet-housekeep.vault.azure.net/secrets/Email-SmtpPassword/)`      |

   The **setting name** (left column) still uses the double-underscore form
   the application's configuration binder expects; only the **value**
   changes to a Key Vault reference. Omitting the version segment from the
   `SecretUri` (as shown) always resolves the secret's current version.
4. Click **Apply**, then **Confirm** to save and restart the Function App
   with the new settings.
5. **Verify the Key Vault references resolved:** back on the Environment
   variables / Application settings grid, each Key Vault-referenced setting
   shows a status of **Resolved** (often with a green check) once the
   Function App can successfully read it — this can take a minute after
   saving. If it instead shows **Resolution error**, re-check: the role
   assignment from Step 6 was granted to the correct managed identity, the
   secret name in the URI exactly matches the name used in Step 5, and the
   vault name in the URI is correct.

### Step 8 — Deploy the Code

Choose **one** of the following; both produce the same result for a one-off
manual deployment.

**Option A — Deployment Center (links to a Git repository):**

1. In the Function App, go to **Deployment → Deployment Center**.
2. **Source**: `GitHub` (or `External Git` for other providers), authorize
   access, select this repository and the `main` branch.
3. **Build provider**: `GitHub Actions` — the Portal generates and commits
   a starter workflow. **Do not use the Portal-generated workflow file for
   this project** — delete it in favor of the project's own
   `.github/workflows/deploy-function-app.yml` (Section 16.3), which
   already builds, tests, and deploys the correct project path using OIDC
   login rather than a stored publish profile.
4. Alternatively, choose **Build provider: Azure Pipelines / External** if
   you only want the Portal to record the deployment source without
   generating a workflow.

**Option B — Zip deploy via Azure CLI (no Git integration required):**

```text
cd src/FBOutletConditionsHousekeeping.Functions
dotnet publish --configuration Release --output ./publish
cd publish
zip -r ../publish.zip .
cd ..
az functionapp deployment source config-zip \
  --resource-group rg-fboutlet-housekeeping-prod \
  --name func-fboutlet-housekeeping-prod \
  --src publish.zip
```

**Option C — Azure Functions Core Tools (as in Section 5):**

```text
cd src/FBOutletConditionsHousekeeping.Functions
func azure functionapp publish func-fboutlet-housekeeping-prod
```

### Step 9 — First-Run Verification

1. Confirm `Cleanup:DryRun` (`Cleanup__DryRun`) is `true`.
2. In the Function App, go to **Functions → ReportLogCleanupFunction →
   Monitor** and either wait for the next scheduled 02:30 run or trigger it
   on demand via **Code + Test → Test/Run** (the Portal can invoke a timer
   function immediately for testing).
3. Verify the summary email (if `Email:EmailEnabled` is `true`) reports the
   expected eligible/would-delete counts, with zero actual deletions.
4. Only once satisfied, set `Cleanup:DryRun` to `false`.

---

## 16.3 Deploying via GitHub Actions (CI/CD)

This repository includes a ready-to-use workflow,
`.github/workflows/deploy-function-app.yml`, that on every push to `main`
(affecting `src/`, `tests/`, or the solution file):

1. Restores, builds (`Release`), and runs the full test suite — the job
   fails, and deployment does **not** proceed, if any test fails.
2. Publishes the Functions project and uploads it as a build artifact.
3. Logs in to Azure using **OIDC federated credentials** (no client secret
   or publish-profile password is stored in GitHub).
4. Deploys the published package to the target Function App using
   `azure/functions-action`.

This requires the Function App to already exist (Section 16.2, Steps 1–3)
before the workflow is run. The workflow only deploys code — it does not
configure Application Settings or Key Vault access — so Steps 4–7 (Key
Vault, secrets, access, and Application Settings) should also be completed
before relying on a real (non-dry-run) deployment, per Section 16.4.

### Step 1 — Create an Azure AD App Registration for GitHub OIDC

1. In **Microsoft Entra ID → App registrations → New registration**, name
   it e.g. `github-fboutlet-housekeeping-deploy`, single tenant, no
   redirect URI. **Register**.
2. Record the **Application (client) ID** and **Directory (tenant) ID**.
3. Go to **Certificates & secrets → Federated credentials → + Add
   credential**.
   - **Scenario**: `GitHub Actions deploying Azure resources`.
   - **Organization**: your GitHub org/user.
   - **Repository**: this repository's name.
   - **Entity type**: `Branch`, **Branch name**: `main` (add a second
     federated credential with **Entity type**: `Environment`, **Environment
     name**: `production` if you want the credential scoped to the
     `production` GitHub Environment used by the workflow's `deploy` job
     instead of/in addition to the branch).
   - **Name**: e.g. `github-main-branch`. **Add**.

   No client secret is created or needed — this federated credential lets
   GitHub Actions obtain a short-lived Azure AD token for each run instead.

### Step 2 — Grant the App Registration Access to the Resource Group

1. Open the resource group created in Section 16.2 Step 1.
2. **Access control (IAM) → + Add → Add role assignment**.
3. Role: **Contributor** (or, for tighter scoping, **Website Contributor**
   plus **Storage Blob Data Contributor** on just the Storage Account —
   least-privilege alternative if your organization requires it).
4. **Members**: select the App Registration created in Step 1 by name.
   **Review + assign**.

### Step 3 — Configure GitHub Repository Secrets and Variables

In the GitHub repository, go to **Settings → Secrets and variables →
Actions**:

**Secrets** (tab: *Secrets*):

| Name                     | Value                                                    |
| ----------------------------| -------------------------------------------------------------|
| `AZURE_CLIENT_ID`         | The App Registration's Application (client) ID (Step 1). |
| `AZURE_TENANT_ID`         | Your Directory (tenant) ID (Step 1).                     |
| `AZURE_SUBSCRIPTION_ID`   | The Azure subscription ID containing the resource group. |

**Variables** (tab: *Variables*):

| Name                        | Value                                                          |
| ------------------------------| ------------------------------------------------------------------|
| `AZURE_FUNCTIONAPP_NAME`     | The Function App name created in Section 16.2 Step 3 (e.g. `func-fboutlet-housekeeping-prod`). |

### Step 4 — Create the `production` GitHub Environment (Recommended)

The workflow's `deploy` job targets a GitHub Environment named
`production`. Creating it lets you add required reviewers or wait timers
before a deployment proceeds:

1. Repository **Settings → Environments → New environment**, name it
   `production`.
2. Optionally add **Required reviewers** so a deployment pauses for manual
   approval before running.

If you skip this step, the workflow still runs — GitHub creates the
environment implicitly on first use — but without any approval gate.

### Step 5 — Run the Workflow

1. Push a change under `src/`, `tests/`, or the solution file to `main`
   (or go to **Actions → Deploy Function App → Run workflow** to trigger it
   manually via `workflow_dispatch`).
2. In the **Actions** tab, confirm the `build-and-test` job passes before
   `deploy` starts.
3. Confirm the `deploy` job's **Deploy to Azure Functions** step completes
   successfully.
4. Perform the same first-run verification as Section 16.2 Step 9
   (`Cleanup:DryRun=true`, confirm the summary email, then disable DryRun).

### Troubleshooting GitHub Actions deployment

See Section 21 for general runtime troubleshooting. For the workflow
itself:

- **`azure/login` fails with an AADSTS70021 or federated credential
  mismatch error** — the federated credential's **Entity type**/**Branch**
  or **Environment name** in Azure AD (Step 1) does not match the branch or
  GitHub Environment actually running the workflow; confirm they match
  exactly, including case.
- **`azure/functions-action` fails with an authorization error** — confirm
  the role assignment in Step 2 was granted on the correct resource group
  and has not expired/been removed.
- **Workflow does not trigger on push** — confirm the changed files fall
  under the `paths` filters in `.github/workflows/deploy-function-app.yml`,
  or use **Run workflow** (`workflow_dispatch`) to trigger it manually.

---

## 16.4 Production Cutover

Regardless of which deployment method was used, do not enable real
deletions until:

1. `Cleanup:DryRun` has been confirmed `true` for at least one full run in
   the target environment.
2. The summary email (if `Email:EmailEnabled=true`) was reviewed and its
   eligible/would-delete counts matched expectations for the real
   SharePoint list and Cloudinary account.
3. Only then, set `Cleanup:DryRun` to `false` (Portal: Section 16.2 Step 7;
   GitHub Actions: update the Function App setting directly in the Portal —
   this workflow does not manage application settings, only code
   deployment, so changing `DryRun` does not require a new deployment).

---

# 17. Infrastructure

| Resource                        | Purpose                                                         |
| -----------------------------------| --------------------------------------------------------------------|
| Azure Function App              | Hosts the timer-triggered cleanup function.                    |
| Azure Storage Account            | Required by Azure Functions for the `AzureWebJobsStorage` setting (timer trigger scheduling/locking). |
| Azure Application Insights       | Logging/telemetry (Section 19).                                |
| Azure Key Vault (recommended)    | Stores `SharePoint:ClientSecret`, `Cloudinary:ApiSecret`, `Email:SmtpPassword`. |
| Microsoft Entra ID App Registration (Graph) | Service principal for Graph access (Section 12).    |
| Microsoft Entra ID App Registration (GitHub OIDC) | Service principal GitHub Actions uses to deploy, via federated credential — no stored secret (Section 16.3). |

No networking (VNet), containers, or additional compute resources are
required by this version.

---

# 18. CI/CD

A GitHub Actions workflow, `.github/workflows/deploy-function-app.yml`, is
included in this repository:

- **Pipeline location:** `.github/workflows/deploy-function-app.yml`.
- **Trigger:** push to `main` touching `src/`, `tests/`, or the solution
  file; also manually triggerable (`workflow_dispatch`).
- **Build process:** `dotnet restore` / `dotnet build --configuration
  Release` of `FBOutletConditionsHousekeeping.slnx`.
- **Test process:** `dotnet test --configuration Release` — the workflow
  fails (and does not deploy) if any test fails.
- **Deployment process:** `dotnet publish` of
  `FBOutletConditionsHousekeeping.Functions`, then `azure/functions-action`
  deploys the published package to the Function App named by the
  `AZURE_FUNCTIONAPP_NAME` repository variable, authenticated via Azure AD
  OIDC federated credentials (`azure/login`) — no client secret or publish
  profile is stored in GitHub.
- **Environment approvals:** the `deploy` job targets the GitHub
  `production` environment, which can optionally require manual reviewer
  approval before deploying (Section 16.3, Step 4).
- **Required secrets/variables:** `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
  `AZURE_SUBSCRIPTION_ID` (repository secrets) and `AZURE_FUNCTIONAPP_NAME`
  (repository variable) — see Section 16.3 for how to obtain and configure
  each one.

Full step-by-step setup instructions (creating the Azure AD app
registration and federated credential, granting resource group access,
configuring GitHub secrets/variables) are in Section 16.3.

Manual build/test/deploy via the `dotnet`/`func` CLI (Sections 14–16.2)
remains fully supported and is not replaced by this workflow — use whichever
fits a given situation (e.g. CLI for a one-off hotfix, GitHub Actions for
normal ongoing changes).

---

# 19. Monitoring

- **Application logging:** `ILogger` output flows to Application Insights
  via the Azure Functions Worker's OpenTelemetry integration (enabled
  automatically when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set).
- **Metrics/alerting:** standard Azure Functions/Application Insights
  monitoring (invocation count, failures, duration); no custom alert rules
  are created by this change — configure these in the Azure Portal per
  your organization's monitoring standards.
- **Health checks:** not applicable — a timer-triggered batch function has
  no HTTP health endpoint. Successful/failed runs are visible in
  Application Insights function invocation history and via the
  summary/failure emails (if enabled).

---

# 20. Backup and Recovery

Not applicable — this application owns no persistent data store. See
`docs/ARCHITECTURE.md` Section 21.

---

# 21. Troubleshooting

## Problem: Every run fails immediately with an authentication error

**Symptoms:**

Failure email (if enabled) or Application Insights shows an exception from
`Azure.Identity` or `Microsoft.Graph` on every run.

**Cause:**

The Azure AD App Registration's client secret has expired, admin consent
was not granted, or `SharePoint:TenantId`/`ClientId`/`ClientSecret` are
misconfigured.

**Resolution:**

Verify the client secret has not expired in **Certificates & secrets**;
rotate if needed and update the Function App setting (or Key Vault secret).
Confirm admin consent was granted for `Sites.ReadWrite.All` (Section 12).

---

## Problem: Entries are never recognized as eligible

**Symptoms:**

`EntriesScanned` is non-zero in the summary email, but `EligibleCount` is
always 0, or many entries show as `Skipped`.

**Cause:**

`SharePoint:DateFieldInternalName` does not match the list's actual internal
column name (SharePoint internal names often differ from the display name
shown in the UI, especially for columns renamed after creation).

**Resolution:**

Query `GET /sites/{site-id}/lists/{list-id}/columns` via Graph Explorer and
confirm the internal `name` value; update the setting accordingly.

---

## Problem: Entries fail with "Failed to delete Cloudinary image"

**Symptoms:**

The failure reason in the summary email references a specific image URL.

**Cause:**

Either the stored URL does not match the expected Cloudinary delivery URL
format (see `docs/SCHEMA.md` Section 17), or the configured Cloudinary API
key/secret does not have permission to delete the asset.

**Resolution:**

Confirm the URL format; confirm `Cloudinary:CloudName`/`ApiKey`/`ApiSecret`
correspond to the same Cloudinary account that hosts the image.

---

## Problem: No summary/failure emails are received despite `EmailEnabled=true`

**Symptoms:**

Application Insights shows the run completed, but no email arrives.

**Cause:**

Most commonly an SMTP authentication/connectivity failure (logged as an
error per ERR-004, but does not fail the run itself), or `EmailRecipients`
contains no syntactically valid address.

**Resolution:**

Check Application Insights for a logged SMTP send failure; verify
`Email:SmtpHost`/`Port`/`UseSsl`/`Username`/`Password` and that outbound
SMTP is not blocked by network/firewall rules.

---

# 22. Security Checklist

- [x] No secrets committed (all secrets are placeholders in this repository/documentation).
- [x] Authentication configured (Azure AD App Registration, Cloudinary API key/secret, SMTP credentials — Section 12/13).
- [x] Authorization configured (least-privilege Graph application permission).
- [x] Least-privilege permissions applied where tenant policy allows (site-scoped Graph access recommended, Section 12 step 6).
- [x] HTTPS/TLS enabled for all external calls (Graph, Cloudinary, SMTP over TLS).
- [x] Dependencies reviewed (only official NuGet packages used — Microsoft.Graph, Azure.Identity, CloudinaryDotNet, MailKit).
- [x] Logging configured (Application Insights via OpenTelemetry).
- [x] Sensitive data protected (secrets never logged; Key Vault recommended for storage).
- [ ] Production configuration secured — **pending administrator action**: configure real Key Vault references in the target Azure Function App before go-live.

---

# 23. Production Readiness Checklist

- [x] Application builds successfully (`dotnet build FBOutletConditionsHousekeeping.slnx`, 0 errors/warnings).
- [x] Tests pass (23/23 unit tests, `dotnet test`).
- [ ] Required configuration exists — **pending administrator action**: real tenant/site/list IDs, Cloudinary account, and SMTP credentials must be supplied (Section 6); not available in this development environment.
- [x] Database is configured — not applicable (no application-owned database).
- [ ] External integrations tested — **NOT TESTED** against live SharePoint/Cloudinary/SMTP in this environment; only unit-tested against mocked interfaces (see `docs/SPECIFICATION.md` Section 23).
- [ ] Monitoring configured — Application Insights wiring is in place; alert rules are an administrator action, not yet configured.
- [x] Backup configured — not applicable (no application-owned data store).
- [x] Security reviewed (Section 22).
- [ ] Deployment process tested — both the Azure Portal (Section 16.2) and GitHub Actions (Section 16.3) deployment procedures are documented, and the workflow YAML was validated for syntax, but neither has been executed against a real Azure subscription/GitHub repository in this environment (no live Azure subscription or GitHub remote was available).
- [x] Documentation updated (this change set).

---

# 24. Known Deployment Limitations

- The GitHub Actions workflow (`.github/workflows/deploy-function-app.yml`) has been validated for YAML syntax only; it has not yet been executed end-to-end against a real Azure subscription/Function App in this environment (no live Azure OIDC credentials or GitHub remote were available).
- Only a single (production) environment is described; no Dev/Test/Staging promotion path exists.
- Live end-to-end verification against a real SharePoint tenant, Cloudinary account, and SMTP server has not been performed — see `docs/SPECIFICATION.md` Section 23.

---

# 25. Setup Guide Change History

| Version | Date       | Change                                                                 | Author      |
| ------- | ---------- | ---------------------------------------------------------------------- | -------------|
| 1.0     | 2026-10-08 | Initial setup guide for new project.                                  | Claude Code |
| 1.1     | 2026-10-08 | Added detailed Azure Portal deployment walkthrough (Section 16.2) and GitHub Actions CI/CD deployment walkthrough (Section 16.3), including the new `.github/workflows/deploy-function-app.yml` workflow; updated Sections 17, 18, 23, 24 accordingly. | Claude Code |
| 1.2     | 2026-10-08 | Added full step-by-step Azure Key Vault creation and secret-population instructions to Section 16.2 (new Steps 4–6), renumbered the remaining Section 16.2 steps accordingly, and fixed cross-references to them elsewhere in this document. | Claude Code |
