# CS Live Help — Workflow Simulation: Teardown Guide

This document lists **every change** introduced by the demo simulation feature so it can be removed cleanly when it is no longer needed.

---

## What was added / changed

### 1. `Controllers/CsLiveHelpController.cs`

#### Constructor
`IServiceScopeFactory scopeFactory` was added as an injected parameter and stored as `_scopeFactory`.

**To revert:** remove the parameter from the constructor signature and the `_scopeFactory` field declaration.

#### Simulation region (appended after `CardModalsPartial`)

| Symbol | Type | Purpose |
|---|---|---|
| `SimPrefix` | `private const string` | Identifies demo records (`"DEMO-SIM-"`) |
| `_simCts` | `private static CancellationTokenSource?` | Holds running loop state |
| `_simLock` | `private static readonly object` | Thread-safety for CTS access |
| `SimScripts` | `private static readonly (…)[]` | 8 scripted scenarios |
| `SimulationStatus` | `GET /CsLiveHelp/SimulationStatus` | Returns `{ running: bool }` JSON |
| `StartSimulation` | `POST /CsLiveHelp/StartSimulation` | Starts background loop, returns immediately |
| `StopSimulation` | `POST /CsLiveHelp/StopSimulation` | Cancels loop |
| `RunSimLoopAsync` | `private static async Task` | The continuous background worker |
| `CleanupSimulation` | `POST /CsLiveHelp/CleanupSimulation` | Stops loop + deletes all DEMO-SIM- records |

All actions are guarded by `[Authorize(Roles = "TeamLeader,BrandManager,SwissArmyKnife")]`.

**To remove:** delete everything from the comment block starting with  
`// DEMO SIMULATION  (TeamLeader | BrandManager...)`  
through the closing `}` of `CleanupSimulation` (the file's final `}`), plus the constructor parameter and field described above.

---

### 2. `Views/CsLiveHelp/Board.cshtml`

Three blocks were added or modified:

#### a) Header bar
The `<div class="d-flex align-items-center mb-3">` block was changed to include a role-gated **Demo** toggle button (`#simPanelToggle`) instead of the old inline Run/Cleanup buttons.

**To remove:** delete the `@if (User.IsInRole(...))` block wrapping the `<button id="simPanelToggle">`.

#### b) Collapsible simulation panel (`#simPanel`)
Added directly below the header bar — a `card border-info` div containing:
- Status badge (`#simStatusBadge`)
- Start / Stop / Cleanup buttons
- Dark terminal-style live log (`#simLog`, 140 px, monospace)

**To remove:** delete the entire `@* ── SIMULATION PANEL ... *@` block (the `@if` guard and the `<div id="simPanel">` inside it).

#### c) Simulation JS block (`@* ── SIMULATION JS ── *@`)
Added just before `@section Scripts {` — a role-gated `<script>` block (~110 lines) that handles panel toggle, Start/Stop/Cleanup fetch POSTs, status polling, and `SimulationStep` SignalR listener via `window.csHub`.

**To remove:** delete the entire `@* ── SIMULATION JS ── *@` block.

---

### 3. `wwwroot/js/cslivehelp.js`

One line added immediately after the `connection` variable declaration:

```js
window.csHub = connection;
```

**To remove:** delete that single line.

---

### 4. `Hubs/CsLiveHelpHub.cs`

Doc comment updated to include:

```
///   SimulationStep     { message }  — demo simulation progress (cs-board group only)
```

**To remove:** delete that doc-comment line.

---

## How to identify simulation data in the database

All demo records have `ClientId` starting with `"DEMO-SIM-"`.

```sql
SELECT * FROM CsRequests WHERE ClientId LIKE 'DEMO-SIM-%';
```

To delete manually (if `CleanupSimulation` was not used):

```sql
DELETE FROM CsRequestComments
WHERE RequestId IN (SELECT Id FROM CsRequests WHERE ClientId LIKE 'DEMO-SIM-%');

DELETE FROM CsRequests WHERE ClientId LIKE 'DEMO-SIM-%';
```

---

## Files changed — summary

| File | Change type |
|---|---|
| `Controllers/CsLiveHelpController.cs` | Constructor parameter + ~200 lines appended |
| `Views/CsLiveHelp/Board.cshtml` | Header, panel block, and JS block replaced/added |
| `wwwroot/js/cslivehelp.js` | 1 line added (`window.csHub = connection`) |
| `Hubs/CsLiveHelpHub.cs` | 1 doc-comment line added |
| `SIMULATION-TEARDOWN.md` | **New file — delete this file too** |

No database migrations were created. No existing controller logic was modified.
