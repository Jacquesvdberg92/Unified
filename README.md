# Unified

> A unified internal business platform for brand management, CS operations, email templates, process tracking, and finance workflows — built on ASP.NET Core (.NET 10).

Made with ❤️ by [Jacquesvdberg92](https://github.com/Jacquesvdberg92) &nbsp;•&nbsp; 🇺🇦 [We stand with Ukraine](https://u24.gov.ua/) 🇺🇦

---

## 📚 Documentation

| Document | Description |
|---|---|
| [Overview](docs/OVERVIEW.md) | What Unified is, its modules, and tech stack |
| [Deployment Guide](docs/DEPLOYMENT.md) | Step-by-step: deploy locally with IIS or to NameCheap ASP.NET hosting |
| [Support & Terms of Use](docs/SUPPORT.md) | Support posture, known issues, Finance roadmap, and usage terms |

---

## 🚀 Quick Start

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- SQL Server (Express or full edition)

### Run Locally

```powershell
# Clone the repo
git clone https://github.com/Jacquesvdberg92/Unified.git
cd Unified

# Set your connection string in appsettings.json, then run.
# Pending migrations are applied automatically at startup.
dotnet run
```

Browse to the URL shown in the console. In Development, sign in with the sample admin from
`appsettings.Development.json` (`admin@unified.local` / `Admin@1234!`). Outside Development you must
set `Seed:AdminPassword` before the first run (see below); the app refuses to start without one
while the database has no users.

### Configuration

Set these in `appsettings.json`, environment variables (`Section__Key`) or user secrets. Never commit real keys.

| Setting | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `Seed:AdminEmail` / `Seed:AdminPassword` | Bootstrap Brand Manager account, created only when the database has no users |
| `AI:ApiKey` | [OpenRouter](https://openrouter.ai/keys) API key for "Improve with AI". Leave empty to disable |
| `AI:Model` / `AI:FallbackModels` | Defaults to `anthropic/claude-haiku-5.5`, falling back to `openai/gpt-6-luna` |
| `DataProtection:KeysPath` | Folder for the encryption key ring (Vault passwords, login cookies). Back it up with the database |
| `ForwardedHeaders:KnownProxies` | Extra reverse-proxy IPs to trust; proxies on the same machine are trusted already |

See the [Deployment Guide](docs/DEPLOYMENT.md) for details.

---

## 🛠️ Tech Stack

- **Backend:** ASP.NET Core (.NET 10), C#, Entity Framework Core
- **Frontend:** Bootstrap 5, Razor Views, ApexCharts, DataTables, Quill, Flatpickr
- **Auth:** ASP.NET Core Identity
- **Database:** SQL Server

---

## ⚖️ License

This project is licensed under a **custom attribution license** — see [LICENSE.txt](LICENSE.txt) for full terms.

**Short version:** Free to use and run. You may **not** remove attribution or sell this software. Violations will be pursued legally.

---

## 🤝 Contributing

Pull requests are welcome for bug fixes and improvements. Please open an issue first for larger changes.
