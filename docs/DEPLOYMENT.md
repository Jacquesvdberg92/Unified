# Unified — Deployment Guide

This document covers two deployment paths:

1. **Local deployment on Windows using IIS**
2. **Cloud deployment to NameCheap ASP.NET Hosting**

---

## Prerequisites (Both Paths)

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installed on the build machine
- SQL Server (Express or full edition) — or a compatible connection string configured in `appsettings.json`
- The published output of the application (`dotnet publish`)

---

## Path 1: Local Deployment with IIS (Windows)

### Step 1 — Install the .NET 8 Hosting Bundle

Download and install the **ASP.NET Core Hosting Bundle** for .NET 8:

> https://dotnet.microsoft.com/en-us/download/dotnet/8.0

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

This produces a self-contained folder at `./publish` ready to be served.

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

### Step 5 — Set the Connection String

Edit `appsettings.json` (or use environment variables / IIS environment variable bindings):

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;Database=UnifiedDb;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

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

### Step 7 — Verify

Browse to your configured hostname/port. The login page should appear. Register the first admin account and configure roles as needed.

---

## Path 2: NameCheap ASP.NET Hosting

NameCheap's shared ASP.NET hosting uses **Plesk** as the control panel and supports **.NET 8** on Windows shared hosting plans.

### Step 1 — Choose the Right Plan

Ensure your NameCheap hosting plan includes:
- **ASP.NET Core / .NET 8** support
- **MSSQL Server** database add-on (required — MySQL is not compatible without changing the EF provider)

Log into your NameCheap account and confirm the plan details under **Hosting → Manage**.

### Step 2 — Publish the Application (Framework-Dependent)

```powershell
dotnet publish -c Release -r win-x86 --self-contained false -o ./publish
```

> NameCheap shared hosting is typically 32-bit (`win-x86`). Confirm with their support if unsure.

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
3. Set **.NET version** to **8.0**
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

- **Seed roles and an initial admin user** — if no seeding is built in, register the first user and assign roles via the database or an admin panel.
- **Configure SMTP** — update `appsettings.json` with your email provider settings for outbound email functionality.
- **HTTPS** — strongly recommended. On IIS, use a certificate via **IIS → Server Certificates**. On NameCheap/Plesk, use the **Free SSL** option (Let's Encrypt) available in Plesk.

---

## Support

Refer to [SUPPORT.md](./SUPPORT.md) for information on the current support posture for this project.
