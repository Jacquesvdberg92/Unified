using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Hubs;
using Unified.Models.CsLiveHelp;
using Unified.Models.Identity;
using Unified.Services;

namespace Unified.Controllers;

[Authorize(Roles = $"{Roles.AccountManager},{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
public class CsLiveHelpController : Controller
{
    private readonly CsLiveHelpService       _svc;
    private readonly AppDbContext            _db;
    private readonly UserManager<AppUser>    _users;
    private readonly IHubContext<CsLiveHelpHub> _hub;

    public CsLiveHelpController(
        CsLiveHelpService svc,
        AppDbContext db,
        UserManager<AppUser> users,
        IHubContext<CsLiveHelpHub> hub)
    {
        _svc   = svc;
        _db    = db;
        _users = users;
        _hub   = hub;
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
    public async Task<IActionResult> CreateRequest(int brandId, int requestTypeId, string? customDescription, string? clientId)
    {
        var amId = _users.GetUserId(User)!;

        if (await _svc.IsRateLimitedAsync(amId))
        {
            if (Request.Headers.ContainsKey("X-Requested-With"))
                return Json(new { success = false, error = "Too many requests. Please wait a moment and try again." });
            TempData["Error"] = "Too many requests. Please wait a moment and try again.";
            return RedirectToAction(nameof(Requests));
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            if (Request.Headers.ContainsKey("X-Requested-With"))
                return Json(new { success = false, error = "Client ID is required." });
            TempData["Error"] = "Client ID is required.";
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
                if (Request.Headers.ContainsKey("X-Requested-With"))
                    return Json(new { success = false, error = "A description is required for 'Other' request type." });
                TempData["Error"] = "A description is required for 'Other' request type.";
                return RedirectToAction(nameof(Requests));
            }
            if (customDescription.Length > 500)
            {
                if (Request.Headers.ContainsKey("X-Requested-With"))
                    return Json(new { success = false, error = "Description must be 500 characters or fewer." });
                TempData["Error"] = "Description must be 500 characters or fewer.";
                return RedirectToAction(nameof(Requests));
            }
            // English-only check: reject if any non-Latin characters found
            if (Regex.IsMatch(customDescription, @"[^\u0000-\u007F]"))
            {
                if (Request.Headers.ContainsKey("X-Requested-With"))
                    return Json(new { success = false, error = "Description must be written in English only." });
                TempData["Error"] = "Description must be written in English only.";
                return RedirectToAction(nameof(Requests));
            }
        }

        var req = await _svc.CreateRequestAsync(amId, brandId, requestTypeId, customDescription, clientId);
        await _svc.AuditAsync(amId, "CreateRequest", req.Id, GetClientIp());

        // Push real-time event to the AM's own group and to all CS agents
        var brand = await _db.Brands.FindAsync(brandId);
        var rtype = await _db.CsRequestTypes.FindAsync(requestTypeId);
        var payload = new { id = req.Id, brandName = brand?.Name, requestType = rtype?.Name, status = req.Status.ToString(), isInternal = false };
        await _hub.Clients.Group($"am-{amId}").SendAsync("CardAdded", payload);
        await _hub.Clients.Group("cs-board").SendAsync("CardAdded", payload);

        if (Request.Headers.ContainsKey("X-Requested-With"))
            return Json(new { success = true, message = "Request submitted." });

        TempData["Success"] = "Request submitted.";
        return RedirectToAction(nameof(Requests));
    }

    // ── POST /CsLiveHelp/EditRequest/{id} ─────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.AccountManager)]
    public async Task<IActionResult> EditRequest(int id, int brandId, int requestTypeId, string? customDescription, string? clientId)
    {
        var amId = _users.GetUserId(User)!;

        if (await _svc.IsRateLimitedAsync(amId))
        {
            if (Request.Headers.ContainsKey("X-Requested-With"))
                return Json(new { success = false, error = "Too many requests. Please wait a moment and try again." });
            TempData["Error"] = "Too many requests. Please wait a moment and try again.";
            return RedirectToAction(nameof(Requests));
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            if (Request.Headers.ContainsKey("X-Requested-With"))
                return Json(new { success = false, error = "Client ID is required." });
            TempData["Error"] = "Client ID is required.";
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
                if (Request.Headers.ContainsKey("X-Requested-With"))
                    return Json(new { success = false, error = "A description is required for 'Other' request type." });
                TempData["Error"] = "A description is required for 'Other' request type.";
                return RedirectToAction(nameof(Requests));
            }
            if (customDescription.Length > 500)
            {
                if (Request.Headers.ContainsKey("X-Requested-With"))
                    return Json(new { success = false, error = "Description must be 500 characters or fewer." });
                TempData["Error"] = "Description must be 500 characters or fewer.";
                return RedirectToAction(nameof(Requests));
            }
            if (Regex.IsMatch(customDescription, @"[^\u0000-\u007F]"))
            {
                if (Request.Headers.ContainsKey("X-Requested-With"))
                    return Json(new { success = false, error = "Description must be written in English only." });
                TempData["Error"] = "Description must be written in English only.";
                return RedirectToAction(nameof(Requests));
            }
        }

        var ok = await _svc.EditRequestAsync(id, amId, brandId, requestTypeId, customDescription, clientId);
        if (!ok) return Forbid();

        await _svc.AuditAsync(amId, "EditRequest", id, GetClientIp());

        var brand = await _db.Brands.FindAsync(brandId);
        var rtype = await _db.CsRequestTypes.FindAsync(requestTypeId);
        var payload = new { id, brandName = brand?.Name, requestType = rtype?.Name };
        await _hub.Clients.Group($"am-{amId}").SendAsync("CardUpdated", payload);
        await _hub.Clients.Group("cs-board").SendAsync("CardUpdated", payload);

        if (Request.Headers.ContainsKey("X-Requested-With"))
            return Json(new { success = true, message = "Request updated." });

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
            if (Request.Headers.ContainsKey("X-Requested-With"))
                return Json(new { success = false, error = "Too many requests. Please wait a moment and try again." });
            TempData["Error"] = "Too many requests. Please wait a moment and try again.";
            return RedirectToAction(nameof(Requests));
        }

        var ok = await _svc.DeleteRequestAsync(id, amId);
        if (!ok) return Forbid();

        await _svc.AuditAsync(amId, "DeleteRequest", id, GetClientIp());

        await _hub.Clients.Group($"am-{amId}").SendAsync("CardDeleted", new { id });
        await _hub.Clients.Group("cs-board").SendAsync("CardDeleted", new { id });

        if (Request.Headers.ContainsKey("X-Requested-With"))
            return Json(new { success = true, message = "Request deleted." });

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
            if (Request.Headers.ContainsKey("X-Requested-With"))
                return Json(new { success = false, error = "Too many requests. Please wait a moment and try again." });
            TempData["Error"] = "Too many requests. Please wait a moment and try again.";
            return RedirectToAction(nameof(Requests));
        }

        if (string.IsNullOrWhiteSpace(body) || body.Length > 1000)
        {
            if (Request.Headers.ContainsKey("X-Requested-With"))
                return Json(new { success = false, error = "Comment must be between 1 and 1000 characters." });
            TempData["Error"] = "Comment must be between 1 and 1000 characters.";
            return RedirectToAction(nameof(Requests));
        }

        var ok = await _svc.AddCommentAsync(id, amId, body);
        if (!ok) return Forbid();

        await _svc.AuditAsync(amId, "AddComment", id, GetClientIp());

        var author = await _users.GetUserAsync(User);
        await _hub.Clients.Group($"am-{amId}").SendAsync("CommentAdded", new { requestId = id, author = author?.DisplayName ?? amId, body, isSystem = false, createdAt = DateTime.UtcNow });
        await _hub.Clients.Group("cs-board").SendAsync("CommentAdded", new { requestId = id, author = author?.DisplayName ?? amId, body, isSystem = false, createdAt = DateTime.UtcNow });

        if (Request.Headers.ContainsKey("X-Requested-With"))
            return Json(new { success = true, message = "Comment added." });

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
        var teamAllocationTeams = await _db.Teams
            .OrderBy(t => t.Name)
            .ToListAsync();

        ViewBag.Requests = requests;
        ViewBag.TeamAllocationTeams = teamAllocationTeams;
        return View();
    }

    // ── GET /CsLiveHelp/BoardPage — AJAX load-more (returns partial HTML) ──

    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> BoardPage(CsRequestStatus status, int afterId = 0)
    {
        if (!Request.Headers.ContainsKey("X-Requested-With")) return BadRequest();

        var page = await _svc.GetBoardRequestsAsync(afterId, status);
        return PartialView("_CsBoardCardList", page);
    }

    // ── POST /CsLiveHelp/UpdateStatus/{id} ────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> UpdateStatus(int id, CsRequestStatus status)
    {
        var csId = _users.GetUserId(User)!;
        var ok = await _svc.UpdateStatusAsync(id, status, csId);
        if (!ok) return NotFound();

        await _svc.AuditAsync(csId, $"UpdateStatus:{status}", id, GetClientIp());

        var agent = await _users.GetUserAsync(User);
        await _hub.Clients.Group("cs-board").SendAsync("CardStatusChanged", new { id, newStatus = status.ToString(), assignedTo = agent?.DisplayName });

        var req = await _db.CsRequests.FindAsync(id);
        if (req?.AccountManagerId is not null)
            await _hub.Clients.Group($"am-{req.AccountManagerId}").SendAsync("CardStatusChanged", new { id, newStatus = status.ToString(), assignedTo = agent?.DisplayName });

        TempData["Success"] = $"Card status updated to {status}.";
        return RedirectToAction(nameof(Board));
    }

    // ── POST /CsLiveHelp/Escalate/{id} ────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> Escalate(int id, int assignedTeamId)
    {
        var csId = _users.GetUserId(User)!;

        if (assignedTeamId <= 0)
        {
            TempData["Error"] = "Please select a team allocation before passing to CS.";
            return RedirectToAction(nameof(Board));
        }

        var assignedTeam = await _db.Teams.FindAsync(assignedTeamId);
        if (assignedTeam is null)
        {
            TempData["Error"] = "Selected team allocation is invalid.";
            return RedirectToAction(nameof(Board));
        }

        var ok = await _svc.UpdateStatusAsync(id, CsRequestStatus.Escalated, csId);
        if (!ok) return NotFound();

        await _svc.CsAddCommentAsync(id, csId, $"Card escalated. Team allocation: {assignedTeam.Name}.", isSystem: true);
        await _svc.AuditAsync(csId, "Escalate", id, GetClientIp());

        await _hub.Clients.Group("cs-board").SendAsync("CardStatusChanged", new { id, newStatus = "Escalated", assignedTo = assignedTeam.Name });

        var req = await _db.CsRequests.FindAsync(id);
        if (req?.AccountManagerId is not null)
            await _hub.Clients.Group($"am-{req.AccountManagerId}").SendAsync("CardStatusChanged", new { id, newStatus = "Escalated", assignedTo = assignedTeam.Name });

        TempData["Success"] = "Card escalated.";
        return RedirectToAction(nameof(Board));
    }

    // ── POST /CsLiveHelp/UpdateStatusJson/{id} — for drag-drop (fetch) ────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> UpdateStatusJson(int id, CsRequestStatus status)
    {
        var csId = _users.GetUserId(User)!;
        var ok   = await _svc.UpdateStatusAsync(id, status, csId);
        if (!ok) return NotFound();

        await _svc.AuditAsync(csId, $"DragDrop:{status}", id, GetClientIp());

        var agent = await _users.GetUserAsync(User);
        await _hub.Clients.Group("cs-board").SendAsync("CardStatusChanged", new { id, newStatus = status.ToString(), assignedTo = agent?.DisplayName });

        var req = await _db.CsRequests.FindAsync(id);
        if (req?.AccountManagerId is not null)
            await _hub.Clients.Group($"am-{req.AccountManagerId}").SendAsync("CardStatusChanged", new { id, newStatus = status.ToString(), assignedTo = agent?.DisplayName });

        return Json(new { success = true });
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
        var ok   = await _svc.CsAddCommentAsync(id, csId, body, isCsInternalOnly: false);
        if (!ok) return NotFound();

        await _svc.AuditAsync(csId, "CsAddComment", id, GetClientIp());

        var agent = await _users.GetUserAsync(User);
        await _hub.Clients.Group("cs-board").SendAsync("CommentAdded", new { requestId = id, author = agent?.DisplayName ?? csId, body, isSystem = false, createdAt = DateTime.UtcNow });

        var req = await _db.CsRequests.FindAsync(id);
        if (req?.AccountManagerId is not null)
            await _hub.Clients.Group($"am-{req.AccountManagerId}").SendAsync("CommentAdded", new { requestId = id, author = agent?.DisplayName ?? csId, body, isSystem = false, createdAt = DateTime.UtcNow });

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

        var agent = await _users.GetUserAsync(User);
        await _hub.Clients.Group("cs-board").SendAsync("CardStatusChanged", new { id, newStatus = "Completed", assignedTo = agent?.DisplayName });
        await _hub.Clients.Group("cs-board").SendAsync("CommentAdded", new { requestId = id, author = agent?.DisplayName ?? csId, body = "Password reset to Aa123456. <- please note the fullstop is part of the password", isSystem = true, createdAt = DateTime.UtcNow });

        var req = await _db.CsRequests.FindAsync(id);
        if (req?.AccountManagerId is not null)
        {
            await _hub.Clients.Group($"am-{req.AccountManagerId}").SendAsync("CardStatusChanged", new { id, newStatus = "Completed", assignedTo = agent?.DisplayName });
            await _hub.Clients.Group($"am-{req.AccountManagerId}").SendAsync("CommentAdded", new { requestId = id, author = agent?.DisplayName ?? csId, body = "Password reset to Aa123456", isSystem = true, createdAt = DateTime.UtcNow });
        }

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

        var agent = await _users.GetUserAsync(User);
        await _hub.Clients.Group("cs-board").SendAsync("CardStatusChanged", new { id, newStatus = "Completed", assignedTo = agent?.DisplayName });
        await _hub.Clients.Group("cs-board").SendAsync("CommentAdded", new { requestId = id, author = agent?.DisplayName ?? csId, body = "Passed to relevant agents", isSystem = true, createdAt = DateTime.UtcNow });

        var req = await _db.CsRequests.FindAsync(id);
        if (req?.AccountManagerId is not null)
        {
            await _hub.Clients.Group($"am-{req.AccountManagerId}").SendAsync("CardStatusChanged", new { id, newStatus = "Completed", assignedTo = agent?.DisplayName });
            await _hub.Clients.Group($"am-{req.AccountManagerId}").SendAsync("CommentAdded", new { requestId = id, author = agent?.DisplayName ?? csId, body = "Passed to relevant agents", isSystem = true, createdAt = DateTime.UtcNow });
        }

        TempData["Success"] = "Card marked as passed to relevant agents.";
        return RedirectToAction(nameof(Board));
    }

    // ─────────────────────────────────────────────────────────────────────
    // REQUESTS ALL BRANDS — internal CS board (TL/Manager/Agents)
    // ─────────────────────────────────────────────────────────────────────

    private static readonly string[] TlManagerRoles =
    [
        Roles.TeamLeader, Roles.BrandManager, Roles.SwissArmyKnife
    ];

    // ── GET /CsLiveHelp/RequestsAllBrands ────────────────────────────────

    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> RequestsAllBrands(bool escalatedOnly = false)
    {
        var isTlManager = User.IsInRole(Roles.TeamLeader)
                       || User.IsInRole(Roles.BrandManager)
                       || User.IsInRole(Roles.SwissArmyKnife);

        var requests = await _svc.GetAllBrandsRequestsAsync(escalatedOnly);
        var types    = await _svc.GetRequestTypesAsync();
        var brands   = await _db.Brands.OrderBy(b => b.Name).ToListAsync();
        var teams    = await _db.Teams.OrderBy(t => t.Name).ToListAsync();

        ViewBag.Requests      = requests;
        ViewBag.RequestTypes  = types;
        ViewBag.Brands        = brands;
        ViewBag.Teams         = teams;
        ViewBag.EscalatedOnly = escalatedOnly;
        ViewBag.IsTlManager   = isTlManager;
        return View();
    }

    // ── POST /CsLiveHelp/ResolveEscalation/{id} ──────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> ResolveEscalation(int id)
    {
        var csId = _users.GetUserId(User)!;
        var ok   = await _svc.ResolveEscalationAsync(id, csId);
        if (!ok) return NotFound();

        await _svc.AuditAsync(csId, "ResolveEscalation", id, GetClientIp());
        TempData["Success"] = "Escalation resolved.";
        return RedirectToAction(nameof(RequestsAllBrands));
    }

    // ── POST /CsLiveHelp/CreateInternalRequest ───────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> CreateInternalRequest(
        int brandId, int requestTypeId, string? customDescription,
        string? clientId, int? teamId)
    {
        var csId = _users.GetUserId(User)!;

        var brandExists = await _db.Brands.AnyAsync(b => b.Id == brandId);
        if (!brandExists) return BadRequest();

        var type = await _db.CsRequestTypes.FindAsync(requestTypeId);
        if (type is null) return BadRequest();

        if (type.IsOther && string.IsNullOrWhiteSpace(customDescription))
        {
            TempData["Error"] = "A description is required for 'Other' request type.";
            return RedirectToAction(nameof(RequestsAllBrands));
        }

        if (!string.IsNullOrWhiteSpace(customDescription) && customDescription.Length > 500)
        {
            TempData["Error"] = "Description must be 500 characters or fewer.";
            return RedirectToAction(nameof(RequestsAllBrands));
        }

        var req = await _svc.CreateInternalRequestAsync(
            csId, brandId, requestTypeId, customDescription,
            clientId: string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim());
        await _svc.AuditAsync(csId, "CreateInternalRequest", req.Id, GetClientIp());

        // If a team was selected, post it as a system comment so it shows in the thread
        if (teamId.HasValue && teamId.Value > 0)
        {
            var team = await _db.Teams.FindAsync(teamId.Value);
            if (team is not null)
                await _svc.CsAddCommentAsync(req.Id, csId, $"Team allocation: {team.Name}.", isSystem: true);
        }

        var brand = await _db.Brands.FindAsync(brandId);
        var rtype = await _db.CsRequestTypes.FindAsync(requestTypeId);
        await _hub.Clients.Group("cs-board").SendAsync("CardAdded", new { id = req.Id, brandName = brand?.Name, requestType = rtype?.Name, status = "Open", isInternal = true });

        TempData["Success"] = "Internal request created.";
        return RedirectToAction(nameof(RequestsAllBrands));
    }

    // ── POST /CsLiveHelp/InternalAddComment/{id} ─────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> InternalAddComment(int id, string body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length > 1000)
        {
            TempData["Error"] = "Comment must be between 1 and 1000 characters.";
            return RedirectToAction(nameof(RequestsAllBrands));
        }

        var csId = _users.GetUserId(User)!;
        var ok   = await _svc.CsAddCommentAsync(id, csId, body, isCsInternalOnly: true);
        if (!ok) return NotFound();

        await _svc.AuditAsync(csId, "InternalAddComment", id, GetClientIp());

        var agent = await _users.GetUserAsync(User);
        await _hub.Clients.Group("cs-board").SendAsync("CommentAdded", new { requestId = id, author = agent?.DisplayName ?? csId, body, isSystem = false, createdAt = DateTime.UtcNow });

        TempData["Success"] = "Comment added.";
        return RedirectToAction(nameof(RequestsAllBrands));
    }

    // ── POST /CsLiveHelp/InternalUpdateStatusJson/{id} — drag-drop ─────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> InternalUpdateStatusJson(int id, CsRequestStatus status)
    {
        var csId = _users.GetUserId(User)!;
        var ok   = await _svc.UpdateStatusAsync(id, status, csId);
        if (!ok) return NotFound();

        await _svc.AuditAsync(csId, $"InternalDragDrop:{status}", id, GetClientIp());

        var agent = await _users.GetUserAsync(User);
        await _hub.Clients.Group("cs-board").SendAsync("CardStatusChanged",
            new { id, newStatus = status.ToString(), assignedTo = agent?.DisplayName });

        return Json(new { success = true });
    }

    // ── POST /CsLiveHelp/InternalUpdateStatus/{id} ───────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> InternalUpdateStatus(int id, CsRequestStatus status)
    {
        var csId = _users.GetUserId(User)!;
        var ok = await _svc.UpdateStatusAsync(id, status, csId);
        if (!ok) return NotFound();

        await _svc.AuditAsync(csId, $"InternalUpdateStatus:{status}", id, GetClientIp());

        var agent = await _users.GetUserAsync(User);
        await _hub.Clients.Group("cs-board").SendAsync("CardStatusChanged", new { id, newStatus = status.ToString(), assignedTo = agent?.DisplayName });

        TempData["Success"] = $"Card status updated to {status}.";
        return RedirectToAction(nameof(RequestsAllBrands));
    }

    // ── GET /CsLiveHelp/CardPartial/{id} — returns fully rendered card partial ──

    [HttpGet]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> CardPartial(int id)
    {
        if (!Request.Headers.ContainsKey("X-Requested-With")) return BadRequest();
        var req = await _svc.GetRequestAsync(id);
        if (req is null) return NotFound();
        return PartialView("_CsBoardCard", req);
    }

    // ── GET /CsLiveHelp/CardModalsPartial/{id} — returns modal markup for one card ──

    [HttpGet]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> CardModalsPartial(int id)
    {
        if (!Request.Headers.ContainsKey("X-Requested-With")) return BadRequest();
        var req = await _svc.GetRequestAsync(id);
        if (req is null) return NotFound();

        ViewBag.TeamAllocationTeams = await _db.Teams
            .OrderBy(t => t.Name)
            .ToListAsync();

        return PartialView("_CsBoardCardModals", req);
    }
}
