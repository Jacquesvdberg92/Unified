# Unified — Deployment Guide

This document covers two deployment paths:

1. **Local deployment on Windows using IIS**
2. **Cloud deployment to NameCheap ASP.NET Hosting**

---

## Prerequisites (Both Paths)

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) installed on the build machine
- SQL Server (Express or full edition) — or a compatible connection string configured in `appsettings.json`
- The published output of the application (`dotnet publish`)

---

## Path 1: Local Deployment with IIS (Windows)

### Step 1 — Install the .NET 10 Hosting Bundle

Download and install the **ASP.NET Core Hosting Bundle** for .NET 10:

> https://dotnet.microsoft.com/en-us/download/dotnet/10.0

This installs the runtime, ASP.NET Core Module (ANCM), and IIS integration in one step.
Restart IIS after installation:

```powershell
iisreset
```

### Step 2 — Publish the Application

From the solution root, run:

```powershell
dotnet publish -c Release -o ./publish
```

This produces a framework-dependent build in `./publish`, which runs on the Hosting Bundle from Step 1.

### Step 3 — Create a New IIS Site

1. Open **IIS Manager** (`inetmgr`)
2. Right-click **Sites** → **Add Website**
3. Fill in:
   - **Site name:** `Unified` (or your preferred name)
   - **Physical path:** Point to the `./publish` folder
   - **Binding:** Choose your port (e.g. `80`) and hostname (e.g. `unified.local`)
4. Click **OK**

### Step 4 — Configure the Application Pool

1. In IIS Manager, click **Application Pools**
2. Find the pool created for your site
3. Set **.NET CLR version** to **No Managed Code** (Kestrel handles the runtime)
4. Ensure **Identity** has read/write access to the publish folder

### Step 5 — Set the Connection String and Required Settings

Edit `appsettings.json` (or use environment variables / IIS environment variable bindings):

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;Database=UnifiedDb;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

