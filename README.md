# Unified

> A unified internal business platform for brand management, CS operations, email templates, process tracking, and finance workflows — built on ASP.NET Core (.NET 8).

Made with ❤️ by [Jacquesvdberg92](https://github.com/Jacquesvdberg92) &nbsp;•&nbsp; 🇺🇦 [We stand with Ukraine](https://u24.gov.ua/) 🇺🇦

---

## 🌐 Live Demo

**[https://demo.jacquesvdberg.me](https://demo.jacquesvdberg.me)**

---

## 📚 Documentation

| Document | Description |
|---|---|
| [Overview](docs/OVERVIEW.md) | What Unified is, its modules, tech stack, and demo link |
| [Deployment Guide](docs/DEPLOYMENT.md) | Step-by-step: deploy locally with IIS or to NameCheap ASP.NET hosting |
| [Support & Terms of Use](docs/SUPPORT.md) | Support posture, known issues, Finance roadmap, and usage terms |

---

## 🚀 Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- SQL Server (Express or full edition)

### Run Locally

```powershell
# Clone the repo
git clone https://github.com/Jacquesvdberg92/Unified.git
cd Unified

# Set your connection string in appsettings.json
# Then apply the database migration
dotnet ef database update

# Run the application
dotnet run
```

Browse to `https://localhost:5001` — register your first user and get started.

---

## 🛠️ Tech Stack

- **Backend:** ASP.NET Core (.NET 8), C#, Entity Framework Core
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
