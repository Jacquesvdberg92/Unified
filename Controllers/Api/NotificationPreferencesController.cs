using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.CsLiveHelp;
using Unified.Models.Identity;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Unified.Controllers.Api;

/// <summary>
/// API endpoints for managing notification preferences
/// Allows clients to save/retrieve mute settings server-side for persistence across devices
/// </summary>
[Authorize]
[ApiController]
[Route("api/notification-preferences")]
[Route("api/[controller]")]
public class NotificationPreferencesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<NotificationPreferencesController> _logger;

    public NotificationPreferencesController(
        AppDbContext dbContext,
        UserManager<AppUser> userManager,
        ILogger<NotificationPreferencesController> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Get all mute preferences for the current user
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetPreferences()
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var preferences = await _dbContext.NotificationPreferences
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.LastUpdated)
                .ToListAsync();

            return Ok(new { success = true, data = preferences });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification preferences");
            return StatusCode(500, new { success = false, message = "Error retrieving preferences" });
        }
    }

    /// <summary>
    /// Get a specific preference by context
    /// </summary>
    [HttpGet("get/{contextType}/{contextId?}")]
    public async Task<IActionResult> GetPreference(string contextType, string? contextId = "")
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var preference = await _dbContext.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId 
                    && p.ContextType == contextType 
                    && p.ContextId == (contextId ?? ""));

            if (preference == null)
                return Ok(new { success = true, data = (object?)null });

            return Ok(new { success = true, data = preference });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification preference");
            return StatusCode(500, new { success = false, message = "Error retrieving preference" });
        }
    }

    /// <summary>
    /// Set mute preference for a context
    /// </summary>
    [HttpPost("set-mute")]
    public async Task<IActionResult> SetMute([FromBody] SetMuteRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ContextType))
                return BadRequest(new { success = false, message = "ContextType is required" });

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var contextId = request.ContextId ?? "";

            // Find existing preference or create new one
            var preference = await _dbContext.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId 
                    && p.ContextType == request.ContextType 
                    && p.ContextId == contextId);

            if (preference == null)
            {
                preference = new NotificationPreference
                {
                    UserId = userId,
                    ContextType = request.ContextType,
                    ContextId = contextId,
                    IsMuted = request.IsMuted,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };
                _dbContext.NotificationPreferences.Add(preference);
            }
            else
            {
                preference.IsMuted = request.IsMuted;
                preference.LastUpdated = DateTime.UtcNow;
                _dbContext.NotificationPreferences.Update(preference);
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, data = preference });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting notification preference");
            return StatusCode(500, new { success = false, message = "Error saving preference" });
        }
    }

    /// <summary>
    /// Batch set mute preferences
    /// </summary>
    [HttpPost("batch-set-mute")]
    public async Task<IActionResult> BatchSetMute([FromBody] BatchSetMuteRequest request)
    {
        try
        {
            if (request?.Preferences == null || request.Preferences.Count == 0)
                return BadRequest(new { success = false, message = "Preferences array is required" });

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var results = new List<NotificationPreference>();

            foreach (var pref in request.Preferences)
            {
                if (string.IsNullOrEmpty(pref.ContextType))
                    continue;

                var contextId = pref.ContextId ?? "";
                var preference = await _dbContext.NotificationPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId 
                        && p.ContextType == pref.ContextType 
                        && p.ContextId == contextId);

                if (preference == null)
                {
                    preference = new NotificationPreference
                    {
                        UserId = userId,
                        ContextType = pref.ContextType,
                        ContextId = contextId,
                        IsMuted = pref.IsMuted,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };
                    _dbContext.NotificationPreferences.Add(preference);
                }
                else
                {
                    preference.IsMuted = pref.IsMuted;
                    preference.LastUpdated = DateTime.UtcNow;
                    _dbContext.NotificationPreferences.Update(preference);
                }

                results.Add(preference);
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, data = results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch setting notification preferences");
            return StatusCode(500, new { success = false, message = "Error saving preferences" });
        }
    }

    /// <summary>
    /// Toggle mute preference for a context
    /// </summary>
    [HttpPost("toggle-mute")]
    public async Task<IActionResult> ToggleMute([FromBody] ToggleMuteRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ContextType))
                return BadRequest(new { success = false, message = "ContextType is required" });

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var contextId = request.ContextId ?? "";

            var preference = await _dbContext.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId 
                    && p.ContextType == request.ContextType 
                    && p.ContextId == contextId);

            // Determine the new state
            bool newMutedState = preference == null || !preference.IsMuted;

            if (preference == null)
            {
                preference = new NotificationPreference
                {
                    UserId = userId,
                    ContextType = request.ContextType,
                    ContextId = contextId,
                    IsMuted = newMutedState,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };
                _dbContext.NotificationPreferences.Add(preference);
            }
            else
            {
                preference.IsMuted = newMutedState;
                preference.LastUpdated = DateTime.UtcNow;
                _dbContext.NotificationPreferences.Update(preference);
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { 
                success = true, 
                data = preference,
                isMuted = newMutedState
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling notification preference");
            return StatusCode(500, new { success = false, message = "Error toggling preference" });
        }
    }

    /// <summary>
    /// Delete a preference
    /// </summary>
    [HttpDelete("delete/{contextType}/{contextId?}")]
    public async Task<IActionResult> DeletePreference(string contextType, string? contextId = "")
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var preference = await _dbContext.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId 
                    && p.ContextType == contextType 
                    && p.ContextId == (contextId ?? ""));

            if (preference == null)
                return Ok(new { success = true, message = "Preference not found" });

            _dbContext.NotificationPreferences.Remove(preference);
            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, message = "Preference deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification preference");
            return StatusCode(500, new { success = false, message = "Error deleting preference" });
        }
    }

    /// <summary>
    /// Clear all preferences for the current user
    /// </summary>
    [HttpDelete("clear-all")]
    public async Task<IActionResult> ClearAllPreferences()
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var preferences = await _dbContext.NotificationPreferences
                .Where(p => p.UserId == userId)
                .ToListAsync();

            if (preferences.Count == 0)
                return Ok(new { success = true, message = "No preferences to clear" });

            _dbContext.NotificationPreferences.RemoveRange(preferences);
            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, message = $"Cleared {preferences.Count} preferences" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all notification preferences");
            return StatusCode(500, new { success = false, message = "Error clearing preferences" });
        }
    }

    /// <summary>
    /// Get current user's global notification settings
    /// </summary>
    [HttpGet("user-settings")]
    public async Task<IActionResult> GetUserSettings()
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var settings = await _dbContext.UserNotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (settings == null)
            {
                // Create default settings
                settings = new UserNotificationSettings
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };
                _dbContext.UserNotificationSettings.Add(settings);
                await _dbContext.SaveChangesAsync();
            }

            return Ok(new { success = true, data = settings });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user notification settings");
            return StatusCode(500, new { success = false, message = "Error retrieving settings" });
        }
    }

    /// <summary>
    /// Update user's global notification settings
    /// </summary>
    [HttpPost("user-settings")]
    public async Task<IActionResult> UpdateUserSettings([FromBody] Dictionary<string, object> updates)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (updates == null || updates.Count == 0)
                return BadRequest(new { success = false, message = "No updates provided" });

            var settings = await _dbContext.UserNotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (settings == null)
            {
                settings = new UserNotificationSettings { UserId = userId };
                _dbContext.UserNotificationSettings.Add(settings);
            }

            // Update properties based on the dictionary
            foreach (var kvp in updates)
            {
                var prop = typeof(UserNotificationSettings).GetProperty(kvp.Key, 
                    System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        object convertedValue = kvp.Value;

                        // Special handling for boolean values
                        if (prop.PropertyType == typeof(bool))
                        {
                            if (kvp.Value is bool boolVal)
                            {
                                convertedValue = boolVal;
                            }
                            else if (kvp.Value is string strVal)
                            {
                                convertedValue = strVal.Equals("true", StringComparison.OrdinalIgnoreCase);
                            }
                            else if (kvp.Value is JsonElement jsonElement)
                            {
                                convertedValue = jsonElement.ValueKind == JsonValueKind.True;
                            }
                        }
                        else
                        {
                            // Use standard conversion for other types
                            convertedValue = Convert.ChangeType(kvp.Value, prop.PropertyType);
                        }

                        prop.SetValue(settings, convertedValue);
                        _logger.LogInformation($"Updated property {kvp.Key} to {convertedValue}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to convert property {kvp.Key}");
                        return BadRequest(new { success = false, message = $"Failed to set property {kvp.Key}: {ex.Message}" });
                    }
                }
                else
                {
                    _logger.LogWarning($"Property {kvp.Key} not found or not writable");
                }
            }

            settings.LastUpdated = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, data = settings });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user notification settings");
            return StatusCode(500, new { success = false, message = "Error updating settings: " + ex.Message });
        }
    }
}

/// <summary>
/// Request DTOs
/// </summary>
public class SetMuteRequest
{
    public string? ContextType { get; set; }
    public string? ContextId { get; set; }
    public bool IsMuted { get; set; }
}

public class ToggleMuteRequest
{
    public string? ContextType { get; set; }
    public string? ContextId { get; set; }
}

public class BatchSetMuteRequest
{
    public List<PreferenceItem> Preferences { get; set; } = new();

    public class PreferenceItem
    {
        public string? ContextType { get; set; }
        public string? ContextId { get; set; }
        public bool IsMuted { get; set; }
    }
}
