# End-to-End Testing Guide: Audio & Real-Time Messaging

## Overview
This guide walks through comprehensive testing of the audio notification system and real-time messaging fixes implemented in this session.

## Prerequisites
- Two browser instances (Chrome/Edge recommended for better dev tools)
- Two user accounts with different roles (e.g., CS Agent and Team Lead/Manager)
- Access to the application running locally or on a test server
- Visual Studio open with debug output visible (or browser console open)

---

## Test Scenario 1: Audio Permission & Initialization

### Test 1.1: Permission Modal Display
**Steps:**
1. Open the application in a private/incognito window to clear localStorage
2. Navigate to the CS Messaging page
3. **Expected:** After ~2 seconds, a modal should appear asking to enable notification sounds

**Verification:**
- Modal title: "Enable Notification Sounds"
- Contains information about browser autoplay policy
- Two buttons: "Later" and "Enable Audio"

### Test 1.2: Permission Grant & LocalStorage
**Steps:**
1. Click "Enable Audio" in the permission modal
2. Open browser DevTools (F12) → Console
3. Type: `localStorage.getItem('audio-permission-granted')`
4. **Expected:** Should return `"true"`

**Verification:**
- Checkbox in modal closes/dismisses
- localStorage key is set

### Test 1.3: Header Audio Status Indicator
**Steps:**
1. After granting permission, look at the notification bell area in the header
2. There should be a small badge next to or near the bell
3. **Expected:** Badge should show "Audio On" with a speaker icon (green)

**Verification:**
- Badge is visible near message dropdown
- Icon shows speaker/volume icon
- Badge is green (success color)

### Test 1.4: Permission Suppression on Reload
**Steps:**
1. Close the permission modal or grant permission
2. Refresh the page (F5)
3. **Expected:** Permission modal should NOT appear again for 2+ seconds (only once on first load)

**Verification:**
- Modal does not re-appear if permission was granted
- Modal does appear if permission was denied in previous session

---

## Test Scenario 2: Notification Settings UI

### Test 2.1: Access Notification Settings Tab
**Steps:**
1. Navigate to `/Profile/Edit`
2. Look for a button labeled "Notification Settings" in the page header
3. Click on it
4. **Expected:** Should navigate to `/Profile/Notifications`

**Verification:**
- URL changes to /Profile/Notifications
- Page displays notification preferences form

### Test 2.2: Audio Settings Controls
**Steps:**
1. On the Notifications page, look for the "Audio Notifications" card
2. **Expected:** Should see:
   - Toggle for "Enable Audio Notifications" 
   - Volume slider (0-100%)
   - "Test Sound" button
   - Status display

**Verification:**
- Toggle checkbox is functional
- Slider moves smoothly and displays percentage
- Test Sound button is clickable

### Test 2.3: Audio Settings Persistence
**Steps:**
1. Turn off "Enable Audio Notifications" toggle
2. See status message "Preference saved successfully"
3. Refresh page (F5)
4. **Expected:** Toggle should remain OFF

**Verification:**
- Toggle state persists after refresh
- Setting is saved to database

### Test 2.4: Volume Control
**Steps:**
1. Open browser DevTools → Network tab
2. Adjust the volume slider to 75%
3. Watch for API call to `/api/notification-preferences/user-settings`
4. See response body should include `"audioVolume":75`

**Verification:**
- API call is made with POST method
- Response includes updated volume value
- Status message shows "Preference saved successfully"

### Test 2.5: Visual & Toast Notification Toggles
**Steps:**
1. Scroll down to "Visual Notifications" section
2. Look for toggles: Desktop, Toast, Badge
3. Toggle each one individually
4. **Expected:** Each should trigger an API save

**Verification:**
- Three separate toggles are present
- Each has descriptive label
- API calls are made for each change

### Test 2.6: Notification Type Filters
**Steps:**
1. Scroll down to "Notification Types" section
2. Look for toggles: Messages, Mentions, System Alerts
3. Toggle each one
4. **Expected:** Each should trigger API save

**Verification:**
- Three separate toggles for notification types
- Descriptions explain what each controls
- Toggling updates database

### Test 2.7: Test Sound Button
**Steps:**
1. Ensure audio is enabled in browser
2. Click "Test Sound" button
3. **Expected:** Should hear a notification sound

**Verification:**
- Sound plays (or notification that sound played)
- Status message shows "Sound test played successfully"

---

## Test Scenario 3: Real-Time Message Delivery (Critical Fix)

### Test 3.1: Message Appears Without Refresh
**Setup:**
- Two browser windows with different users (User A and User B)
- Both users in the same CS Messaging conversation
- Both have browser console visible (F12)

**Steps:**
1. In User A's browser: Open a conversation and view the message thread
2. In User B's browser: Send a message to User A
3. In User A's browser: **Do NOT refresh the page**
4. **Expected:** Message should appear in real-time

**Verification:**
- Message appears in message thread without refresh
- Console shows: `[SignalR] MessageAdded event received:`
- Conversation list updates in real-time
- Message is from User B's ID

