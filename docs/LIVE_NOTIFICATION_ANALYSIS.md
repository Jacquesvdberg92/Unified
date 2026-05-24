# Unified Live Messaging Notification Analysis

## Objective
Determine why CS messaging notifications are not delivered live across Unified and instead require a page refresh, then outline a practical remediation plan for reliable visual and audio notifications from anywhere in the app.

## Executive Summary
The current implementation is only partially real-time.

SignalR is configured on the server and the CS messaging page does establish a live hub connection, but that connection only exists while the user is on the messaging screen. On all other pages, the notification stack is loaded but passive: there is no shared SignalR client listening for message events, so no live event reaches the browser. As a result, users only see updated notification state after navigating back to messaging or refreshing a page that re-fetches data.

This is the primary failure.

There are also secondary gaps:
- notification-specific hub methods exist but are not part of the active send pipeline,
- the current "desktop notifications" setting does not create browser desktop notifications,
- audio playback is not globally warmed up for autoplay-restricted browsers,
- the shared header bell updates only from local in-page state and not from a unified server-driven unread source.

## High-Confidence Root Cause
### 1. The real-time connection is page-scoped, not app-scoped
The messaging client exits immediately unless the messaging page container exists:
- `wwwroot/js/cs-messaging.js:4-5`

That means this entire file, including the SignalR connection and message event handlers, only runs on the messaging page.

The hub connection itself is created inside that same page-specific file:
- `wwwroot/js/cs-messaging.js:983-989`

The message notification trigger is also only inside that page-specific handler:
- `wwwroot/js/cs-messaging.js:992-1035`

Impact:
- Users on `/CsMessaging` can receive live events.
- Users on any other page do not connect to `/hubs/cs-messaging`.
- No connection means no push event, no sound, no toast, no visual update, and no live bell update.

### 2. SignalR is only loaded on the messaging view
The SignalR browser script is loaded only in the messaging page:
- `Views/CsMessaging/Index.cshtml:720-721`

The shared layout loads notification helper scripts globally, but not SignalR and not a shared notification hub client:
- `Views/Shared/Layouts/_scripts.cshtml:27-34`

Impact:
- Notification scripts are present everywhere.
- The transport they need for real-time message delivery is not.

### 3. Server broadcasts are real-time, but optimized for conversation pages rather than global notifications
The controller broadcasts:
- `MessageAdded` to `conv-{id}` groups: `Controllers/CsMessagingController.cs:135-139`
- `ConversationChanged` to the global `cs-messaging` group: `Controllers/CsMessagingController.cs:143-145`

The hub automatically joins the connected user to all active conversation groups on connect:
- `Hubs/CsMessagingHub.cs:19-27`
- `Hubs/CsMessagingHub.cs:33-57`

This is good server-side groundwork, but it only helps if the browser is connected in the first place.

### 4. Notification-specific hub methods exist, but are not wired into the active message send flow
The hub defines methods for:
- `NotifyNewMessage`: `Hubs/CsMessagingHub.cs:68-78`
- `NotifyMention`: `Hubs/CsMessagingHub.cs:83-94`
- `NotifyDirectMessage`: `Hubs/CsMessagingHub.cs:99-108`
- `NotifyGroupMessage`: `Hubs/CsMessagingHub.cs:113-124`

Current findings indicate these methods are not part of the controller/service path used when a message is posted. The active flow relies on `MessageAdded` plus `ConversationChanged` instead.

Impact:
- The codebase contains the beginnings of a richer notification model.
- The actual runtime path does not use it.
- Mention-specific and direct-message-specific delivery behavior is therefore incomplete.

### 5. "Desktop notifications" currently means in-page visuals, not OS/browser notifications
The preferences page exposes a setting called **Desktop Notifications**:
- `Views/Profile/Notifications.cshtml:92-105`

But in the current manager, that flag gates `showVisualIndicators(...)`, which only updates DOM elements and page indicators:
- `wwwroot/js/notification-manager.js:219-221`
- `wwwroot/js/notification-manager.js:263-330`

There is no actual use of the browser Notification API (`new Notification(...)`) in the inspected workspace.

Impact:
- The UI promises desktop notifications.
- The implementation does not deliver true desktop notifications.
- Users may believe notifications are enabled while the app only does on-page highlighting.

### 6. Audio notifications are not globally initialized for autoplay-restricted browsers
The sound utility correctly notes that user interaction is required:
- `wwwroot/js/notification-sounds.js:28-31`

It provides `enableOnFirstInteraction()`:
- `wwwroot/js/notification-sounds.js:213-220`

But current findings show no app-wide wiring that calls this method from a shared user interaction path.

Impact:
- Sound may work inconsistently depending on browser state.
- Even after a live event is received, audio may still fail unless the browser has already permitted playback.

### 7. Header bell state is driven by local client state, not a canonical unread pipeline
The global header has a bell container and unread label:
- `Views/Shared/Layouts/_header.cshtml:147-163`

