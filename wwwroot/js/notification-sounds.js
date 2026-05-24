/**
 * Notification Sounds Utility Module
 * Handles audio playback for notifications with browser compatibility and error handling
 */

const NotificationSounds = (() => {
    let audioContext = null;
    let audioBuffer = null;
    let sourceCache = new Map();
    let isInitialized = false;

    const SOUND_FILES = {
        notification: '/assets/audio/perfect-beauty.mp3',
        comment: '/assets/audio/perfect-beauty.mp3',
        mention: '/assets/audio/perfect-beauty.mp3',
        newMessage: '/assets/audio/perfect-beauty.mp3'
    };

    const SOUND_SETTINGS = {
        volume: 0.5,
        maxConcurrentSounds: 3,
        soundDebounceMs: 500
    };

    let lastSoundTime = 0;
    let soundQueue = [];

    /**
     * Initialize AudioContext on first user interaction
     * Browser autoplay policy requires user gesture
     */
    function initializeAudioContext() {
        if (isInitialized) return;

        try {
            // Try different constructors for cross-browser support
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) {
                console.warn('AudioContext not supported in this browser');
                return false;
            }

            audioContext = new AudioContext();
            isInitialized = true;
            console.log('AudioContext initialized');
            return true;
        } catch (error) {
            console.error('Failed to initialize AudioContext:', error);
            return false;
        }
    }

    /**
     * Resume AudioContext if suspended (required by some browsers)
     */
    function resumeAudioContext() {
        if (!audioContext || audioContext.state === 'running') return;

        try {
            if (audioContext.state === 'suspended') {
                audioContext.resume().then(() => {
                    console.log('AudioContext resumed');
                }).catch(error => {
                    console.error('Failed to resume AudioContext:', error);
                });
            }
        } catch (error) {
            console.error('Error resuming AudioContext:', error);
        }
    }

    /**
     * Fetch and cache audio file as ArrayBuffer
     */
    async function loadAudioBuffer(soundType = 'notification') {
        if (audioBuffer) return audioBuffer;

        const soundUrl = SOUND_FILES[soundType] || SOUND_FILES.notification;

        try {
            const response = await fetch(soundUrl);
            if (!response.ok) {
                throw new Error(`Failed to fetch audio: ${response.status}`);
            }

            const arrayBuffer = await response.arrayBuffer();

            // Decode audio data
            if (!audioContext) {
                console.warn('AudioContext not initialized');
                return null;
            }

            audioBuffer = await audioContext.decodeAudioData(arrayBuffer);
            console.log('Audio buffer loaded and decoded');
            return audioBuffer;
        } catch (error) {
            console.error('Failed to load audio buffer:', error);
            return null;
        }
    }

    /**
     * Play sound with debouncing to prevent rapid sound spam
     */
    async function playSound(soundType = 'notification', options = {}) {
        const now = Date.now();
        const timeSinceLastSound = now - lastSoundTime;

        // Debounce: don't play sounds too frequently
        if (timeSinceLastSound < SOUND_SETTINGS.soundDebounceMs) {
            console.log('Sound debounced - too soon since last sound');
            return false;
        }

        // Initialize on first interaction
        if (!isInitialized) {
            initializeAudioContext();
        }

        if (!audioContext) {
            console.warn('AudioContext not available');
            return false;
        }

        // Resume if suspended
        resumeAudioContext();

        try {
            // Load audio buffer if needed
            if (!audioBuffer) {
                audioBuffer = await loadAudioBuffer(soundType);
            }

            if (!audioBuffer) {
                console.warn('Audio buffer not available');
                return false;
            }

            // Create audio source
            const source = audioContext.createBufferSource();
            source.buffer = audioBuffer;

            // Create gain node for volume control
            const gainNode = audioContext.createGain();
            gainNode.gain.value = options.volume !== undefined ? options.volume : SOUND_SETTINGS.volume;

            // Connect nodes
            source.connect(gainNode);
            gainNode.connect(audioContext.destination);

            // Play sound
            source.start(0);

            lastSoundTime = now;

            // Log playback
            console.log(`Playing notification sound: ${soundType}`);
            return true;
        } catch (error) {
            console.error('Failed to play sound:', error);
            return false;
        }
    }

    /**
     * Fallback: Play sound using HTML5 Audio element
     * Used if Web Audio API is not available
     */
    function playSoundFallback(soundType = 'notification', options = {}) {
        const now = Date.now();
        if (now - lastSoundTime < SOUND_SETTINGS.soundDebounceMs) {
            console.log('Sound fallback debounced');
            return false;
        }

        try {
            const soundUrl = SOUND_FILES[soundType] || SOUND_FILES.notification;
            const audio = new Audio(soundUrl);
            audio.volume = options.volume !== undefined ? options.volume : SOUND_SETTINGS.volume;
            audio.play().catch(error => {
                console.warn('Fallback audio playback failed:', error);
            });
            lastSoundTime = now;
            return true;
        } catch (error) {
            console.error('Fallback audio playback error:', error);
            return false;
        }
    }

    /**
     * Public API: Play notification sound
     * Automatically handles initialization and fallbacks
     */
    async function notify(soundType = 'notification', options = {}) {
        try {
            // Try Web Audio API first
            const played = await playSound(soundType, options);
            if (!played && audioContext === null) {
                // Fallback to HTML5 Audio
                return playSoundFallback(soundType, options);
            }
            return played;
        } catch (error) {
            console.error('Notification sound error:', error);
            // Try fallback
            return playSoundFallback(soundType, options);
        }
    }

    /**
     * Enable audio notifications on first user interaction
     * Call this from click handlers or gesture events
     */
    function enableOnFirstInteraction() {
        if (!isInitialized) {
            initializeAudioContext();
        }
    }

    /**
     * Set volume for all sounds (0.0 - 1.0)
     */
    function setVolume(volume) {
        SOUND_SETTINGS.volume = Math.max(0, Math.min(1, volume));
    }

    /**
     * Get current volume
     */
    function getVolume() {
        return SOUND_SETTINGS.volume;
    }

    /**
     * Check if audio is supported
     */
    function isAudioSupported() {
        return !!((window.AudioContext || window.webkitAudioContext) || (typeof Audio !== 'undefined'));
    }

    /**
     * Get audio context state
     */
    function getAudioContextState() {
        if (!audioContext) return 'not-initialized';
        return audioContext.state;
    }

    // Public API
    return {
        notify,
        enableOnFirstInteraction,
        setVolume,
        getVolume,
        isAudioSupported,
        getAudioContextState,
        SOUND_TYPES: Object.keys(SOUND_FILES)
    };
})();

// Auto-enable on first user interaction (document-wide)
document.addEventListener('click', () => {
    NotificationSounds.enableOnFirstInteraction();
}, { once: true });

document.addEventListener('keydown', () => {
    NotificationSounds.enableOnFirstInteraction();
}, { once: true });

document.addEventListener('touchstart', () => {
    NotificationSounds.enableOnFirstInteraction();
}, { once: true });