### Test 3.2: Conversation List Order Updates in Real-Time
**Setup:**
- Three conversations in conversation list
- User A viewing conversation #1 (oldest)
- User B will send message to conversation #2 (middle)

**Steps:**
1. Note the order of conversations in User A's list
2. In User B's browser: Send a message to conversation #2
3. In User A's browser: Without refreshing, conversation #2 should move to top
4. **Expected:** Conversation order changes without refresh

**Verification:**
- Conversation list re-orders automatically
- Conversation #2 is now at the top
- Message preview shows the new message
- "Last Message At" timestamp is current

### Test 3.3: Multiple Recipients Get Message in Real-Time
**Setup:**
- Three users in same conversation
- All three have the conversation open

**Steps:**
1. User A sends a message
2. **Expected:** Message appears in all three browsers in real-time

**Verification:**
- All three see the message instantly
- No refresh required for any user
- Unread counts update correctly

### Test 3.4: Manager Receives Direct Messages in Real-Time
**Setup:**
- User A is a CS Agent
- User B is a Team Lead/Manager
- User A has open a conversation with User B
- User B has multiple conversations open (not necessarily viewing the one with User A)

**Steps:**
1. In User A's browser: Send a message to User B in their current conversation
2. In User B's browser: Do NOT navigate to that conversation
3. **Expected:** Message should still appear in User B's conversation list in real-time

**Verification:**
- Console shows: `[SignalR] Updating conversation list for conv-{id}`
- Conversation appears/updates in list without User B clicking on it
- Unread count increases

### Test 3.5: SignalR Connection Auto-Join Verification
**Setup:**
- Browser DevTools open with Network tab watching for SignalR activity
- Application just loaded

**Steps:**
1. Refresh CS Messaging page
2. In browser console, run:
   ```javascript
   console.log('SignalR state:', connection.state);
   ```
3. **Expected:** Should show `"Connected"`

**Verification:**
- Connection state is "Connected"
- No errors in console about connection

---

## Test Scenario 4: Notification Sounds & Visuals

### Test 4.1: Sound Plays on New Message (Audio Enabled)
**Setup:**
- Audio permission granted
- Notification settings: Audio enabled, volume 50%
- User viewing different conversation

**Steps:**
1. User B sends message to User A's current conversation
2. User A is NOT viewing that conversation
3. **Expected:** Hear a notification sound

**Verification:**
- Sound plays
- Console shows: `[SignalR] Rendering notification for incoming message`
- No errors in audio playback

