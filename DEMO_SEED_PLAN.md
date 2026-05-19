# Demo Seed Data Plan — Unified Platform

> **Status:** Plan only — not yet implemented.  
> All data described here will be inserted by a new `DemoSeedData.cs` class,
> callable from `Program.cs` when the environment variable / app-setting
> `Seed:LoadDemoData = true` is set.

---

## 1. Brands (5)

| # | Name | Primary Colour | Languages |
|---|------|---------------|-----------|
| 1 | **NovaTrade FX** | `#1A73E8` (blue) | EN, DE |
| 2 | **ApexMarkets** | `#E84C4C` (red) | EN, FR |
| 3 | **ZenithCapital** | `#2ECC71` (green) | EN, ES |
| 4 | **PrimeVault** | `#9B59B6` (purple) | EN, PT |
| 5 | **SilkRoute Invest** | `#F39C12` (amber) | EN, AR |

Each brand will be seeded with:
- `Name`, `PrimaryColour`, `SiteUrl`
- `CrmUrl`, `RedmineUrl`, `QuemetricsUrl` (dummy HTTPS URLs)
- `EmailDealing`, `EmailAml`, `EmailAssign`, `EmailDemo`
- `BrandLinksJson` — 2 labelled links each (e.g. Bank Details - EN, T&C - EN)
- `FooterSignatureHtml` — a short branded HTML signature block
- One **EmailTemplate** per brand (a welcome / onboarding email template)

---

## 2. Users — Managers (2)

Role: `BrandManager`

| # | Display Name | Email | Manages Brands |
|---|-------------|-------|---------------|
| 1 | Sarah Mitchell | `sarah.mitchell@unified.local` | NovaTrade FX, ApexMarkets |
| 2 | David Okonkwo | `david.okonkwo@unified.local` | ZenithCapital, PrimeVault, SilkRoute Invest |

Both managers:
- `EmailConfirmed = true`
- Password: `Demo@1234!`
- `HourlyRate = 25.00`
- `Language = "EN"`

---

## 3. Users — Team Leaders (5)

Role: `TeamLeader`

| # | Display Name | Email | Team | Brand(s) |
|---|-------------|-------|------|---------|
| 1 | James Thornton | `james.thornton@unified.local` | Team Alpha | NovaTrade FX |
| 2 | Priya Sharma | `priya.sharma@unified.local` | Team Beta | ApexMarkets |
| 3 | Carlos Rivera | `carlos.rivera@unified.local` | Team Gamma | ZenithCapital |
| 4 | Annika Larsson | `annika.larsson@unified.local` | Team Delta | PrimeVault |
| 5 | Mohammed Al-Farsi | `mo.alfarsi@unified.local` | Team Epsilon | SilkRoute Invest |

All leaders:
- Password: `Demo@1234!`
- `HasCsLiveHelp = true`
- `HourlyRate = 18.00`
- Assigned to their respective team as `TeamLeaderId`

---

## 4. Users — CS Agents (25, 5 per team)

Role: `CSAgent`

Password for all: `Demo@1234!`  
`HourlyRate` varies between `10.00` – `15.00`

### Team Alpha — NovaTrade FX (James Thornton)
| Agent | Email | Language | HasWeekendShift |
|-------|-------|----------|----------------|
| Ethan Collins | ethan.collins@unified.local | EN | true |
| Lena Becker | lena.becker@unified.local | DE | false |
| Noah Fischer | noah.fischer@unified.local | DE | true |
| Sofia Mendes | sofia.mendes@unified.local | EN | false |
| Omar Hassan | omar.hassan@unified.local | EN | true |

### Team Beta — ApexMarkets (Priya Sharma)
| Agent | Email | Language | HasWeekendShift |
|-------|-------|----------|----------------|
| Isabelle Dupont | isabelle.dupont@unified.local | FR | true |
| Lucas Martin | lucas.martin@unified.local | FR | false |
| Aisha Ndiaye | aisha.ndiaye@unified.local | EN | true |
| Ben Carter | ben.carter@unified.local | EN | false |
| Yuki Tanaka | yuki.tanaka@unified.local | EN | true |

