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

/// <summary>
/// CS Live Help Controller — manages three integrated pages for Account Managers and CS Agents.
/// 
/// ARCHITECTURE REFERENCE: See docs/CsLiveHelp-Architecture.md for comprehensive documentation of:
/// - The three pages (Requests, Board, RequestsAllBrands) and their relationships
/// - Role-based access control and data visibility rules
/// - SignalR real-time update flow and group routing
/// - Card partial endpoints and live modal injection behavior
/// - Comment thread visibility rules (CS-internal comments never reach AMs)
/// - File attachment support and upload paths
/// 
/// Key invariants:
/// - CS-internal comments (IsCsInternalOnly=true) only broadcast to 'cs-board' group
/// - Card partials are role-gated and validate ownership before returning HTML
/// - AM comment threads are refreshed on modal open to show new CS comments without page refresh
/// - Card assignments persist through drag-drop (UpdateStatusJson sets AssignedToId)
/// </summary>
[Authorize(Roles = $"{Roles.AccountManager},{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
public class CsLiveHelpController : Controller
{
    private readonly CsLiveHelpService          _svc;
    private readonly AppDbContext               _db;
    private readonly UserManager<AppUser>       _users;
    private readonly IHubContext<CsLiveHelpHub> _hub;
    private readonly IServiceScopeFactory       _scopeFactory;
    private readonly IWebHostEnvironment        _env;

    public CsLiveHelpController(
        CsLiveHelpService svc,
        AppDbContext db,
        UserManager<AppUser> users,
        IHubContext<CsLiveHelpHub> hub,
        IServiceScopeFactory scopeFactory,
        IWebHostEnvironment env)
    {
        _svc          = svc;
        _db           = db;
        _scopeFactory = scopeFactory;
        _users = users;
        _hub   = hub;
        _env   = env;
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

        // Push real-time event to the AM's own group and scoped CS recipients
        var brand = await _db.Brands.FindAsync(brandId);
        var rtype = await _db.CsRequestTypes.FindAsync(requestTypeId);
        var payload = new { id = req.Id, brandName = brand?.Name, requestType = rtype?.Name, status = req.Status.ToString(), isInternal = false };
        await _hub.Clients.Group($"am-{amId}").SendAsync("CardAdded", payload);

        var recipients = await _svc.ResolveRecipientsAsync(req.Id, null);
        // Board should always receive new AM requests in real time.
        // Sending only to scoped recipients can miss connected CS users who are not
        // in the resolved brand/team sets, which makes cards appear only after refresh.
        await _hub.Clients.Group("cs-board").SendAsync("CardAdded", payload);

        await _hub.Clients.Group("cs-board").SendAsync("RequestNotification", new
        {
            type = "newRequest",
            requestId = req.Id.ToString(),
            brandName = brand?.Name,
            requestType = rtype?.Name,
            actor = User.Identity?.Name ?? "System",
            contextType = "Board",
            timestamp = DateTime.UtcNow
        });

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

        var recipients = await _svc.ResolveRecipientsAsync(id, null);
        if (recipients.AllUniqueAgentIds.Any())
            await _hub.Clients.Users(recipients.AllUniqueAgentIds).SendAsync("CardUpdated", payload);
        else
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

    private static readonly string[] AllowedImageExtensions    = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly string[] AllowedDocumentExtensions = [".pdf", ".doc", ".docx", ".xls", ".xlsx"];
    private const long MaxImageBytes    = 5  * 1024 * 1024; // 5 MB
    private const long MaxDocumentBytes = 20 * 1024 * 1024; // 20 MB

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.AccountManager)]
    public async Task<IActionResult> AddComment(int id, string body, IFormFile? image)
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

        // Validate and save image if provided
        string? imagePath = null;
        if (image is { Length: > 0 })
        {
            if (image.Length > MaxImageBytes)
            {
                if (Request.Headers.ContainsKey("X-Requested-With"))
                    return Json(new { success = false, error = "Image must be 5 MB or smaller." });
                TempData["Error"] = "Image must be 5 MB or smaller.";
                return RedirectToAction(nameof(Requests));
            }

            var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext))
            {
                if (Request.Headers.ContainsKey("X-Requested-With"))
                    return Json(new { success = false, error = "Only jpg, png, gif, and webp images are allowed." });
                TempData["Error"] = "Only jpg, png, gif, and webp images are allowed.";
                return RedirectToAction(nameof(Requests));
            }

            var folder = Path.Combine(_env.WebRootPath, "uploads", "cs-comments", id.ToString());
            Directory.CreateDirectory(folder);
            var fileName  = $"{Guid.NewGuid()}{ext}";
            var filePath  = Path.Combine(folder, fileName);
            await using (var fs = System.IO.File.Create(filePath))
                await image.CopyToAsync(fs);

            imagePath = $"/uploads/cs-comments/{id}/{fileName}";
        }

        var mentionNames = _svc.ExtractMentionNames(body).ToList();
        var ok = await _svc.AddCommentAsync(id, amId, body, imagePath);
        if (!ok) return Forbid();

        await _svc.AuditAsync(amId, "AddComment", id, GetClientIp());

        var author = await _users.GetUserAsync(User);
        var commentPayload = new { requestId = id, author = author?.DisplayName ?? amId, body, isSystem = false, imagePath, createdAt = DateTime.UtcNow };
        await _hub.Clients.Group($"am-{amId}").SendAsync("CommentAdded", commentPayload);

        var recipients = await _svc.ResolveRecipientsAsync(id, mentionNames);
        if (recipients.AllUniqueAgentIds.Any())
            await _hub.Clients.Users(recipients.AllUniqueAgentIds).SendAsync("CommentAdded", commentPayload);
        else
            await _hub.Clients.Group("cs-board").SendAsync("CommentAdded", commentPayload);

        var commentRecipients = await _svc.ResolveCommentNotificationRecipientsAsync(id);
        var amCommentTargets = string.IsNullOrWhiteSpace(commentRecipients.AssignedAgentId)
            ? new List<string>()
            : new List<string> { commentRecipients.AssignedAgentId };

        if (amCommentTargets.Any())
        {
            await _hub.Clients.Users(amCommentTargets).SendAsync("CommentNotification", new
            {
                type = "comment",
                requestId = id.ToString(),
                author = author?.DisplayName ?? amId,
                contextType = "Requests",
                timestamp = DateTime.UtcNow
            });
        }

        if (recipients.MentionedUserIds.Any())
        {
            await _hub.Clients.Users(recipients.MentionedUserIds).SendAsync("MentionNotification", new
            {
                requestId = id.ToString(),
                author = author?.DisplayName ?? amId,
                contextType = "Requests",
                timestamp = DateTime.UtcNow
            });
        }

        if (Request.Headers.ContainsKey("X-Requested-With"))
            return Json(new { success = true, message = "Comment added.", imagePath });

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
        var brands = await _db.Brands.OrderBy(b => b.Name).ToListAsync();

        ViewBag.Requests = requests;
        ViewBag.TeamAllocationTeams = teamAllocationTeams;
        ViewBag.Brands = brands;
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

        var statusPayload = new { id, newStatus = "Escalated", assignedTo = assignedTeam.Name };
        var recipients = await _svc.ResolveRecipientsAsync(id, null);

        if (recipients.AllUniqueAgentIds.Any())
            await _hub.Clients.Users(recipients.AllUniqueAgentIds).SendAsync("CardStatusChanged", statusPayload);
        else
            await _hub.Clients.Group("cs-board").SendAsync("CardStatusChanged", statusPayload);

        var req = await _db.CsRequests.FindAsync(id);
        if (req?.AccountManagerId is not null)
            await _hub.Clients.Group($"am-{req.AccountManagerId}").SendAsync("CardStatusChanged", statusPayload);

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
        var statusPayload = new { id, newStatus = status.ToString(), assignedTo = agent?.DisplayName };
        var recipients = await _svc.ResolveRecipientsAsync(id, null);

        if (recipients.AllUniqueAgentIds.Any())
            await _hub.Clients.Users(recipients.AllUniqueAgentIds).SendAsync("CardStatusChanged", statusPayload);
        else
            await _hub.Clients.Group("cs-board").SendAsync("CardStatusChanged", statusPayload);

        var req = await _db.CsRequests.FindAsync(id);
        if (req?.AccountManagerId is not null)
            await _hub.Clients.Group($"am-{req.AccountManagerId}").SendAsync("CardStatusChanged", statusPayload);

        return Json(new { success = true });
    }

    // ── POST /CsLiveHelp/CsAddComment/{id} ────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> CsAddComment(int id, string body, IFormFile? image)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length > 1000)
        {
            var isAjax = Request.Headers.ContainsKey("X-Requested-With");
            if (isAjax)
                return Json(new { success = false, error = "Comment must be between 1 and 1000 characters." });
            TempData["Error"] = "Comment must be between 1 and 1000 characters.";
            return RedirectToAction(nameof(Board));
        }

        // Validate and save image if provided
        string? imagePath = null;
        if (image is { Length: > 0 })
        {
            if (image.Length > MaxImageBytes)
            {
                var isAjax = Request.Headers.ContainsKey("X-Requested-With");
                if (isAjax)
                    return Json(new { success = false, error = "Image must be 5 MB or smaller." });
                TempData["Error"] = "Image must be 5 MB or smaller.";
                return RedirectToAction(nameof(Board));
            }

            var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext))
            {
                var isAjax = Request.Headers.ContainsKey("X-Requested-With");
                if (isAjax)
                    return Json(new { success = false, error = "Only jpg, png, gif, and webp images are allowed." });
                TempData["Error"] = "Only jpg, png, gif, and webp images are allowed.";
                return RedirectToAction(nameof(Board));
            }

            var folder = Path.Combine(_env.WebRootPath, "uploads", "cs-comments", id.ToString());
            Directory.CreateDirectory(folder);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(folder, fileName);
            await using (var fs = System.IO.File.Create(filePath))
                await image.CopyToAsync(fs);

            imagePath = $"/uploads/cs-comments/{id}/{fileName}";
        }

        var csId = _users.GetUserId(User)!;
        var mentionNames = _svc.ExtractMentionNames(body).ToList();
        var ok   = await _svc.CsAddCommentAsync(id, csId, body, isCsInternalOnly: false, imagePath: imagePath);
        if (!ok) return NotFound();

        await _svc.AuditAsync(csId, "CsAddComment", id, GetClientIp());

        var agent = await _users.GetUserAsync(User);
        var commentPayload = new { requestId = id, author = agent?.DisplayName ?? csId, body, isSystem = false, imagePath, createdAt = DateTime.UtcNow };
        var recipients = await _svc.ResolveRecipientsAsync(id, mentionNames);

        if (recipients.AllUniqueAgentIds.Any())
            await _hub.Clients.Users(recipients.AllUniqueAgentIds).SendAsync("CommentAdded", commentPayload);
        else
            await _hub.Clients.Group("cs-board").SendAsync("CommentAdded", commentPayload);

        var commentRecipients = await _svc.ResolveCommentNotificationRecipientsAsync(id);
        var csCommentTargets = string.IsNullOrWhiteSpace(commentRecipients.AccountManagerId)
            ? new List<string>()
            : new List<string> { commentRecipients.AccountManagerId };

        if (csCommentTargets.Any())
        {
            await _hub.Clients.Users(csCommentTargets).SendAsync("CommentNotification", new
            {
                type = "comment",
                requestId = id.ToString(),
                author = agent?.DisplayName ?? csId,
                contextType = "Board",
                timestamp = DateTime.UtcNow
            });
        }

        if (recipients.MentionedUserIds.Any())
        {
            await _hub.Clients.Users(recipients.MentionedUserIds).SendAsync("MentionNotification", new
            {
                requestId = id.ToString(),
                author = agent?.DisplayName ?? csId,
                contextType = "Board",
                timestamp = DateTime.UtcNow
            });
        }

        var req = await _db.CsRequests.FindAsync(id);
        if (req?.AccountManagerId is not null)
            await _hub.Clients.Group($"am-{req.AccountManagerId}").SendAsync("CommentAdded", commentPayload);

        if (Request.Headers.ContainsKey("X-Requested-With"))
            return Json(new { success = true, message = "Comment added.", imagePath });

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
        var req  = await _svc.ResolveEscalationAsync(id, csId);
        if (req is null) return NotFound();

        await _svc.AuditAsync(csId, "ResolveEscalation", id, GetClientIp());

        // Notify the CS board
        await _hub.Clients.Group("cs-board").SendAsync("CardStatusChanged",
            new { id, newStatus = "Completed", assignedTo = (string?)null });

        // Notify the owning AM (if this was an escalated AM-originated request)
        if (req.AccountManagerId is not null)
            await _hub.Clients.Group($"am-{req.AccountManagerId}").SendAsync("CardStatusChanged",
                new { id, newStatus = "Completed", assignedTo = (string?)null });

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
        var payload = new { id = req.Id, brandName = brand?.Name, requestType = rtype?.Name, status = "Open", isInternal = true };
        var recipients = await _svc.ResolveRecipientsAsync(req.Id, null);

        if (recipients.AllUniqueAgentIds.Any())
            await _hub.Clients.Users(recipients.AllUniqueAgentIds).SendAsync("CardAdded", payload);
        else
            await _hub.Clients.Group("cs-board").SendAsync("CardAdded", payload);

        if (recipients.AllUniqueAgentIds.Any())
        {
            await _hub.Clients.Users(recipients.AllUniqueAgentIds).SendAsync("RequestNotification", new
            {
                type = "newRequest",
                requestId = req.Id.ToString(),
                brandName = brand?.Name,
                requestType = rtype?.Name,
                actor = User.Identity?.Name ?? "System",
                contextType = "RequestsAllBrands",
                timestamp = DateTime.UtcNow
            });
        }

        TempData["Success"] = "Internal request created.";
        return RedirectToAction(nameof(RequestsAllBrands));
    }

    // ── POST /CsLiveHelp/InternalAddComment/{id} ─────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> InternalAddComment(int id, string body, IFormFile? image)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length > 1000)
        {
            var isAjax = Request.Headers.ContainsKey("X-Requested-With");
            if (isAjax)
                return Json(new { success = false, error = "Comment must be between 1 and 1000 characters." });
            TempData["Error"] = "Comment must be between 1 and 1000 characters.";
            return RedirectToAction(nameof(RequestsAllBrands));
        }

        string? imagePath = null;
        if (image is { Length: > 0 })
        {
            var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
            var isImage    = AllowedImageExtensions.Contains(ext);
            var isDocument = AllowedDocumentExtensions.Contains(ext);

            if (!isImage && !isDocument)
            {
                var isAjax = Request.Headers.ContainsKey("X-Requested-With");
                if (isAjax)
                    return Json(new { success = false, error = "Only jpg, png, gif, webp, pdf, doc, docx, xls, and xlsx files are allowed." });
                TempData["Error"] = "Only jpg, png, gif, webp, pdf, doc, docx, xls, and xlsx files are allowed.";
                return RedirectToAction(nameof(RequestsAllBrands));
            }

            var maxBytes = isDocument ? MaxDocumentBytes : MaxImageBytes;
            if (image.Length > maxBytes)
            {
                var errMsg = isDocument
                    ? "Document must be 20 MB or smaller."
                    : "Image must be 5 MB or smaller.";
                var isAjax = Request.Headers.ContainsKey("X-Requested-With");
                if (isAjax)
                    return Json(new { success = false, error = errMsg });
                TempData["Error"] = errMsg;
                return RedirectToAction(nameof(RequestsAllBrands));
            }

            var subFolder = isDocument ? "cs-docs" : "cs-comments";
            var folder    = Path.Combine(_env.WebRootPath, "uploads", subFolder, id.ToString());
            Directory.CreateDirectory(folder);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(folder, fileName);
            await using (var fs = System.IO.File.Create(filePath))
                await image.CopyToAsync(fs);

            imagePath = $"/uploads/{subFolder}/{id}/{fileName}";
        }

        var csId = _users.GetUserId(User)!;
        var mentionNames = _svc.ExtractMentionNames(body).ToList();
        var ok   = await _svc.CsAddCommentAsync(id, csId, body, isCsInternalOnly: true, imagePath: imagePath);
        if (!ok) return NotFound();

        await _svc.AuditAsync(csId, "InternalAddComment", id, GetClientIp());

        var agent = await _users.GetUserAsync(User);
        var commentPayload = new { requestId = id, author = agent?.DisplayName ?? csId, body, isSystem = false, imagePath, createdAt = DateTime.UtcNow };
        var recipients = await _svc.ResolveRecipientsAsync(id, mentionNames);

        if (recipients.AllUniqueAgentIds.Any())
            await _hub.Clients.Users(recipients.AllUniqueAgentIds).SendAsync("CommentAdded", commentPayload);
        else
            await _hub.Clients.Group("cs-board").SendAsync("CommentAdded", commentPayload);

        if (recipients.MentionedUserIds.Any())
        {
            await _hub.Clients.Users(recipients.MentionedUserIds).SendAsync("MentionNotification", new
            {
                requestId = id.ToString(),
                author = agent?.DisplayName ?? csId,
                contextType = "RequestsAllBrands",
                timestamp = DateTime.UtcNow
            });
        }

        if (Request.Headers.ContainsKey("X-Requested-With"))
            return Json(new { success = true, message = "Comment added.", imagePath });

        TempData["Success"] = "Comment added.";
        return RedirectToAction(nameof(RequestsAllBrands));
    }

    // ── GET /CsLiveHelp/MentionCandidates/{id} — scoped @mention names ─────

    [HttpGet]
    [Authorize(Roles = $"{Roles.AccountManager},{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> MentionCandidates(int id)
    {
        if (!Request.Headers.ContainsKey("X-Requested-With")) return BadRequest();

        var req = await _svc.GetRequestAsync(id);
        if (req is null) return NotFound();

        var userId = _users.GetUserId(User)!;
        var isCsUser = User.IsInRole(Roles.CSAgent) || User.IsInRole(Roles.TeamLeader) || User.IsInRole(Roles.BrandManager) || User.IsInRole(Roles.SwissArmyKnife);

        if (!isCsUser && req.AccountManagerId != userId)
            return Forbid();

        var names = await _svc.GetMentionCandidatesAsync(id);
        return Json(new { success = true, candidates = names });
    }

    // ── GET /CsLiveHelp/AmCommentThread/{id} — refresh thread HTML ───────

    [HttpGet]
    [Authorize(Roles = Roles.AccountManager)]
    public async Task<IActionResult> AmCommentThread(int id)
    {
        if (!Request.Headers.ContainsKey("X-Requested-With"))
            return BadRequest();

        var amId = _users.GetUserId(User)!;

        var req = await _db.CsRequests
            .Where(r => r.Id == id && r.AccountManagerId == amId)
            .Include(r => r.Comments.Where(c => !c.IsCsInternalOnly))
                .ThenInclude(c => c.Author)
            .FirstOrDefaultAsync();

        if (req is null)
            return NotFound();

        var comments = req.Comments.OrderBy(c => c.CreatedAt).ToList();

        if (!comments.Any())
        {
            return Content("<p class=\"mb-0\">No comments yet.</p>");
        }

        var html = new System.Text.StringBuilder();
        foreach (var c in comments)
        {
            var timestamp = c.CreatedAt.ToString("MMM dd, HH:mm");
            var author = c.Author?.DisplayName ?? c.AuthorId;
            var bgClass = c.IsSystemMessage ? "bg-warning bg-opacity-10" : "bg-light";

            html.AppendLine($"<div class=\"mb-2 p-2 rounded {bgClass}\">");
            html.AppendLine("  <div class=\"d-flex justify-content-between mb-1\">");
            html.AppendLine($"    <strong class=\"small\">{System.Net.WebUtility.HtmlEncode(author)}</strong>");
            html.AppendLine($"    <span class=\"text-muted small\">{timestamp} UTC</span>");
            html.AppendLine("  </div>");
            html.Append($"  <p class=\"mb-0 small\">{System.Net.WebUtility.HtmlEncode(c.Body)}");

            if (c.IsSystemMessage)
            {
                html.Append(" <span class=\"badge bg-warning text-dark ms-1\">System</span>");
            }

            html.AppendLine("</p>");

            if (!string.IsNullOrWhiteSpace(c.ImagePath))
            {
                html.AppendLine("  <div class=\"mt-2\">");
                html.AppendLine($"    <a href=\"{System.Net.WebUtility.HtmlEncode(c.ImagePath)}\" target=\"_blank\" rel=\"noopener\">");
                html.AppendLine($"      <img src=\"{System.Net.WebUtility.HtmlEncode(c.ImagePath)}\" alt=\"Attachment\" class=\"img-fluid rounded\" style=\"max-height:200px\" />");
                html.AppendLine("    </a>");
                html.AppendLine("  </div>");
            }

            html.AppendLine("</div>");
        }

        return Content(html.ToString(), "text/html");
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
        var statusPayload = new { id, newStatus = status.ToString(), assignedTo = agent?.DisplayName };
        var recipients = await _svc.ResolveRecipientsAsync(id, null);

        if (recipients.AllUniqueAgentIds.Any())
            await _hub.Clients.Users(recipients.AllUniqueAgentIds).SendAsync("CardStatusChanged", statusPayload);
        else
            await _hub.Clients.Group("cs-board").SendAsync("CardStatusChanged", statusPayload);

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
        var statusPayload = new { id, newStatus = status.ToString(), assignedTo = agent?.DisplayName };
        var recipients = await _svc.ResolveRecipientsAsync(id, null);

        if (recipients.AllUniqueAgentIds.Any())
            await _hub.Clients.Users(recipients.AllUniqueAgentIds).SendAsync("CardStatusChanged", statusPayload);
        else
            await _hub.Clients.Group("cs-board").SendAsync("CardStatusChanged", statusPayload);

        TempData["Success"] = $"Card status updated to {status}.";
        return RedirectToAction(nameof(RequestsAllBrands));
    }

    // ── GET /CsLiveHelp/AmCardPartial/{id} — AM-accessible card partial ─────

    [HttpGet]
    [Authorize(Roles = Roles.AccountManager)]
    public async Task<IActionResult> AmCardPartial(int id)
    {
        if (!Request.Headers.ContainsKey("X-Requested-With")) return BadRequest();
        var amId = _users.GetUserId(User)!;
        var req = await _svc.GetRequestAsync(id);
        if (req is null || req.AccountManagerId != amId) return NotFound();
        ViewData["ShowActions"] = true;
        return PartialView("_CsRequestCard", req);
    }

    // ── GET /CsLiveHelp/AmCardModalsPartial/{id} — AM comment modal partial ─

    [HttpGet]
    [Authorize(Roles = Roles.AccountManager)]
    public async Task<IActionResult> AmCardModalsPartial(int id)
    {
        if (!Request.Headers.ContainsKey("X-Requested-With")) return BadRequest();
        var amId = _users.GetUserId(User)!;
        var req = await _svc.GetRequestAsync(id);
        if (req is null || req.AccountManagerId != amId) return NotFound();
        return PartialView("_CsRequestCardModal", req);
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

    // ── GET /CsLiveHelp/InternalCardPartial/{id} — internal board card partial ──

    [HttpGet]
    [Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> InternalCardPartial(int id)
    {
        if (!Request.Headers.ContainsKey("X-Requested-With")) return BadRequest();
        var req = await _svc.GetRequestAsync(id);
        if (req is null) return NotFound();
        return PartialView("_InternalBoardCard", req);
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

    // ─────────────────────────────────────────────────────────────────────
    // DEMO SIMULATION  (TeamLeader | BrandManager | SwissArmyKnife only)
    // Requests are identified by ClientId prefix "DEMO-SIM-" so no DB
    // migration is required.  Remove this entire region + the Board.cshtml
    // UI block when the demo is no longer needed.
    // ─────────────────────────────────────────────────────────────────────

    private const string SimPrefix = "DEMO-SIM-";

    // Holds the running simulation loop — static so it survives across requests.
    private static CancellationTokenSource? _simCts;
    private static readonly object          _simLock = new();

    // ── GET /CsLiveHelp/SimulationStatus ─────────────────────────────────

    [HttpGet]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public IActionResult SimulationStatus()
        => Json(new { running = _simCts is not null && !_simCts.IsCancellationRequested });

    // ── POST /CsLiveHelp/StartSimulation ─────────────────────────────────

    /// <summary>
    /// Starts a continuous background simulation loop that keeps running
    /// until StopSimulation is called.  Each iteration creates one demo card
    /// and walks it through every lifecycle stage, pushing SignalR events
    /// (CardAdded, CardStatusChanged, CommentAdded, SimulationStep) so the
    /// Board and Requests views update live.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> StartSimulation()
    {
        lock (_simLock)
        {
            if (_simCts is not null && !_simCts.IsCancellationRequested)
                return Json(new { running = true, message = "Simulation already running." });

            _simCts = new CancellationTokenSource();
        }

        var actorId   = _users.GetUserId(User)!;
        var actor     = await _users.GetUserAsync(User);
        var actorName = actor?.DisplayName ?? "Demo";
        var token     = _simCts.Token;

        _ = Task.Run(async () =>
            await RunSimLoopAsync(actorId, actorName, _hub, _scopeFactory, token));

        return Json(new { running = true, message = "Simulation started." });
    }

    // ── POST /CsLiveHelp/StopSimulation ──────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public IActionResult StopSimulation()
    {
        lock (_simLock)
        {
            _simCts?.Cancel();
            _simCts = null;
        }
        return Json(new { running = false, message = "Simulation stopped." });
    }

    // ── Simulation background loop ────────────────────────────────────────

    private static readonly (string AmMsg, string CsReply, CsRequestStatus Mid, CsRequestStatus Final)[] SimScripts =
    [
        ("Client PL-{0} cannot log in — please reset.",               "Resetting password to default. Card marked InProgress.",          CsRequestStatus.InProgress,  CsRequestStatus.Completed),
        ("Account verification pending for client PL-{0}.",           "KYC documents reviewed. Moved to OnGoing pending final check.",   CsRequestStatus.OnGoing,     CsRequestStatus.Completed),
        ("Deposit not reflecting after 2 hrs for client PL-{0}.",     "Escalating to Payments team for immediate review.",               CsRequestStatus.Escalated,   CsRequestStatus.Completed),
        ("Withdrawal on hold — compliance flag for client PL-{0}.",   "Compliance reviewed — hold lifted. Card InProgress.",             CsRequestStatus.InProgress,  CsRequestStatus.Completed),
        ("Bonus not credited after qualifying deposit — PL-{0}.",     "Bonus manually applied by CS. Resolving now.",                    CsRequestStatus.OnGoing,     CsRequestStatus.Completed),
        ("Client PL-{0} locked out after failed 2FA attempts.",       "2FA reset performed. Client notified via email.",                 CsRequestStatus.InProgress,  CsRequestStatus.Completed),
        ("Account closure request received from client PL-{0}.",      "Closure process initiated. Documents sent to client.",            CsRequestStatus.Escalated,   CsRequestStatus.Completed),
        ("Responsible gambling limit increase — client PL-{0}.",      "Cooling-off period observed. Limit adjusted per policy.",         CsRequestStatus.OnGoing,     CsRequestStatus.Completed),
    ];

    private static async Task RunSimLoopAsync(
        string actorId, string actorName,
        IHubContext<CsLiveHelpHub> hub,
        IServiceScopeFactory factory,
        CancellationToken ct)
    {
        var rng     = new Random();
        var counter = 1;

        async Task Step(string msg)
            => await hub.Clients.Group("cs-board").SendAsync("SimulationStep",
                   new { message = $"[{DateTime.UtcNow:HH:mm:ss}] {msg}" }, ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = factory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var brand  = await db.Brands.OrderBy(b => b.Name).FirstOrDefaultAsync(ct);
                var rtypes = await db.CsRequestTypes.Where(t => !t.IsOther).OrderBy(t => t.Id).ToListAsync(ct);

                if (brand is null || rtypes.Count == 0)
                {
                    await Step("⚠ No brands or request types found — simulation paused.");
                    await Task.Delay(5000, ct);
                    continue;
                }

                var rtype    = rtypes[rng.Next(rtypes.Count)];
                var script   = SimScripts[counter % SimScripts.Length];
                var clientId = $"{SimPrefix}{counter:D4}";
                var playerId = rng.Next(100_000, 999_999);
                counter++;

                var amMsg   = string.Format(script.AmMsg, playerId);
                var csMsg   = script.CsReply;
                var midSt   = script.Mid;
                var finalSt = script.Final;

                // 1. AM submits request ────────────────────────────────────
                await Step($"🧑‍💼 AM submits request for client {clientId} · {rtype.Name}");

                var req = new CsRequest
                {
                    AccountManagerId = actorId,
                    BrandId          = brand.Id,
                    RequestTypeId    = rtype.Id,
                    ClientId         = clientId,
                    Status           = CsRequestStatus.Open,
                    CreatedAt        = DateTime.UtcNow,
                    UpdatedAt        = DateTime.UtcNow
                };
                db.CsRequests.Add(req);
                await db.SaveChangesAsync(ct);

                await hub.Clients.Group("cs-board").SendAsync("CardAdded",
                    new { id = req.Id, brandName = brand.Name, requestType = rtype.Name, status = "Open", isInternal = false }, ct);
                await hub.Clients.Group($"am-{actorId}").SendAsync("CardAdded",
                    new { id = req.Id, brandName = brand.Name, requestType = rtype.Name, status = "Open", isInternal = false }, ct);

                await Task.Delay(rng.Next(800, 1500), ct);

                // 2. AM adds a comment
                await Step($"💬 AM: \"{amMsg}\"");

                db.CsRequestComments.Add(new CsRequestComment
                {
                    RequestId = req.Id, AuthorId = actorId,
                    Body      = $"[DEMO] {amMsg}", CreatedAt = DateTime.UtcNow
                });
                req.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                await hub.Clients.Group("cs-board").SendAsync("CommentAdded",
                    new { requestId = req.Id, author = $"{actorName} (AM)", body = $"[DEMO] {amMsg}", isSystem = false, createdAt = DateTime.UtcNow }, ct);
                await hub.Clients.Group($"am-{actorId}").SendAsync("CommentAdded",
                    new { requestId = req.Id, author = actorName, body = $"[DEMO] {amMsg}", isSystem = false, createdAt = DateTime.UtcNow }, ct);

                await Task.Delay(rng.Next(800, 1500), ct);

                // 3. CS picks up card
                await Step($"🎯 CS picks up #{req.Id} → {midSt}");

                req.Status       = midSt;
                req.AssignedToId = actorId;
                req.UpdatedAt    = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                await hub.Clients.Group("cs-board").SendAsync("CardStatusChanged",
                    new { id = req.Id, newStatus = midSt.ToString(), assignedTo = actorName }, ct);
                await hub.Clients.Group($"am-{actorId}").SendAsync("CardStatusChanged",
                    new { id = req.Id, newStatus = midSt.ToString(), assignedTo = actorName }, ct);

                await Task.Delay(rng.Next(800, 1500), ct);

                // 4. CS posts resolution comment
                await Step($"✍ CS: \"{csMsg}\"");

                db.CsRequestComments.Add(new CsRequestComment
                {
                    RequestId = req.Id, AuthorId = actorId,
                    Body      = $"[DEMO – CS] {csMsg}", CreatedAt = DateTime.UtcNow, IsSystemMessage = true
                });
                req.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                await hub.Clients.Group("cs-board").SendAsync("CommentAdded",
                    new { requestId = req.Id, author = $"{actorName} (CS)", body = $"[DEMO – CS] {csMsg}", isSystem = true, createdAt = DateTime.UtcNow }, ct);
                await hub.Clients.Group($"am-{actorId}").SendAsync("CommentAdded",
                    new { requestId = req.Id, author = $"{actorName} (CS)", body = $"[DEMO – CS] {csMsg}", isSystem = true, createdAt = DateTime.UtcNow }, ct);

                await Task.Delay(rng.Next(800, 1500), ct);

                // 5. CS resolves card
                await Step($"✅ Card #{req.Id} resolved → {finalSt}");

                req.Status    = finalSt;
                req.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                await hub.Clients.Group("cs-board").SendAsync("CardStatusChanged",
                    new { id = req.Id, newStatus = finalSt.ToString(), assignedTo = actorName }, ct);
                await hub.Clients.Group($"am-{actorId}").SendAsync("CardStatusChanged",
                    new { id = req.Id, newStatus = finalSt.ToString(), assignedTo = actorName }, ct);

                await Task.Delay(rng.Next(1500, 2500), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                await hub.Clients.Group("cs-board").SendAsync("SimulationStep",
                    new { message = $"[{DateTime.UtcNow:HH:mm:ss}] ⚠ Error: {ex.Message}" });
                await Task.Delay(3000);
            }
        }

        await hub.Clients.Group("cs-board").SendAsync("SimulationStep",
            new { message = $"[{DateTime.UtcNow:HH:mm:ss}] ⏹ Simulation stopped." });
    }

    // ── POST /CsLiveHelp/CleanupSimulation ───────────────────────────────

    /// <summary>
    /// Stops any running loop and deletes all DEMO-SIM- requests, pushing
    /// CardDeleted SignalR events so the board clears in real-time.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> CleanupSimulation()
    {
        lock (_simLock)
        {
            _simCts?.Cancel();
            _simCts = null;
        }

        var simRequests = await _db.CsRequests
            .Where(r => r.ClientId != null && r.ClientId.StartsWith(SimPrefix))
            .ToListAsync();

        foreach (var r in simRequests)
        {
            await _hub.Clients.Group("cs-board").SendAsync("CardDeleted", new { id = r.Id });
            if (r.AccountManagerId is not null)
                await _hub.Clients.Group($"am-{r.AccountManagerId}").SendAsync("CardDeleted", new { id = r.Id });
        }

        _db.CsRequests.RemoveRange(simRequests);
        await _db.SaveChangesAsync();

        return Json(new { cleaned = simRequests.Count, message = $"{simRequests.Count} demo card(s) removed." });
    }
}