### Test 4.2: No Sound When Audio Disabled
**Setup:**
- Turn OFF "Audio Notifications" in Settings
- Volume set to 100% (to verify it's not just volume)

**Steps:**
1. Refresh page
2. User B sends message to User A
3. **Expected:** NO sound should play

**Verification:**
- No audio plays
- Console shows notification was received but audio was skipped
- Toast/visual notification may still appear

### Test 4.3: Volume Control Affects Sound Level
**Setup:**
- Three test states: 25%, 50%, 75%

**Steps:**
1. Set volume to 25%
2. Trigger notification sound (Test Sound button)
3. Note the volume
4. Set volume to 75%
5. Trigger notification sound again
6. **Expected:** Second sound should be noticeably louder

**Verification:**
- Volume audibly increases with slider value
- Test sound works at different volumes

### Test 4.4: Visual Glow Effect on Conversation
**Setup:**
- User viewing different conversation (not the one receiving message)
- Notification settings: Visual enabled

**Steps:**
1. User B sends message to User A in conversation #2
2. User A is viewing conversation #3
3. **Expected:** Conversation #2 card/item should flash/glow briefly

**Verification:**
- Visual effect appears on the conversation card
- Glow animates for ~3 seconds then fades
- Doesn't interfere with user's current work

### Test 4.5: Toast Notification Display
**Setup:**
- Notification settings: Toast enabled
- User NOT viewing the conversation

**Steps:**
1. Message arrives from another user
2. **Expected:** Toast appears in corner of screen

**Verification:**
- Toast notification appears (typically bottom-right)
- Shows message preview
- Auto-dismisses after ~5 seconds
- Doesn't block page content

### Test 4.6: Bell Badge Count Updates
**Setup:**
- Look at notification bell icon in header
- Badge shows current notification count

**Steps:**
1. Receive first message (not viewing that conversation)
2. Badge should show "1"
3. Receive second message (different conversation)
4. **Expected:** Badge updates to "2"

**Verification:**
- Badge appears when notifications arrive
- Count increments for each new notification
- Badge uses appropriate color (warning/danger)

---

## Test Scenario 5: Notification Type Filtering

### Test 5.1: Message Notifications Can Be Toggled
**Setup:**
- Turn OFF "Messages" in Notification Types
- Turn ON "Mentions"

**Steps:**
1. User B sends regular message (no mention) to User A
2. **Expected:** No notification

**Verification:**
- No sound plays
- No toast appears
- No badge update
- Console may show message was muted by preference

### Test 5.2: Mention Notifications Work When Enabled
**Setup:**
- Turn ON "Mentions" in Notification Types
- Turn OFF "Messages"

**Steps:**
1. User B sends message with @mention to User A
2. **Expected:** Notification triggers

**Verification:**
- Sound plays
- Toast shows
- Console indicates notification was sent for mention type

---

## Test Scenario 6: Settings Persistence Across Sessions

### Test 6.1: Settings Survive Page Reload
**Steps:**
1. Set all preferences to specific values:
   - Audio: ON, Volume: 75%
   - Desktop: OFF
   - Toast: ON
   - Badge: OFF
   - Messages: ON, Mentions: OFF, System: ON
2. Refresh page (F5)
3. Navigate back to Notifications page
4. **Expected:** All settings should be exactly as set

**Verification:**
- Each toggle/slider shows previously set value
- No settings were reset

### Test 6.2: Settings Survive User Logout/Login
**Steps:**
1. Set preferences as in 6.1
2. Log out
3. Log back in as same user
4. Navigate to Notifications page
5. **Expected:** All settings preserved

**Verification:**
- All toggles/sliders show correct values
- Settings persisted in database

---

## Test Scenario 7: Error Handling

### Test 7.1: API Failure Handling
**Setup:**
- DevTools open with Network tab
- Throttle network to "Slow 3G" (DevTools Network settings)

**Steps:**
1. Try to toggle a preference
2. Observe network request
3. **Expected:** Should handle gracefully

**Verification:**
- Error message displays: "Error saving preference" or similar
- UI doesn't break
- User can retry

### Test 7.2: Connection Recovery
**Setup:**
- DevTools Network tab open
- Find SignalR connection requests

**Steps:**
1. Disconnect internet briefly (Dev Tools → Network conditions → Offline)
2. Wait 5 seconds
3. Turn internet back on
4. **Expected:** Connection should reconnect and resume receiving messages

**Verification:**
- Console shows reconnection attempt
- Connection state returns to "Connected"
- New messages still arrive in real-time

---

## Test Scenario 8: Browser Compatibility

### Test 8.1: Chrome/Edge (Chromium)
**Steps:**
1. Open application in Chrome or Edge
2. Run through Scenario 1-5
3. **Expected:** All features work

### Test 8.2: Firefox
**Steps:**
1. Open application in Firefox
2. Run through Scenario 1-5
3. **Expected:** All features work

### Test 8.3: Safari (Mac/iOS)
**Steps:**
1. Open application in Safari
2. Run through Scenario 1-5
3. **Expected:** All features work (may have different audio handling)

---

## Troubleshooting Checklist

If tests fail, check:

- [ ] Database migration applied: `Update-Database` in Package Manager Console
- [ ] AudioPermissionUI.js is loaded: Check Network tab for file
- [ ] NotificationManager.js is loaded: Check Network tab for file
- [ ] Audio file exists: `/wwwroot/assets/audio/perfect-beauty.mp3`
- [ ] SignalR hub is accessible: Check `/hubs/cs-messaging` in Network
- [ ] User settings exist in database: Check `UserNotificationSettings` table
- [ ] Browser console for errors: Press F12, look for red errors
- [ ] User has CsMessaging permission: Check user roles

---

## Console Commands for Manual Testing

```javascript
// Check audio permission status
AudioPermissionUI.isPermissionGranted();

// Check if audio is fully enabled
AudioPermissionUI.isAudioEnabled();

// Get user settings
AudioPermissionUI.getUserSettings();

// Test sound immediately
AudioPermissionUI.testAudio();

// Check recent notifications
NotificationManager.getRecentNotifications();

// Check mute status
NotificationManager.isMuted('CsMessaging', conversationId);

// Manually trigger notification
NotificationManager.handleNotification({
	type: 'newMessage',
	contextType: 'CsMessaging',
	contextId: '1',
	title: 'Test Message',
	message: 'This is a test',
	sound: true,
	visual: true,
	toast: true
});

// Check SignalR connection
connection?.state;

// Force refresh conversations
refreshConversations();
```

---

## Success Criteria

All tests pass when:

1. ✅ Audio permission modal appears on first visit
2. ✅ Audio can be toggled and volume adjusted in settings
3. ✅ Messages appear in real-time without refresh
4. ✅ Notification sounds play with correct volume
5. ✅ Settings persist across page reloads and sessions
6. ✅ Conversation list updates automatically in real-time
7. ✅ Managers receive messages in real-time even when not viewing that conversation
8. ✅ Error handling works gracefully
9. ✅ Works on all major browsers

---

## Notes

- The audio file is located at `/wwwroot/assets/audio/perfect-beauty.mp3`
- User settings are stored in the `UserNotificationSettings` table
- Context-based muting uses the `NotificationPreferences` table
- SignalR groups are managed by `CsMessagingHub` with auto-join on connect
- LocalStorage is used for immediate permission state, database for preferences
