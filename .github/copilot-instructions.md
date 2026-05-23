# Copilot Instructions

## Scope
- Work only within this repository and the user’s explicit request.
- Keep changes small, direct, and limited to the requested task.
- Do not expand into unrelated refactors, upgrades, or cleanup unless explicitly asked.

## Intent
- Deliver the minimum correct outcome with clear, concise communication.
- Follow existing project patterns and conventions.
- If anything is ambiguous, stop and ask for input.

## User Interface Preferences
- Design chat UI/UX to resemble modern messengers like Google Chat or Telegram (layout and behavior).
- Create a dedicated 'Chat' sidebar category with recent chats, plus New DM/New Group actions.
- Include a modern app-like chat experience with a full emoji picker (for messages and reactions), GIF search/send, and Ctrl+V screenshot paste upload.
- Simplify the chat composer by removing the manual GIF URL field, ensuring Emoji/GIF buttons work, and supporting fast screenshot send flow via Ctrl+V then Enter.

## Execution Phases (Strict)
Agents may execute only one phase at a time.

### Phase 1: Understand
- Confirm the requested outcome and required files.
- Gather only the minimum context needed.
- If unsure, prompt for input.

### Phase 2: Plan
- Provide a short, concrete plan for the requested change only.
- Do not implement in this phase.
- Do not move forward until explicitly told.

### Phase 3: Implement
- Apply only the approved plan.
- Keep edits minimal and scoped.
- Do not proceed to validation/reporting unless explicitly told.

### Phase 4: Validate
- Run relevant checks/tests for touched areas only.
- Report concise pass/fail results.
- Do not start additional improvements.

### Phase 5: Report
- Summarize changes, validation results, and remaining risks/questions.
- Stop after reporting.

## Hard Rules
- MUST NOT move to another phase without explicit user instruction.
- If uncertain at any step, pause and ask for input.
- Do not infer permission to continue from prior messages.
- Keep responses short and action-focused.
