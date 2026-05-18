# Phase 2B — Brand Model Expansion & CS Agent Email Template Access

> **Purpose:** Step-by-step implementation plan for Phase 2B.
> Tick each checkbox `[x]` when the step is complete.
> Steps are ordered — do not skip ahead.

---

## Overview

| Goal | Detail |
|------|--------|
| CS Agent access | All authenticated users can see Email Templates in the nav and use the Preview/Copy workflow |
| Brand model expansion | Replace raw JSON blob + flat URL fields with structured fields for site URL, tool URLs, department emails, and a labelled document-links list |
| New tokens | `{{Link:<label>}}` resolves any entry in the brand's document-links list; new tokens for RedmineUrl, department emails |
| Token cheat sheet | Collapsible panel in Create/Edit template views lists every valid token with one-click Insert |
| Plain-text copy | Preview page gains a "Copy as Plain Text" button for agents who need unformatted text |
| Seed data | Seeded brands updated with realistic placeholder values for all new fields |

---

## Step 1 — Fix nav sidebar so CS Agents see Email Templates

**File:** `Views/Shared/Layouts/_sidebar.cshtml`

- [ ] **1.1** Move the `Email Templates` `<li>` link out of the `@if (User.IsInRole("BrandManager") || User.IsInRole("TeamLeader"))` block
- [ ] **1.2** Place it in the shared section visible to all authenticated users (same section as Updates, Process Templates, My Vault)
- [ ] **1.3** Run `dotnet build` — confirm clean

---

## Step 2 — Expand `Brand` model

**File:** `Unified/Models/EmailTemplates/Brand.cs`

Remove `BrandWebsiteLink` and replace with `BrandLink` (Label + Url).  
Rename `WebsiteLinksJson` → `BrandLinksJson`.  
Add new structured fields.

**Target model:**

```csharp
public class Brand
{
	public int    Id             { get; set; }
	public string Name           { get; set; } = string.Empty;
	public string? LogoUrl       { get; set; }
	public string? PrimaryColour { get; set; }

	// Primary site URL (global / default)
	public string? SiteUrl       { get; set; }

	// Tool URLs
	public string? CrmUrl        { get; set; }
	public string? RedmineUrl    { get; set; }
	public string? QuemetricsUrl { get; set; }

	// Department email addresses
	public string? EmailDealing  { get; set; }
	public string? EmailAml      { get; set; }
	public string? EmailAssign   { get; set; }
	public string? EmailDemo     { get; set; }

	// Labelled document / regional links  (replaces WebsiteLinksJson)
	// JSON list of { Label, Url }
	// e.g. [{"Label":"Bank Details - EN","Url":"https://..."},...]
	public string BrandLinksJson { get; set; } = "[]";
	public List<BrandLink> GetBrandLinks() =>
		JsonSerializer.Deserialize<List<BrandLink>>(BrandLinksJson) ?? new();

	// ZoHo signature
	public string? FooterSignatureHtml { get; set; }
	public string? ZohoSignatureNote   { get; set; }
}

public class BrandLink
{
	public string Label { get; set; } = string.Empty;  // e.g. "Bank Details - EN"
	public string Url   { get; set; } = string.Empty;
}
```

- [ ] **2.1** Update `Brand.cs` as above
- [ ] **2.2** Search for all usages of `GetWebsiteLinks()` and `BrandWebsiteLink` across the solution — update each to `GetBrandLinks()` / `BrandLink`
- [ ] **2.3** Run `dotnet build` — fix any compile errors before continuing

---

## Step 3 — Database migration

- [ ] **3.1** Run:
  ```
  dotnet ef migrations add Phase2B_BrandExpansion --project Unified
  ```
- [ ] **3.2** Open the generated migration file and verify it adds:
  - `SiteUrl`, `RedmineUrl`, `EmailDealing`, `EmailAml`, `EmailAssign`, `EmailDemo` columns
  - Renames (or adds) `BrandLinksJson` (note: EF may add as new column — manually copy data in `Up()` if needed)
