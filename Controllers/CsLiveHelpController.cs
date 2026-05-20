using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.CsLiveHelp;
using Unified.Models.Identity;
using Unified.Services;

namespace Unified.Controllers;

[Authorize(Roles = $"{Roles.AccountManager},{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
public class CsLiveHelpController : Controller
{
    private readonly CsLiveHelpService    _svc;
    private readonly AppDbContext         _db;
    private readonly UserManager<AppUser> _users;

    public CsLiveHelpController(CsLiveHelpService svc, AppDbContext db, UserManager<AppUser> users)
    {
        _svc   = svc;
        _db    = db;
        _users = users;
    }

    // ── GET /CsLiveHelp/Requests — AM Kanban (own cards + read-only Others) ──

    [Authorize(Roles = Roles.AccountManager)]
    public async Task<IActionResult> Requests()
    {
        var amId = _users.GetUserId(User)!;

        var own    = await _svc.GetAmRequestsAsync(amId);
        var others = await _svc.GetOtherAmOpenRequestsAsync(amId);
        var types  = await _svc.GetRequestTypesAsync();
        // AMs are external users — they are not in AgentBrands, so show all active brands
        var brands = await _db.Brands
            .OrderBy(b => b.Name)
            .ToListAsync();

        ViewBag.OwnRequests   = own;
        ViewBag.OtherRequests = others;
        ViewBag.RequestTypes  = types;
        ViewBag.Brands        = brands;
        return View();
    }

    // ── POST /CsLiveHelp/CreateRequest ────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.AccountManager)]
    public async Task<IActionResult> CreateRequest(int brandId, int requestTypeId, string? customDescription)
    {
        var amId = _users.GetUserId(User)!;

        if (await _svc.IsRateLimitedAsync(amId))
        {
            TempData["Error"] = "Too many requests. Please wait a moment and try again.";
            return RedirectToAction(nameof(Requests));
        }

        // Validate the brand exists (AMs are external – not in AgentBrands)
        var brandExists = await _db.Brands.AnyAsync(b => b.Id == brandId);
        if (!brandExists) return BadRequest();

        var type = await _db.CsRequestTypes.FindAsync(requestTypeId);
        if (type is null) return BadRequest();

        if (type.IsOther)
        {
            if (string.IsNullOrWhiteSpace(customDescription))
            {
                TempData["Error"] = "A description is required for 'Other' request type.";
                return RedirectToAction(nameof(Requests));
            }
            if (customDescription.Length > 500)
            {
                TempData["Error"] = "Description must be 500 characters or fewer.";
                return RedirectToAction(nameof(Requests));
            }
            // English-only check: reject if any non-Latin characters found
            if (Regex.IsMatch(customDescription, @"[^\u0000-\u007F]"))
            {
                TempData["Error"] = "Description must be written in English only.";
                return RedirectToAction(nameof(Requests));
            }
        }

        var req = await _svc.CreateRequestAsync(amId, brandId, requestTypeId, customDescription);
        await _svc.AuditAsync(amId, "CreateRequest", req.Id, GetClientIp());

        TempData["Success"] = "Request submitted.";
        return RedirectToAction(nameof(Requests));
    }

    // ── POST /CsLiveHelp/EditRequest/{id} ─────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.AccountManager)]
    public async Task<IActionResult> EditRequest(int id, int brandId, int requestTypeId, string? customDescription)
    {
        var amId = _users.GetUserId(User)!;

        if (await _svc.IsRateLimitedAsync(amId))
        {
            TempData["Error"] = "Too many requests. Please wait a moment and try again.";
            return RedirectToAction(nameof(Requests));
        }

        var brandExists = await _db.Brands.AnyAsync(b => b.Id == brandId);
        if (!brandExists) return BadRequest();

        var type = await _db.CsRequestTypes.FindAsync(requestTypeId);
        if (type is null) return BadRequest();

        if (type.IsOther)
        {
            if (string.IsNullOrWhiteSpace(customDescription))
            {
                TempData["Error"] = "A description is required for 'Other' request type.";
                return RedirectToAction(nameof(Requests));
            }
            if (customDescription.Length > 500)
            {
                TempData["Error"] = "Description must be 500 characters or fewer.";
                return RedirectToAction(nameof(Requests));
            }
            if (Regex.IsMatch(customDescription, @"[^\u0000-\u007F]"))
            {
                TempData["Error"] = "Description must be written in English only.";
                return RedirectToAction(nameof(Requests));
            }
        }

        var ok = await _svc.EditRequestAsync(id, amId, brandId, requestTypeId, customDescription);
        if (!ok) return Forbid();

        await _svc.AuditAsync(amId, "EditRequest", id, GetClientIp());
        TempData["Success"] = "Request updated.";
        return RedirectToAction(nameof(Requests));
    }

    // ── POST /CsLiveHelp/DeleteRequest/{id} ───────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.AccountManager)]
    public async Task<IActionResult> DeleteRequest(int id)
    {
        var amId = _users.GetUserId(User)!;

        if (await _svc.IsRateLimitedAsync(amId))
        {
            TempData["Error"] = "Too many requests. Please wait a moment and try again.";
            return RedirectToAction(nameof(Requests));
        }

        var ok = await _svc.DeleteRequestAsync(id, amId);
        if (!ok) return Forbid();

        await _svc.AuditAsync(amId, "DeleteRequest", id, GetClientIp());
        TempData["Success"] = "Request deleted.";
        return RedirectToAction(nameof(Requests));
    }

    // ── POST /CsLiveHelp/AddComment/{id} ──────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.AccountManager)]
    public async Task<IActionResult> AddComment(int id, string body)
    {
        var amId = _users.GetUserId(User)!;

        if (await _svc.IsRateLimitedAsync(amId))
        {
            TempData["Error"] = "Too many requests. Please wait a moment and try again.";
            return RedirectToAction(nameof(Requests));
        }

        if (string.IsNullOrWhiteSpace(body) || body.Length > 1000)
        {
            TempData["Error"] = "Comment must be between 1 and 1000 characters.";
            return RedirectToAction(nameof(Requests));
        }

        var ok = await _svc.AddCommentAsync(id, amId, body);
        if (!ok) return Forbid();

        await _svc.AuditAsync(amId, "AddComment", id, GetClientIp());
        TempData["Success"] = "Comment added.";
        return RedirectToAction(nameof(Requests));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private string GetClientIp()
        => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    // ─────────────────────────────────────────────────────────────────────
    // CS AGENT ACTIONS  (CSAgent | TeamLeader | BrandManager | SwissArmyKnife)
    // ─────────────────────────────────────────────────────────────────────

    private static readonly string[] CsRoles =
    [
        Roles.CSAgent, Roles.TeamLeader, Roles.BrandManager, Roles.SwissArmyKnife
    ];

    // ── GET /CsLiveHelp/Board ─────────────────────────────────────────────

    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> Board()
    {
        var requests = await _svc.GetBoardRequestsAsync();
        ViewBag.Requests = requests;
        return View();
    }

    // ── POST /CsLiveHelp/UpdateStatus/{id} ────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> UpdateStatus(int id, CsRequestStatus status)
    {
        var ok = await _svc.UpdateStatusAsync(id, status);
        if (!ok) return NotFound();

        var csId = _users.GetUserId(User)!;
        await _svc.AuditAsync(csId, $"UpdateStatus:{status}", id, GetClientIp());
        TempData["Success"] = $"Card status updated to {status}.";
        return RedirectToAction(nameof(Board));
    }

    // ── POST /CsLiveHelp/Escalate/{id} ────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> Escalate(int id)
    {
        var ok = await _svc.UpdateStatusAsync(id, CsRequestStatus.Escalated);
        if (!ok) return NotFound();

        var csId = _users.GetUserId(User)!;
        await _svc.CsAddCommentAsync(id, csId, "Card escalated.", isSystem: true);
        await _svc.AuditAsync(csId, "Escalate", id, GetClientIp());
        TempData["Success"] = "Card escalated.";
        return RedirectToAction(nameof(Board));
    }

    // ── POST /CsLiveHelp/CsAddComment/{id} ────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> CsAddComment(int id, string body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length > 1000)
        {
            TempData["Error"] = "Comment must be between 1 and 1000 characters.";
            return RedirectToAction(nameof(Board));
        }

        var csId = _users.GetUserId(User)!;
        var ok   = await _svc.CsAddCommentAsync(id, csId, body);
        if (!ok) return NotFound();

        await _svc.AuditAsync(csId, "CsAddComment", id, GetClientIp());
        TempData["Success"] = "Comment added.";
        return RedirectToAction(nameof(Board));
    }

    // ── POST /CsLiveHelp/SendReset/{id} — smart action ────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> SendReset(int id)
    {
        var csId = _users.GetUserId(User)!;
        await _svc.CsAddCommentAsync(id, csId, "Password reset to Aa123456", isSystem: true);
        await _svc.UpdateStatusAsync(id, CsRequestStatus.Completed);
        await _svc.AuditAsync(csId, "SendReset", id, GetClientIp());
        TempData["Success"] = "Password reset comment posted and card completed.";
        return RedirectToAction(nameof(Board));
    }

    // ── POST /CsLiveHelp/MarkPassed/{id} — smart action ──────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> MarkPassed(int id)
    {
        var csId = _users.GetUserId(User)!;
        await _svc.CsAddCommentAsync(id, csId, "Passed to relevant agents", isSystem: true);
        await _svc.UpdateStatusAsync(id, CsRequestStatus.Completed);
        await _svc.AuditAsync(csId, "MarkPassed", id, GetClientIp());
        TempData["Success"] = "Card marked as passed to relevant agents.";
        return RedirectToAction(nameof(Board));
    }
}
