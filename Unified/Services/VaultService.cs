using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Identity;
using Unified.Models.Vault;

namespace Unified.Services;

public class VaultService
{
    private readonly AppDbContext    _db;
    private readonly IDataProtector  _protector;

    public VaultService(AppDbContext db, IDataProtectionProvider dpProvider)
    {
        _db       = db;
        _protector = dpProvider.CreateProtector("Unified.Vault.v1");
    }

    // ── Categories ────────────────────────────────────────────────────────

    public async Task<List<VaultCategory>> GetCategoriesAsync(bool includeCustom = true)
    {
        var query = _db.VaultCategories.AsQueryable();
        if (!includeCustom) query = query.Where(c => !c.IsCustom);
        return await query.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<VaultCategory> CreateCustomCategoryAsync(
        string name, string icon, string createdByUserId, bool isSystemWide = false)
    {
        var cat = new VaultCategory
        {
            Name            = name,
            IconCssClass    = icon,
            IsCustom        = !isSystemWide,
            CreatedByUserId = isSystemWide ? null : createdByUserId
        };
        _db.VaultCategories.Add(cat);
        await _db.SaveChangesAsync();
        return cat;
    }

    public async Task DeleteCategoryAsync(int id)
    {
        var cat = await _db.VaultCategories.FindAsync(id);
        if (cat is null) return;
        _db.VaultCategories.Remove(cat);
        await _db.SaveChangesAsync();
    }

    // ── Vault entries ─────────────────────────────────────────────────────

    /// <summary>Returns all entries for a user — passwords are NOT decrypted.</summary>
    public async Task<List<VaultEntry>> GetVaultForUserAsync(string userId)
        => await _db.VaultEntries
            .Include(e => e.Category)
            .Where(e => e.OwnerId == userId)
            .OrderBy(e => e.Category!.Name).ThenBy(e => e.Label)
            .ToListAsync();

    /// <summary>Returns a single entry and decrypts the password.
    /// Throws if the requesting user is not the owner (unless BrandManager/TL scope is pre-checked).</summary>
    public async Task<(VaultEntry Entry, string PlainPassword)> GetEntryDecryptedAsync(
        int entryId, string requestingUserId)
    {
        var entry = await _db.VaultEntries
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.Id == entryId)
            ?? throw new KeyNotFoundException("Vault entry not found.");

        if (entry.OwnerId != requestingUserId)
            throw new UnauthorizedAccessException("You can only access your own vault entries.");

        var plain = _protector.Unprotect(entry.EncryptedPassword);
        return (entry, plain);
    }

    public async Task<VaultEntry> UpsertEntryAsync(
        VaultEntry entry, string plainPassword, string requestingUserId)
    {
        entry.EncryptedPassword = _protector.Protect(plainPassword);
        entry.OwnerId           = requestingUserId;

        if (entry.Id == 0)
        {
            entry.CreatedAt = DateTime.UtcNow;
            entry.UpdatedAt = DateTime.UtcNow;
            _db.VaultEntries.Add(entry);
        }
        else
        {
            var existing = await _db.VaultEntries.FindAsync(entry.Id)
                ?? throw new KeyNotFoundException("Entry not found.");
            if (existing.OwnerId != requestingUserId)
                throw new UnauthorizedAccessException("You can only edit your own entries.");

            existing.Label             = entry.Label;
            existing.Username          = entry.Username;
            existing.EncryptedPassword = entry.EncryptedPassword;
            existing.Url               = entry.Url;
            existing.Notes             = entry.Notes;
            existing.CategoryId        = entry.CategoryId;
            existing.UpdatedAt         = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task DeleteEntryAsync(int entryId, string requestingUserId, bool isBrandManager = false)
    {
        var entry = await _db.VaultEntries.FindAsync(entryId)
            ?? throw new KeyNotFoundException("Entry not found.");

        if (!isBrandManager && entry.OwnerId != requestingUserId)
            throw new UnauthorizedAccessException("You can only delete your own entries.");

        _db.VaultEntries.Remove(entry);
        await _db.SaveChangesAsync();
    }

    // ── Bulk operations ───────────────────────────────────────────────────

    public async Task BulkProvisionAsync(
        int categoryId, string label, string username, string plainPassword,
        string? url, string? notes, IEnumerable<string> targetUserIds,
        string provisionedByUserId)
    {
        var encrypted = _protector.Protect(plainPassword);
        var now       = DateTime.UtcNow;

        foreach (var uid in targetUserIds.Distinct())
        {
            var existing = await _db.VaultEntries.FirstOrDefaultAsync(
                e => e.OwnerId == uid && e.CategoryId == categoryId && e.Label == label);

            if (existing is not null)
            {
                existing.Username          = username;
                existing.EncryptedPassword = encrypted;
                existing.Url               = url;
                existing.Notes             = notes;
                existing.UpdatedAt         = now;
                existing.ProvisionedByUserId = provisionedByUserId;
            }
            else
            {
                _db.VaultEntries.Add(new VaultEntry
                {
                    OwnerId              = uid,
                    CategoryId           = categoryId,
                    Label                = label,
                    Username             = username,
                    EncryptedPassword    = encrypted,
                    Url                  = url,
                    Notes                = notes,
                    CreatedAt            = now,
                    UpdatedAt            = now,
                    ProvisionedByUserId  = provisionedByUserId
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task BulkUpdatePasswordAsync(
        int categoryId, string label, string newPlainPassword, IEnumerable<string> targetUserIds)
    {
        var encrypted = _protector.Protect(newPlainPassword);
        var now       = DateTime.UtcNow;

        var entries = await _db.VaultEntries
            .Where(e => e.CategoryId == categoryId && e.Label == label &&
                        targetUserIds.Contains(e.OwnerId))
            .ToListAsync();

        foreach (var e in entries)
        {
            e.EncryptedPassword = encrypted;
            e.UpdatedAt         = now;
        }

        await _db.SaveChangesAsync();
    }

    // ── Audit log ─────────────────────────────────────────────────────────

    public async Task LogAccessAsync(string userId, int entryId, string action)
    {
        _db.VaultAccessLogs.Add(new VaultAccessLog
        {
            UserId     = userId,
            EntryId    = entryId,
            AccessedAt = DateTime.UtcNow,
            Action     = action
        });
        await _db.SaveChangesAsync();
    }

    public async Task<List<VaultAccessLog>> GetAccessLogsAsync(
        string? userId = null, int? entryId = null)
    {
        var query = _db.VaultAccessLogs
            .Include(l => l.User)
            .Include(l => l.Entry)
            .AsQueryable();

        if (userId  is not null) query = query.Where(l => l.UserId  == userId);
        if (entryId is not null) query = query.Where(l => l.EntryId == entryId);

        return await query.OrderByDescending(l => l.AccessedAt).Take(500).ToListAsync();
    }
}