- [ ] **3.3** Run:
  ```
  dotnet ef database update --project Unified
  ```
- [ ] **3.4** Run `dotnet build` — confirm clean

---

## Step 4 — Extend `SubstituteTokens` in `EmailTemplateService`

**File:** `Unified/Services/EmailTemplateService.cs`

Replace the current `SubstituteTokens` private method body with the expanded version below.

**Token table:**

| Token | Source |
|-------|--------|
| `{{BrandName}}` | `Brand.Name` |
| `{{SiteUrl}}` | `Brand.SiteUrl` |
| `{{CrmUrl}}` | `Brand.CrmUrl` |
| `{{RedmineUrl}}` | `Brand.RedmineUrl` |
| `{{QuemetricsUrl}}` | `Brand.QuemetricsUrl` |
| `{{Email:Dealing}}` | `Brand.EmailDealing` |
| `{{Email:AML}}` | `Brand.EmailAml` |
| `{{Email:Assign}}` | `Brand.EmailAssign` |
| `{{Email:Demo}}` | `Brand.EmailDemo` |
| `{{FooterSignature}}` | `Brand.FooterSignatureHtml` |
| `{{ZohoSignature}}` | `Brand.ZohoSignatureNote` |
| `{{Link:<label>}}` | Lookup `BrandLinksJson` by `Label` (case-insensitive) |

The `{{Link:...}}` pattern uses a `Regex.Replace` so any label stored in `BrandLinksJson` resolves automatically — no code change needed when new document types are added.

- [ ] **4.1** Update `SubstituteTokens` to handle all tokens above
- [ ] **4.2** Keep backward-compat aliases: `{{WebsiteUrl}}` → first link in `BrandLinksJson`, `{{CallSystemUrl}}` → `QuemetricsUrl`
- [ ] **4.3** Run `dotnet build` — confirm clean

---

## Step 5 — Update Brand form UI

**File:** `Views/EmailTemplates/_BrandForm.cshtml`

Replace the current form partial with structured sections:

### Section A — Identity
- Brand Name (required)
- Logo URL
- Primary Colour (colour picker)

### Section B — Site & Tool URLs
- Site URL (`SiteUrl`) — primary / global website
- CRM URL (`CrmUrl`)
- Redmine URL (`RedmineUrl`)
- Quemetrics URL (`QuemetricsUrl`)

### Section C — Department Emails
- Dealing (`EmailDealing`)
- AML Team (`EmailAml`)
- Assign (`EmailAssign`)
- Demo (`EmailDemo`)

### Section D — Document / Regional Links (replaces raw JSON textarea)
A dynamic JS table with **Add Row** / **Remove** buttons.  
Hidden `<textarea name="BrandLinksJson" id="brandLinksJson">` is kept in sync by JS.

**Default rows pre-populated when creating a new brand:**
```
Bank Details - EN
Bank Details - PT
FNS
FNS - PT
JAF
DOA
Joint FNS
FATCA
BOR
Corporate FNS
```

Each row has: **Label** text input | **URL** url input | **Remove** button  
**Add Row** button appends a blank row.  
JS serialises the table to JSON on form submit.

### Section E — Signatures
- Footer Signature HTML (textarea)
- ZoHo Signature Notes (textarea)

- [ ] **5.1** Rewrite `_BrandForm.cshtml` with the five sections above
- [ ] **5.2** Add the JS table ↔ JSON serialisation logic in a `@section Scripts` block
- [ ] **5.3** Verify Create Brand and Edit Brand pages still render correctly
- [ ] **5.4** Run `dotnet build` — confirm clean

---

## Step 6 — Token Reference panel in Create / Edit Template views

**Files:** `Views/EmailTemplates/Create.cshtml`, `Views/EmailTemplates/Edit.cshtml`

Add a collapsible **"Available Tokens"** card alongside the Quill editor.

**Groups:**