The notification manager updates that bell from its own recent-notification array:
- `wwwroot/js/notification-manager.js:335-373`

Because recent notifications are only added when `handleNotification(...)` is called, and that currently happens from the messaging page handler, the header bell cannot remain accurate app-wide.

Impact:
- The bell can look correct on the messaging page and stale elsewhere.
- Refreshes appear to "fix" the state because server-rendered page data is reloaded.

## Supporting Findings
### Server-side foundations that are already good
- SignalR is registered and the hub is mapped:
  - `Program.cs:22-25`
  - `Program.cs:146-147`
- The hub joins users to all active conversations on connect:
  - `Hubs/CsMessagingHub.cs:19-27`
  - `Hubs/CsMessagingHub.cs:40-49`
- The service maintains unread/read data through `LastReadAt`:
  - `Services/CsMessagingService.cs:51-90`
  - `Services/CsMessagingService.cs:319-327`

These pieces mean the issue is not that the app lacks real-time infrastructure entirely. The failure is mostly in how the client integration is scoped.

### Preferences infrastructure is present but only partially aligned with behavior
Global notification settings exist and persist server-side:
- `Controllers/Api/NotificationPreferencesController.cs:336-446`
- `Models/CsLiveHelp/UserNotificationSettings.cs:15-52`

This is useful and should be preserved, but the runtime behavior needs to match the names and expectations of these settings.

### Mention pipeline appears incomplete
The messaging page supports composing mentions in the UI:
- `wwwroot/js/cs-messaging.js:833-897`

However, no corresponding inspected server-side logic was found that parses mentions during message send and emits mention-targeted live events. The service method that saves messages does not appear to implement mention extraction or mention delivery:
- `Services/CsMessagingService.cs:222-269`

Impact:
- Mention preferences exist.
- Mention UI exists.
- Mention-specific live notifications do not appear fully implemented end-to-end.

## Why Users Currently Need a Refresh
Users need a refresh because the app is mixing:
1. **server push on one page**, with
2. **server-rendered pull / REST refresh on other pages**.

When a user leaves the messaging page:
- there is no active messaging hub connection,
- no live event is received,
- notification manager never gets called,
- the header bell and visual state do not change.

A page refresh re-runs server-side rendering and fetch logic, which makes the user finally see updated state.

## Recommended Solution Architecture
## Option A - Recommended: Shared app-wide notification client
Create a global notification client that starts from the shared layout for authenticated users.

### What it should do
1. Load SignalR globally from the shared layout.
2. Start a shared hub connection on every authenticated page.
3. Listen for messaging events regardless of current page.
4. Route events into `NotificationManager` for sound, toast, badge, and visual handling.
5. If the current page is `/CsMessaging`, let the chat page keep its detailed rendering behavior.
6. If the current page is not `/CsMessaging`, still surface lightweight notifications and update the global bell/unread state.

### Likely implementation shape
- Add a new shared script such as `wwwroot/js/live-notifications.js`
- Load it from `Views/Shared/Layouts/_scripts.cshtml`
- Load SignalR globally in the shared layout/scripts partial instead of only in `Views/CsMessaging/Index.cshtml`
- Keep `wwwroot/js/cs-messaging.js` focused on page rendering and conversation UX
- Move shared real-time connection ownership out of `cs-messaging.js`

### Benefits
- Solves the main defect directly.
- Keeps messaging-page behavior rich while enabling notifications anywhere.
- Minimal architectural disruption compared with a full redesign.

## Option B - Stronger long-term model: Dedicated notification hub/event contract
Instead of reusing chat-page events for global notifications, create a dedicated notification stream.

### What it should do
- Emit normalized events like:
  - `notification:new-message`
  - `notification:direct-message`
  - `notification:mention`
  - `notification:conversation-updated`
- Deliver them to users, not just conversation groups.
- Include enough metadata for global notification UX without requiring a full conversation reload.

### Benefits
- Cleaner separation of concerns.
- Easier to support future system alerts, workflow alerts, and cross-module notifications.
- Better for telemetry and testing.

### Drawback
- More refactoring than Option A.

## Recommended Remediation Plan
### Phase 1 - Stabilize app-wide live delivery
1. Load the SignalR browser client globally from shared layout/scripts.
2. Add a shared notification bootstrap script for authenticated pages.
3. Start one app-wide connection to `/hubs/cs-messaging`.
4. Subscribe globally to message-related events.
5. Route incoming events to `NotificationManager.handleNotification(...)`.
6. Update the global header bell and unread summary in real time.

### Phase 2 - Separate page rendering from global notifications
1. Remove ownership of the only hub connection from `wwwroot/js/cs-messaging.js`.
2. Either:
   - reuse the shared connection from a global singleton, or
   - have the messaging page register only page-specific handlers against the shared client.
3. Keep message thread rendering local to the messaging page.
4. Keep cross-app notification behavior global.

