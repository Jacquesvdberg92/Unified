# Unified — Solution Overview

## What Is Unified?

**Unified** is a multi-module internal business web application built on **ASP.NET Core (.NET 8)** with Razor Pages/MVC, Entity Framework Core, and ASP.NET Core Identity. It is designed to consolidate business workflows — particularly around brand management, email templating, process tracking, document handling, live CS allocation, and Finance department operations — into a single, cohesive platform.

Rather than juggling multiple disconnected tools, Unified brings the key operational pieces of a business together under one authenticated, role-controlled interface.

---

## Core Modules

| Module | Description |
|---|---|
| **Brand Management** | Manage brand profiles, documents, and associated metadata |
| **Email Templates** | Create, store, and send branded email templates |
| **Process Templates** | Define and track repeatable internal workflows |
| **CS Live Allocation** | Real-time customer service allocation and tracking |
| **POI Simulation** | Point-of-interest simulation with a full activity log |
| **Finance Workflows** | Finance-department tooling (actively being scoped and expanded) |
| **Identity & Roles** | User authentication, registration, and role-based access via ASP.NET Core Identity |

---

## Technology Stack

- **Backend:** ASP.NET Core (.NET 8), C#, Entity Framework Core
- **Frontend:** Razor Views, Bootstrap 5, ApexCharts, Flatpickr, Quill, DataTables, SortableJS
- **Database:** SQL Server (configurable via `appsettings.json`)
- **Auth:** ASP.NET Core Identity
- **Build tooling:** Gulp, Node.js (asset pipeline)

---

## Live Demo

A live preview of the application is available at:

**[https://demo.jacquesvdberg.me](https://demo.jacquesvdberg.me)**

> The demo environment may run on limited resources. Performance on the demo is not representative of a properly provisioned production deployment.

---

## Author & Attribution

Unified was designed and built by **Jacques van den Berg** ([@Jacquesvdberg92](https://github.com/Jacquesvdberg92)).

This project is made available for public use. **Removal of attribution or any attempt to sell this software will result in legal action.** The author has a demonstrated history of pursuing such matters through the courts — do not test it.

---

## Repository

[https://github.com/Jacquesvdberg92/Unified](https://github.com/Jacquesvdberg92/Unified)