### Team Gamma — ZenithCapital (Carlos Rivera)
| Agent | Email | Language | HasWeekendShift |
|-------|-------|----------|----------------|
| Elena Vásquez | elena.vasquez@unified.local | ES | false |
| Diego López | diego.lopez@unified.local | ES | true |
| Rachel Kim | rachel.kim@unified.local | EN | false |
| Tom Bradley | tom.bradley@unified.local | EN | true |
| Nina Johansson | nina.johansson@unified.local | EN | false |

### Team Delta — PrimeVault (Annika Larsson)
| Agent | Email | Language | HasWeekendShift |
|-------|-------|----------|----------------|
| Victor Santos | victor.santos@unified.local | PT | true |
| Ana Ferreira | ana.ferreira@unified.local | PT | false |
| Jack Wilson | jack.wilson@unified.local | EN | true |
| Grace Turner | grace.turner@unified.local | EN | false |
| Leon Müller | leon.muller@unified.local | EN | true |

### Team Epsilon — SilkRoute Invest (Mohammed Al-Farsi)
| Agent | Email | Language | HasWeekendShift |
|-------|-------|----------|----------------|
| Fatima Al-Zahra | fatima.alzahra@unified.local | AR | false |
| Khalid Mansour | khalid.mansour@unified.local | AR | true |
| Emma Clarke | emma.clarke@unified.local | EN | false |
| Ryan O'Brien | ryan.obrien@unified.local | EN | true |
| Zara Ahmed | zara.ahmed@unified.local | EN | false |

---

## 5. Teams (5)

| Team | Language | Leader | Members |
|------|----------|--------|---------|
| Team Alpha | EN/DE | James Thornton | 5 Alpha agents |
| Team Beta | EN/FR | Priya Sharma | 5 Beta agents |
| Team Gamma | EN/ES | Carlos Rivera | 5 Gamma agents |
| Team Delta | EN/PT | Annika Larsson | 5 Delta agents |
| Team Epsilon | EN/AR | Mohammed Al-Farsi | 5 Epsilon agents |

Each agent is linked via `AgentTeam` join table.  
Each agent is linked to their brand via `AgentBrand` join table.

---

## 6. Shift Templates (3)

| Name | Start | End | IsWeekendShift |
|------|-------|-----|---------------|
| Morning Shift | 07:00 | 15:00 | false |
| Afternoon Shift | 14:00 | 22:00 | false |
| Weekend Shift | 09:00 | 17:00 | true |

---

## 7. Agent Schedules

For each agent, seed **4 weeks** of schedule entries (Mon–Fri) using their
natural shift template, plus weekend entries for agents with `HasWeekendShift = true`.
A random ~10% of weekdays will be seeded as `DayOff` or `Vacation`.

---

## 8. Attendance Logs

For each agent, seed attendance for the **last 30 calendar days**:
- `CheckInTime` = shift start ± 0–10 minutes random drift
- `CheckOutTime` = shift end ± 0–15 minutes random drift
- `Status` = `Present` (90%), `Late` (7%), `Absent` (3%)
- `PayType` = `Regular` (Mon–Fri), `Weekend` (Sat–Sun shifts)
- All entries in `Approved` state (no pending retrospectives)

---

## 9. Team Reports (Weekly & Monthly)

Each team leader will have:
- **4 weekly** `TeamReport` records (last 4 weeks)
- **1 monthly** `TeamReport` record (last month)

Each report contains one `AgentStat` per team member with randomised but
realistic values:
- `Chats`: 40–120
- `Tickets`: 10–50
- `Calls`: 5–30
- `FTD`: 0–5
- Top-performer flags set on the highest-value agent per metric

---

## 10. Performance Reviews

