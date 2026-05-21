# CS Live Help stress test

Use the stress utility to measure write-path and render capacity for:
- `POST /CsLiveHelp/CreateRequest`
- `POST /CsLiveHelp/EditRequest`
- `POST /CsLiveHelp/AddComment`
- `GET /CsLiveHelp/Requests`

## 1) Prerequisites
1. Run the Unified app.
2. Sign in as an Account Manager in browser.
3. Open browser dev tools and copy the full `Cookie` request header for `GET /CsLiveHelp/Requests`.

## 2) Validate session and parsing
```powershell
 dotnet run --project tools/CsLiveHelpStress -- --base-url https://localhost:5001 --cookie ".AspNetCore.Identity.Application=<...>; .AspNetCore.Antiforgery.<...>=<...>" --validate-only true
```

## 3) Run staged capacity tests
### Realistic baseline (closest to normal usage)
```powershell
 dotnet run --project tools/CsLiveHelpStress -- --base-url https://localhost:5001 --cookie "<cookie header>" --profile realistic --workers 2 --duration-min 10
```

### Elevated stress
```powershell
 dotnet run --project tools/CsLiveHelpStress -- --base-url https://localhost:5001 --cookie "<cookie header>" --profile stress --workers 8 --duration-min 15
```

### Breakpoint search
```powershell
 dotnet run --project tools/CsLiveHelpStress -- --base-url https://localhost:5001 --cookie "<cookie header>" --profile breakpoint --workers 16 --duration-min 20
```

## 4) Capacity interpretation
Use the summary output and track these thresholds:
- Keep `p95` for writes under your acceptable SLA (for example <2s).
- Ensure failed operations stay near 0%.
- Note first worker level where error rate rises or p95 sharply increases.

For your operations model, treat the safe limit as the highest worker level where 2 people can still complete request flow within ~2-3 minutes under sustained load.
