# Notification Sounds - Audio File Configuration Guide

## 📍 Audio File Location

The notification sound file is located at:
```
wwwroot/assets/audio/perfect-beauty.mp3
```

### Full Path
```
C:\Users\jacqu\source\repos\Unified\wwwroot\assets\audio\perfect-beauty.mp3
```

## 🔊 How It Works

### Sound Playback Flow
1. **Web Audio API** - Primary method using the browser's AudioContext for optimal performance
2. **HTML5 Audio Element** - Fallback for browsers that don't support Web Audio API
3. **Autoplay Policy** - Requires user interaction before audio can play (browser security)

### Supported Audio Formats
- **.mp3** (current) - Widely supported across all browsers
- **.wav** - Can be added as additional format
- **.ogg** - Can be added for Vorbis compression support

## 🎵 Where to Manage Notification Sounds

### 1. **JavaScript Configuration** (`wwwroot/js/notification-sounds.js`)

#### Current Sound File Mappings
```javascript
const SOUND_FILES = {
	notification: '/assets/audio/perfect-beauty.mp3',
	comment: '/assets/audio/perfect-beauty.mp3',
	mention: '/assets/audio/perfect-beauty.mp3',
	newMessage: '/assets/audio/perfect-beauty.mp3'
};
```

To use different sounds for different notification types, you can:
- Add new audio files to `wwwroot/assets/audio/`
- Update the SOUND_FILES mapping to point to different files

#### Sound Settings
```javascript
const SOUND_SETTINGS = {
	volume: 0.5,              // Default volume (0.0-1.0)
	maxConcurrentSounds: 3,   // Max simultaneous sounds
	soundDebounceMs: 500      // Minimum time between sounds
};
```

**To adjust:**
- `volume`: Change the decimal (0.5 = 50%). Range is 0.0 to 1.0
- `maxConcurrentSounds`: Limit how many notification sounds can play at once
- `soundDebounceMs`: Prevent sound spam by requiring X milliseconds between sounds

### 2. **Client-Side Notification Manager** (`wwwroot/js/notification-manager.js`)

The NotificationManager controls when sounds play:

```javascript
NotificationManager.handleNotification({
	type: 'comment',           // notification type
	contextType: 'Requests',   // page context
	contextId: '12345',        // specific request/conversation ID
	title: 'New Comment',      // notification title
	message: 'Someone commented on your request',
	sound: true,               // Set to false to disable sound for this notification
	visual: true,              // Show visual indicators
	toast: true                // Show toast popup
});
```

**To disable sound for specific events:**
- Set `sound: false` in the handleNotification call
- Example: In `cslivehelp.js`, set `sound: false` if you don't want sounds there

### 3. **Mute Preferences** (Database + Client)

Users can mute notifications per context:

#### Client-Side (localStorage)
```javascript
// Mute all requests notifications
NotificationManager.mute('Requests', '');

// Mute notifications for a specific request
NotificationManager.mute('Requests', '12345');

// Unmute
NotificationManager.unmute('Requests', '12345');

// Toggle
NotificationManager.toggleMute('CsMessaging', '67890');
```

#### Server-Side Persistence (Database)
- API: `POST /api/notificationpreferences/set-mute`
- Table: `NotificationPreferences` (stores UserId, ContextType, ContextId, IsMuted)
- Auto-synced: Client changes are automatically saved to the database

## 🔧 Customizing Notification Sounds

### To Add a New Sound File

1. **Place the audio file** in `wwwroot/assets/audio/`
   ```
   wwwroot/assets/audio/my-custom-sound.mp3
   ```

2. **Update sound-files mapping** in `wwwroot/js/notification-sounds.js`:
   ```javascript
   const SOUND_FILES = {
	   notification: '/assets/audio/perfect-beauty.mp3',
	   comment: '/assets/audio/comment-sound.mp3',        // NEW
	   mention: '/assets/audio/mention-sound.mp3',        // NEW
	   newMessage: '/assets/audio/perfect-beauty.mp3'
   };
   ```