Each team leader authors **2 performance reviews** per agent (10 total per team,
50 across the platform):
- Review 1: ~6 weeks ago (`PeriodLabel = "May 2025"`)
- Review 2: ~2 weeks ago (`PeriodLabel = "June 2025"`)

Each review contains **3 `ReviewItem`s** (one per `ReviewCategory`: Ticket, Chat, Call):
- `Rating`: 6–10
- `Positive`: short praise note
- `Negative`: short improvement note
- `ActionRequired`: true for ~20% of items

---

## 11. POI Simulations

For each brand, seed **5–8 POI simulation records**:
- `ClientId`: random alphanumeric (e.g. `CLT-00481`)
- `SimulatedAt`: random date within last 30 days
- `LoggedById`: one of the brand's agents
- `PoiReceived`: true for ~60%, false for ~40% (to show pending items)
- Received entries have `ReceivedAt` and `ReceivedById` populated

---

## 12. Updates / Announcements

Seed **10 `Update` records** authored by the managers:
- 3 pinned (`IsPinned = true`)
- 7 regular
- Spread across brands via `UpdateBrand` join table
- Tags include: "Compliance", "System", "Process", "Holiday", "Policy"
- Realistic `Title` and `Body` (HTML) content

---

## 13. Vault

**Categories (global):**
- CRM Credentials
- Email Accounts
- Trading Platform
- Admin Portals

**Entries:**  
Each agent receives **2 vault entries** provisioned by their team leader:
- One CRM credential
- One email account credential
- `EncryptedPassword` = AES-encrypted placeholder (same key as app)
- `ProvisionedByUserId` = team leader Id

---

## 14. Work Distribution

Seed **5 days** of work distribution entries (Mon–Fri of the current week),
one per day, created by each team's leader.  
Each entry body uses `@mentions` of agent display names and lists task assignments.

---

## 15. CS Live Help Slots

If `HasCsLiveHelp = true` agents exist, seed the current week's slot grid
with agents assigned across time-blocks (as the service normally generates,
but pre-populated to show a busy grid).

---

## Implementation Notes

### File to Create
`Unified\Data\DemoSeedData.cs`

### How to Trigger
Add to `appsettings.Development.json`:
```json
"Seed": {
  "LoadDemoData": true
}
```

In `Program.cs`, after the existing `SeedData.InitialiseAsync(...)` call:
```csharp
if (app.Configuration["Seed:LoadDemoData"] == "true")
	await DemoSeedData.LoadAsync(scope.ServiceProvider);
```

### Safety Guard
`DemoSeedData.LoadAsync` will **no-op** if `Brands.Count >= 5` is already true,
so it is safe to restart the application without duplicating data.

### Password Hashing
All user passwords (`Demo@1234!`) are hashed via `UserManager<AppUser>.CreateAsync`,
exactly as the existing bootstrap admin is created.

### Encryption
Vault passwords are encrypted using the same `IDataProtector` that the
`VaultService` already uses, so vault entries will be readable immediately.

---

## Summary Counts

| Entity | Count |
|--------|-------|
| Brands | 5 |
| Email Templates | 5 (1 per brand) |
| Managers | 2 |
| Team Leaders | 5 |
| CS Agents | 25 |
| Teams | 5 |
| AgentTeam links | 25 |
| AgentBrand links | 25 |
| Shift Templates | 3 |
| Agent Schedule Entries | ~500 (25 agents × ~20 days) |
| Attendance Logs | ~750 (25 agents × 30 days) |
| Team Reports (weekly) | 20 (5 teams × 4 weeks) |
| Team Reports (monthly) | 5 |
| Agent Stats | ~625 (25 per report) |
| Performance Reviews | 50 (2 per agent) |
| Review Items | 150 (3 per review) |
| POI Simulations | ~35 (5–8 per brand) |
| Updates | 10 |
| Vault Categories | 4 |
| Vault Entries | 50 (2 per agent) |
| Work Distribution Entries | 25 (5 days × 5 teams) |
| CS Live Help Slots | ~40 (current week grid) |