Also set the following (see [Configuration Reference](#configuration-reference) below):

- `Seed:AdminEmail` and `Seed:AdminPassword`, required for a fresh database
- `DataProtection:KeysPath`, recommended
- `AI:ApiKey`, optional
- `Giphy:ApiKey`, optional, enables GIF search in Chat
- `GitHub:Token`, needed for Report a bug to create GitHub issues

### Step 6 — Apply Database Migrations

From the solution root (with the database reachable):

```powershell
dotnet ef database update
```

Or, if running manually against the published output, use a migration SQL script:

```powershell
dotnet ef migrations script --output migrations.sql
```

Then execute `migrations.sql` against your SQL Server instance.

> The app also applies any pending migrations itself at startup, so this step is optional when the
> app's database user is allowed to change the schema.

### Step 7 — Verify

Browse to your configured hostname/port. The login page should appear. Sign in with `Seed:AdminEmail` / `Seed:AdminPassword`, then create the other users under **Admin → Users**.

---

## Path 2: NameCheap ASP.NET Hosting

NameCheap's shared ASP.NET hosting uses **Plesk** as the control panel. Unified targets **.NET 10**, so confirm your plan offers the .NET 10 runtime before deploying.

### Step 1 — Choose the Right Plan

Ensure your NameCheap hosting plan includes:
- **ASP.NET Core / .NET 10** support
- **MSSQL Server** database add-on (required — MySQL is not compatible without changing the EF provider)

Log into your NameCheap account and confirm the plan details under **Hosting → Manage**.

### Step 2 — Publish the Application (Framework-Dependent)

```powershell
dotnet publish -c Release -r win-x86 --self-contained false -o ./publish
```

> NameCheap shared hosting is typically 32-bit (`win-x86`). Confirm with their support if unsure.
>
> If the plan doesn't offer .NET 10 yet, ask NameCheap support. A self-contained publish
> (`--self-contained true`) bundles the runtime with the app and may work on hosts without it.

### Step 3 — Create the MSSQL Database

1. In Plesk → **Databases** → **Add Database**
2. Note the **Server**, **Database name**, **Username**, and **Password**

Update `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_MSSQL_HOST;Database=YOUR_DB;User Id=YOUR_USER;Password=YOUR_PASS;MultipleActiveResultSets=true"
}
```

### Step 4 — Upload Files

Use either:

- **Plesk File Manager** — upload the contents of `./publish` to `httpdocs` (or a subdomain folder)
- **FTP/SFTP** — use FileZilla or similar with your Plesk FTP credentials

Upload everything inside `./publish` to the `httpdocs` directory.

### Step 5 — Configure the Application in Plesk

1. In Plesk → **Websites & Domains** → your domain → **ASP.NET Settings** (or **.NET Core**)
2. Set the **Document root** to `httpdocs`
3. Set **.NET version** to **10.0**
4. Set the **Application URL** to `/`
5. Save changes

### Step 6 — Apply Migrations

Since you cannot run `dotnet ef database update` directly on shared hosting, generate a SQL script locally and run it against your hosted database:

```powershell
dotnet ef migrations script --output migrations.sql
```

Then in Plesk → **Databases** → your database → **phpMyAdmin** (or the MSSQL equivalent web tool) — paste and execute `migrations.sql`.

Alternatively, connect via **SQL Server Management Studio (SSMS)** using the credentials from Step 3.

### Step 7 — Set Environment to Production

In Plesk → **ASP.NET Settings**, add an environment variable:

| Name | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |

### Step 8 — Verify

Browse to your domain. The Unified login page should load. If you see a 500 error, check the **Plesk error logs** under **Logs** → **Error Log**.

---

## Common Post-Deployment Steps (Both Paths)

- **Roles and the first admin** are seeded automatically on first start. Sign in with `Seed:AdminEmail` / `Seed:AdminPassword` and change the password under **Profile → Change Password**.
- **Forgotten passwords:** a Brand Manager can set a new one under **Admin → Users → Edit → Reset Password**.
- **HTTPS** — strongly recommended. On IIS, use a certificate via **IIS → Server Certificates**. On NameCheap/Plesk, use the **Free SSL** option (Let's Encrypt) available in Plesk.

---

## Configuration Reference

Settings can go in `appsettings.json`, in `appsettings.Production.json`, or in environment variables using `__` for nesting (e.g. `AI__ApiKey`). Keep secrets out of source control.

| Setting | Default | Notes |
|---|---|---|
| `Seed:AdminEmail` | `admin@unified.local` | Bootstrap Brand Manager, created only while the database has no users |
| `Seed:AdminPassword` | *(none)* | **Required** for a fresh database outside Development. The sample `Admin@1234!` is rejected |
| `AI:ApiKey` | *(empty)* | OpenRouter key from https://openrouter.ai/keys. Empty disables "Improve with AI" |
| `AI:Model` | `anthropic/claude-haiku-5.5` | Any OpenRouter model ID |
| `AI:FallbackModels` | `["openai/gpt-6-luna"]` | Tried in order if the main model fails or is unavailable |
| `AI:ReasoningEffort` | `none` | Leave as `none` for rewriting; reasoning tokens are billed as output |
| `AI:MaxOutputTokens` | `4000` | Upper bound on each response |
| `Giphy:ApiKey` | *(empty)* | GIPHY key from https://developers.giphy.com/dashboard/ (create an API app). Empty hides the GIF button in Chat. The server must reach `api.giphy.com` and browsers `media*.giphy.com` |
| `Giphy:Rating` | `g` | Highest content rating returned (`g`, `pg`, `pg-13`, `r`) |
| `GitHub:Token` | *(empty)* | Fine-grained token with **Issues** and **Contents** read/write on the repo below. Empty keeps bug reports in the app until it's set |
| `GitHub:Owner` / `GitHub:Repo` | `Jacquesvdberg92` / `Unified` | Where Report a bug creates issues |
| `GitHub:IssueLabels` | `["bug", "user-report"]` | Labels added to every reported issue |
| `GitHub:ScreenshotBranch` | `bug-report-screenshots` | Branch that screenshots are committed to (created from the default branch on first use) |
| `GitHub:RetryEveryMinutes` | `15` | How often reports that couldn't reach GitHub are retried (up to 10 attempts) |
| `DataProtection:KeysPath` | *(platform default)* | Folder for the key ring that encrypts Vault passwords and login cookies |
| `ForwardedHeaders:KnownProxies` | *(loopback only)* | IPs of reverse proxies on other machines, e.g. `["10.0.0.5"]` |
| `ForwardedHeaders:KnownNetworks` | *(none)* | Proxy networks in CIDR form, e.g. `["10.0.0.0/24"]` |
| `ForwardedHeaders:TrustAllProxies` | `false` | Old behaviour (trust any client's `X-Forwarded-*`). Only if the app is reachable solely through the proxy |

"Improve with AI" is limited to 20 requests per user per 10 minutes.

### Report a bug

Every signed-in user can open **System → Report a Bug**. Each report becomes a GitHub issue with the page, browser and server details, the user's recent browser and server errors, and an optional screenshot. If the repository is public, so are the reports. Screenshots wait in `App_Data/bug-reports` until they are uploaded, so the app pool needs write access there. Brand Managers and Team Leaders see all reports, and can resend or delete unsent ones, under **Bug Reports**.

### Data Protection keys

Vault passwords and login cookies are encrypted with ASP.NET Core's Data Protection key ring.
**If the key ring is lost, every saved Vault password becomes unreadable.** Point
`DataProtection:KeysPath` at a folder the app pool can write to, keep it out of `wwwroot`, and back
it up with the database.

When adding `KeysPath` to an existing install, first copy the existing key files (`key-*.xml`) into
the new folder. Otherwise existing Vault entries and sessions can no longer be decrypted. Where the
keys currently live depends on the host:

- **App pool loads its user profile:** `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` of the app pool
  identity. Copy the files across.
- **Registry** (`HKLM\SOFTWARE\Microsoft\ASP.NET\4.0.30319.0\AutoGenKeys\<SID>\DataProtection`):
  these can't simply be copied, so leave `KeysPath` unset unless you're prepared to re-enter Vault
  passwords.
- **The log warns about an "ephemeral key repository":** keys are already lost on every restart.
  Set `KeysPath` now; there is nothing to migrate.

### Behind Cloudflare Tunnel or another reverse proxy

Forwarded client IPs and HTTPS scheme are trusted only from proxies on the same machine
(loopback). If `cloudflared` or your proxy runs on a different host, add its address to
`ForwardedHeaders:KnownProxies`. Otherwise every visitor appears to come from the proxy, and the
site may redirect-loop if the proxy talks plain HTTP to the app.

---

## Upgrading an Existing (.NET 8) Deployment

1. Install the **.NET 10 Hosting Bundle** on the server and run `iisreset`.
2. Publish and deploy as above. The database schema is unchanged.
3. **Demo mode has been removed.** Any `Demo:Enabled` setting is now ignored and all visitors must sign in.
4. Set `AI:ApiKey` to an OpenRouter key if you use "Improve with AI". The previous GitHub Models key no longer applies.
5. If `cloudflared` doesn't run on the same machine as IIS, set `ForwardedHeaders:KnownProxies` (see above).
6. Optional: set `DataProtection:KeysPath`, after copying the existing keys into it.

---

## Support

Refer to [SUPPORT.md](./SUPPORT.md) for information on the current support posture for this project.