| Group | Tokens |
|-------|--------|
| General | `{{BrandName}}`, `{{SiteUrl}}`, `{{FooterSignature}}`, `{{ZohoSignature}}` |
| Tool URLs | `{{CrmUrl}}`, `{{RedmineUrl}}`, `{{QuemetricsUrl}}` |
| Department Emails | `{{Email:Dealing}}`, `{{Email:AML}}`, `{{Email:Assign}}`, `{{Email:Demo}}` |
| Document Links | `{{Link:Bank Details - EN}}`, `{{Link:Bank Details - PT}}`, `{{Link:FNS}}`, `{{Link:FNS - PT}}`, `{{Link:JAF}}`, `{{Link:DOA}}`, `{{Link:Joint FNS}}`, `{{Link:FATCA}}`, `{{Link:BOR}}`, `{{Link:Corporate FNS}}` |

Each token has a **Copy token** button that inserts the token string at the current Quill cursor position (or copies to clipboard as fallback).

- [ ] **6.1** Add the token panel to `Create.cshtml`
- [ ] **6.2** Add the token panel to `Edit.cshtml`
- [ ] **6.3** Wire up Insert-at-cursor JS for Quill
- [ ] **6.4** Run `dotnet build` — confirm clean

---

## Step 7 — Preview page: plain-text copy button

**File:** `Views/EmailTemplates/Preview.cshtml`

- [ ] **7.1** Add a **"Copy as Plain Text"** button next to the existing "Copy Email Body (HTML)" button
- [ ] **7.2** JS implementation: render `renderedHtml` into a hidden off-screen `<div>`, read `innerText`, copy to clipboard
- [ ] **7.3** Same flash-feedback pattern as the HTML copy button
- [ ] **7.4** Run `dotnet build` — confirm clean

---

## Step 8 — Update seed data

**File:** `Unified/Data/SeedData.cs`

Update the seeded brands to populate all new fields with realistic placeholder values:

| Field | Colbari example | BullFX example |
|-------|----------------|----------------|
| `SiteUrl` | `https://colbari.com` | `https://bullfx.com` |
| `CrmUrl` | `https://crm.colbari.local` | `https://crm.bullfx.local` |
| `RedmineUrl` | `https://redmine.colbari.local` | `https://redmine.bullfx.local` |
| `QuemetricsUrl` | `https://quemetrics.colbari.local` | `https://quemetrics.bullfx.local` |
| `EmailDealing` | `dealing@colbari.com` | `dealing@bullfx.com` |
| `EmailAml` | `aml@colbari.com` | `aml@bullfx.com` |
| `EmailAssign` | `assign@colbari.com` | `assign@bullfx.com` |
| `EmailDemo` | `demo@colbari.com` | `demo@bullfx.com` |
| `BrandLinksJson` | Full JSON with Bank Details EN/PT, FNS, JAF, DOA, etc. | Same structure |

Also update any seeded `EmailTemplate` bodies to use the new `{{Link:...}}` token format where applicable.

- [ ] **8.1** Update seeded brands with all new fields
- [ ] **8.2** Update seeded email template bodies if they reference old `{{WebsiteUrl:...}}` tokens
- [ ] **8.3** Run `dotnet build` — confirm clean
- [ ] **8.4** Drop and re-seed the dev database, confirm no runtime errors

---

## Step 9 — Smoke test

- [ ] **9.1** Log in as `agent01@unified.local` / `Unified@1234!`
- [ ] **9.2** Confirm "Email Templates" is visible in the sidebar nav
- [ ] **9.3** Open a template, select a brand — confirm `{{Link:Bank Details - EN}}` resolves to the correct URL
- [ ] **9.4** Click "Copy Email Body (HTML)" — paste into a text editor and confirm HTML is correct
- [ ] **9.5** Click "Copy as Plain Text" — confirm clean readable text with no HTML tags
- [ ] **9.6** Log in as `leader1@unified.local` — confirm Create / Edit / Delete buttons visible
- [ ] **9.7** Open Brand Manager, edit a brand — confirm all five form sections render and save correctly
- [ ] **9.8** Run `dotnet build` one final time — confirm clean

---

## Phase 2B Status: `[ ] IN PROGRESS` → `[ ] DONE`
