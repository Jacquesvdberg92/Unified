using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Unified.Models.Identity;

namespace Unified.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<ProfileController> _logger;
    private readonly IWebHostEnvironment _webHostEnvironment;

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const string AllowedMimeTypes = "image/jpeg,image/png,image/gif,image/webp";
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public ProfileController(
        UserManager<AppUser> userManager,
        ILogger<ProfileController> logger,
        IWebHostEnvironment webHostEnvironment)
    {
        _userManager = userManager;
        _logger = logger;
        _webHostEnvironment = webHostEnvironment;
    }

    /// <summary>
    /// GET /Profile - View current user's profile
    /// </summary>
    [HttpGet("/Profile")]
    public async Task<IActionResult> View()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound("User not found");
        }

        return View(user);
    }

    /// <summary>
    /// GET /Profile/Edit - Edit current user's profile
    /// </summary>
    [HttpGet("/Profile/Edit")]
    public async Task<IActionResult> Edit()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound("User not found");
        }

        return View(user);
    }

    /// <summary>
    /// GET /Profile/Notifications - View notification preferences
    /// </summary>
    [HttpGet("/Profile/Notifications")]
    public async Task<IActionResult> Notifications()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound("User not found");
        }

        return View(user);
    }

    /// <summary>
    /// POST /Profile/UpdateProfile - Update profile information and avatar
    /// </summary>
    [HttpPost("/Profile/UpdateProfile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(
        [FromForm] string displayName,
        [FromForm] string? anydeskId,
        [FromForm] IFormFile? avatarFile)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound("User not found");
        }

        // Validate inputs
        if (string.IsNullOrWhiteSpace(displayName))
        {
            ModelState.AddModelError("displayName", "Display name is required");
        }

        if (displayName != null && displayName.Length > 100)
        {
            ModelState.AddModelError("displayName", "Display name cannot exceed 100 characters");
        }

        if (anydeskId != null && anydeskId.Length > 50)
        {
            ModelState.AddModelError("anydeskId", "AnyDesk ID cannot exceed 50 characters");
        }

        // Validate avatar file if provided
        if (avatarFile != null)
        {
            if (avatarFile.Length > MaxFileSizeBytes)
            {
                ModelState.AddModelError("avatarFile", "File size cannot exceed 5 MB");
            }

            if (!AllowedMimeTypes.Contains(avatarFile.ContentType))
            {
                ModelState.AddModelError("avatarFile", "Only JPEG, PNG, GIF, and WebP images are allowed");
            }
        }

        if (!ModelState.IsValid)
        {
            return View("Edit", user);
        }

        // Update basic profile info
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            user.DisplayName = displayName;
        }

        user.AnydeskId = string.IsNullOrWhiteSpace(anydeskId) ? null : anydeskId;

        // Handle avatar upload
        if (avatarFile != null && avatarFile.Length > 0)
        {
            try
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "profiles");

                // Create directory if it doesn't exist
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Delete old avatar if it exists
                if (!string.IsNullOrEmpty(user.AvatarUrl))
                {
                    var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, user.AvatarUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                // Generate unique filename
                var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(extension))
                {
                    extension = ".png";
                }

                var fileName = $"{user.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(stream);
                }

                // Update user's avatar URL
                user.AvatarUrl = $"/uploads/profiles/{fileName}";

                _logger.LogInformation("User {UserId} uploaded new profile avatar: {FileName}", user.Id, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading avatar for user {UserId}", user.Id);
                ModelState.AddModelError("avatarFile", "An error occurred while uploading the file. Please try again.");
                return View("Edit", user);
            }
        }

        // Save changes
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View("Edit", user);
        }

        _logger.LogInformation("User {UserId} updated their profile", user.Id);

        TempData["SuccessMessage"] = "Your profile has been updated successfully.";
        return RedirectToAction("View");
    }
}