3. **Set volume for each type** (optional, advanced):
   - Create a `SOUND_VOLUMES` object similar to SOUND_FILES
   - Update `notify()` method to use per-type volumes

### To Adjust Global Volume

Edit `wwwroot/js/notification-sounds.js`:
```javascript
const SOUND_SETTINGS = {
	volume: 0.7,  // Change 0.5 to desired level (0.0-1.0)
	...
};
```

Or programmatically:
```javascript
NotificationSounds.setVolume(0.8);  // 80% volume
console.log(NotificationSounds.getVolume()); // Get current volume
```

## 🎛️ Volume Control UI

Currently, volume is controlled:
1. **Globally** via browser's AudioContext volume
2. **Per-notification** in the handleNotification call
3. **System-wide** via browser settings and OS volume

### To Add User-Facing Volume Slider

Add HTML control (example):
```html
<input type="range" id="notificationVolumeSlider" min="0" max="100" value="50">
```

Add JavaScript handler:
```javascript
document.getElementById('notificationVolumeSlider').addEventListener('change', (e) => {
	const volume = parseInt(e.target.value) / 100;
	NotificationSounds.setVolume(volume);
});
```

## 📊 Notification Preferences API Endpoints

### Get All Preferences
```
GET /api/notificationpreferences/list
```

### Get Single Preference
```
GET /api/notificationpreferences/get/{contextType}/{contextId?}
```

### Mute/Unmute
```
POST /api/notificationpreferences/toggle-mute
Body: { contextType: "Requests", contextId: "12345" }
```

### Set Mute State
```
POST /api/notificationpreferences/set-mute
Body: { contextType: "Requests", contextId: "12345", isMuted: true }
```

### Batch Operations
```
POST /api/notificationpreferences/batch-set-mute
Body: { preferences: [
  { contextType: "Requests", contextId: "123", isMuted: true },
  { contextType: "Board", contextId: "", isMuted: false }
]}
```

### Clear All Preferences
```
DELETE /api/notificationpreferences/clear-all
```

## 🧪 Testing Audio Playback

### Manual Test in Browser Console

```javascript
// Test direct sound playback
NotificationSounds.notify('notification').then(success => {
	console.log('Sound played:', success);
});

// Test with full notification
NotificationManager.handleNotification({
	type: 'comment',
	contextType: 'Requests',
	contextId: '1',
	title: 'Test Notification',
	message: 'Testing audio playback',
	sound: true,
	visual: true,
	toast: true
});

// Check if audio is supported
console.log('Audio supported:', NotificationSounds.isAudioSupported());

// Check audio context status
console.log('Audio context state:', NotificationSounds._audioContextState?.());
```

### Browser Autoplay Policy

⚠️ **Important**: Browsers block audio playback without user interaction:
- First click on the page enables audio playback
- After that, notifications will play automatically
- This is a security feature to prevent malicious auto-playing sounds

## 📝 Troubleshooting

### Sound Not Playing
1. **Check browser console** for errors
2. **Verify file exists** at `wwwroot/assets/audio/perfect-beauty.mp3`
3. **Check browser settings** - audio might be muted
4. **Test AudioContext** - use `NotificationSounds.isAudioSupported()` in console
5. **Check mute preferences** - notification might be muted via UI
6. **Verify user interaction** - audio won't play until user clicks page

### Volume Issues
```javascript
// Reset to default volume
NotificationSounds.setVolume(0.5);

// Check current settings
console.log(NotificationSounds._getVolume?.());
```

### Performance Issues
If sounds are lagging:
1. Reduce `maxConcurrentSounds` from 3 to 1 or 2
2. Increase `soundDebounceMs` from 500 to 1000 (prevents sound spam)
3. Lower volume to reduce processing overhead

---

**Summary**: The notification sound system is fully configured and ready to use. Audio files are managed in `wwwroot/assets/audio/`, settings are in `wwwroot/js/notification-sounds.js`, and user preferences are persisted in the database via the NotificationPreferences API.