### Phase 3 - Fix semantics of visual and desktop notifications
1. Implement actual browser Notification API support.
2. Ask permission only after user interaction and only when enabled in preferences.
3. Keep toasts as an in-app fallback even if desktop notifications are denied.
4. Rename UI labels if behavior remains in-app only.

### Phase 4 - Make audio reliable
1. Call `NotificationSounds.enableOnFirstInteraction()` from a shared interaction bootstrap.
2. Warm the audio system on first click/keydown/touch for authenticated users.
3. Respect `AudioNotificationsEnabled` and `AudioVolume` from server settings.
4. Add throttling rules so bursts do not create audio spam.

### Phase 5 - Complete direct-message and mention targeting
1. Parse outgoing messages for mentions on the server.
2. Emit mention-targeted events only to the mentioned users.
3. Use direct user-targeted notifications for direct conversations.
4. Continue using conversation group events for message thread synchronization.
5. Treat message sync and user notification as related but separate concerns.

### Phase 6 - Canonical unread state
1. Add a lightweight unread summary endpoint or hub event for the header bell.
2. Make header unread count derive from a canonical server truth.
3. Reconcile local optimistic increments with server refresh after reconnect.
4. Ensure reconnect restores accurate unread counts.

### Phase 7 - Reliability and observability
1. Add client logging around connect, reconnect, and handler registration.
2. Add server logging for emitted notification events.
3. Add metrics for connected users, reconnect frequency, and dropped notifications.
4. Validate behavior under multiple tabs, reconnects, and long-lived sessions.

## Concrete File-Level Change Targets
### High priority
- `Views/Shared/Layouts/_scripts.cshtml`
  - Add global SignalR/shared live notification script loading.
- `Views/CsMessaging/Index.cshtml`
  - Remove page-only ownership of shared real-time infrastructure.
- `wwwroot/js/cs-messaging.js`
  - Refactor into page-rendering handlers only, or consume a shared connection.
- `wwwroot/js/notification-manager.js`
  - Add true desktop notification support and clearer event handling boundaries.
- `wwwroot/js/notification-sounds.js`
  - Wire first-interaction enablement globally.
- `Controllers/CsMessagingController.cs`
  - Emit normalized user notification events, not only conversation updates.
- `Hubs/CsMessagingHub.cs`
  - Either reuse existing notification-specific methods properly or simplify and formalize the event contract.

### Medium priority
- `Services/CsMessagingService.cs`
  - Add mention extraction and notification-target discovery.
- `Views/Shared/Layouts/_header.cshtml`
  - Bind bell UI to a real unread source.
- `Views/Profile/Notifications.cshtml`
  - Align wording with actual behavior and add browser-permission guidance if desktop notifications are implemented.

## Risks and Edge Cases
### Browser audio restrictions
Even with a correct live connection, sound can still fail unless audio is enabled after user interaction.

### Duplicate notifications across multiple tabs
If the same user has multiple tabs open, each tab may notify separately. A cross-tab coordination strategy may be needed later using `localStorage`, `BroadcastChannel`, or leader-tab logic.

### Reconnect gaps
During temporary disconnects, unread state may drift. Reconnect should trigger a summary refresh.

### Notification fatigue
Group bursts can overwhelm users. The app should debounce or collapse notifications for high-volume threads.

### Permission mismatch
Desktop notifications require explicit browser permission. The UI should explain that enabling the toggle alone is not enough.

## Validation Plan
### Manual scenarios
1. Open Unified on a non-messaging page.
2. From another user, send a direct message.
3. Confirm the first user receives:
   - live bell update,
   - toast/in-app visual notification,
   - audio notification if enabled,
   - optional desktop notification if allowed.
4. Repeat while the first user remains on:
   - Home
   - Reports
   - Profile
   - CsLiveHelp
5. Repeat with the user on `/CsMessaging` inside the active conversation.
6. Repeat with the user on `/CsMessaging` but a different conversation.
7. Repeat with mentions in group chats.
8. Repeat with two tabs open.
9. Repeat after network reconnect.
10. Repeat after session has been open for an extended period.

### Technical validation
- Confirm shared connection is started once per page load.
- Confirm handlers are not registered twice.
- Confirm reconnect re-subscribes correctly.
- Confirm unread count matches server truth after reconnect.
- Confirm mute/preferences are respected for message, mention, toast, badge, desktop, and sound behaviors.

## Recommended Delivery Order
1. Global SignalR bootstrap
2. Shared live-notification client
3. Header bell/unread real-time updates
4. Audio first-interaction bootstrap
5. True desktop notifications
6. Mention-targeted notifications
7. Telemetry and hardening

## Final Assessment
The issue is not primarily a database or controller persistence problem. The main defect is architectural scoping on the client: the live messaging connection exists only on the messaging page, while the requirement is cross-application notification delivery.

The fastest safe fix is to move real-time connection ownership into the shared layout via a global notification client and let the messaging page consume that shared infrastructure instead of being the only place where it exists.
