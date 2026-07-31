using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Volt.Application.Dtos;
using Volt.Domain.Entities;
using Volt.Infrastructure.Data;

namespace Volt.API.Controllers;

/// <summary>
/// Deliberately small unauthenticated surface for public agents. It never uses
/// admin data and only serialises fields already presented on the public site.
/// </summary>
[Route("api/public-agent")]
[ApiController]
public sealed class PublicAgentController : ControllerBase
{
    private static readonly HashSet<string> ValidSources = new(StringComparer.Ordinal)
    {
        "public_mcp", "webmcp"
    };

    private readonly DataContext _db;
    private readonly ILogger<PublicAgentController> _logger;

    public PublicAgentController(DataContext db, ILogger<PublicAgentController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("products")]
    [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<ApiResponse<object>>> SearchProducts([FromQuery] string? query, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var safePage = Math.Clamp(page, 1, 1000);
        var safePageSize = Math.Clamp(pageSize, 1, 50);
        var normalizedQuery = query?.Trim();

        var products = _db.Products.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            products = products.Where(x => x.ProductName.Contains(normalizedQuery));
        }

        var total = await products.CountAsync(ct);
        var items = await products
            .OrderByDescending(x => x.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new
            {
                id = x.Id,
                name = x.ProductName,
                inStock = x.InStock,
                productUrl = $"https://volt.az/product/{x.Id}",
                images = x.ProductImages.Where(image => image.Type).Select(image => image.ImageUrl).Take(3),
                variants = x.ProductParametrs.Where(parameter => parameter.IsActive).Select(parameter => new
                {
                    technicalPower = parameter.TechnicalPower,
                    effectiveness = parameter.Effectiveness,
                    publicPriceAzn = parameter.Amount
                })
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.SuccessResponse(new { page = safePage, pageSize = safePageSize, total, items }));
    }

    [HttpGet("products/{id:int}")]
    [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<ApiResponse<object>>> GetProduct(int id, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking()
            .Where(x => x.Id == id && x.IsActive)
            .Select(x => new
            {
                id = x.Id,
                name = x.ProductName,
                inStock = x.InStock,
                productUrl = $"https://volt.az/product/{x.Id}",
                certificate = x.Certificate,
                images = x.ProductImages.Where(image => image.Type).Select(image => image.ImageUrl).Take(3),
                variants = x.ProductParametrs.Where(parameter => parameter.IsActive).Select(parameter => new
                {
                    technicalPower = parameter.TechnicalPower,
                    effectiveness = parameter.Effectiveness,
                    publicPriceAzn = parameter.Amount
                }),
                descriptions = x.ProductDescriptions.SelectMany(description => description.Languages)
                    .Where(language => language.IsActive)
                    .Select(language => new { language = language.LanguageCode, description = language.Description, features = language.Features })
            })
            .FirstOrDefaultAsync(ct);

        return product is null
            ? NotFound(ApiResponse<object>.ErrorResponse("PRODUCT_NOT_FOUND", "The published product was not found."))
            : Ok(ApiResponse<object>.SuccessResponse(product));
    }

    [HttpGet("services")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<ApiResponse<object>>> GetServices(CancellationToken ct)
    {
        var services = await _db.ServiceManagements.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                id = x.Id,
                icon = x.Icon,
                languages = x.Languages.Where(language => language.IsActive).Select(language => new
                {
                    language = language.LanguageCode,
                    title = language.Title,
                    description = language.Description,
                    content = new[] { language.Content1, language.Content2, language.Content3, language.Content4 }.Where(value => !string.IsNullOrWhiteSpace(value))
                })
            })
            .ToListAsync(ct);

        var contactRequestTypes = await _db.ApplicationTypes.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                id = x.Id,
                languages = x.Languages.Where(language => language.IsActive)
                    .Select(language => new { language = language.LanguageCode, name = language.Name })
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.SuccessResponse(new { services, contactRequestTypes, contactUrl = "https://volt.az/contact" }));
    }

    [HttpPost("contact-drafts")]
    [EnableRateLimiting("public-agent-draft")]
    public async Task<ActionResult<ApiResponse<object>>> CreateDraft([FromBody] PublicAgentContactDraftRequest request, CancellationToken ct)
    {
        if (!ValidSources.Contains(request.Source ?? string.Empty))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("INVALID_SOURCE", "The submission source is invalid."));
        }
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("INVALID_REQUEST", "The contact draft could not be created."));
        }
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("INVALID_CONTACT_DRAFT", "Required contact fields are invalid."));
        }

        var applicationTypeExists = await _db.ApplicationTypes.AnyAsync(x => x.Id == request.ApplicationTypeId && x.IsActive, ct);
        if (!applicationTypeExists)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("APPLICATION_TYPE_NOT_FOUND", "The selected request type is unavailable."));
        }

        var now = DateTime.UtcNow;
        var duplicate = await _db.PublicAgentContactDrafts.AnyAsync(x =>
            x.Email == request.Email.Trim() &&
            x.Message == request.Message.Trim() &&
            x.CreatedAt >= now.AddMinutes(-10) &&
            x.Status == "PendingConfirmation", ct);
        if (duplicate)
        {
            return Conflict(ApiResponse<object>.ErrorResponse("DUPLICATE_CONTACT_DRAFT", "A matching confirmation is already pending."));
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var draft = new PublicAgentContactDraft
        {
            PublicId = Guid.NewGuid(),
            AccessTokenHash = HashToken(token),
            Source = request.Source,
            Name = request.Name.Trim(),
            Surname = request.Surname.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            Message = request.Message.Trim(),
            ApplicationTypeId = request.ApplicationTypeId,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(15),
            Status = "PendingConfirmation"
        };
        _db.PublicAgentContactDrafts.Add(draft);
        await _db.SaveChangesAsync(ct);

        var confirmationUrl = $"https://volt.az/contact/confirm/{draft.PublicId:D}?token={token}";
        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            draftId = draft.PublicId,
            token,
            status = draft.Status,
            expiresAt = draft.ExpiresAt,
            confirmationUrl,
            message = "The visitor must review and confirm this request on Volt.az before it is sent."
        }));
    }

    [HttpGet("contact-drafts/{publicId:guid}")]
    [EnableRateLimiting("public-agent-status")]
    public async Task<ActionResult<ApiResponse<object>>> GetDraftStatus(Guid publicId, [FromQuery] string token, CancellationToken ct)
    {
        var draft = await FindDraft(publicId, token, ct);
        if (draft is null) return NotFound(ApiResponse<object>.ErrorResponse("CONTACT_DRAFT_NOT_FOUND", "The draft was not found."));
        await MarkExpiredIfNeeded(draft, ct);
        return Ok(ApiResponse<object>.SuccessResponse(new { draftId = draft.PublicId, status = draft.Status, expiresAt = draft.ExpiresAt, submitted = draft.SubmittedContactRequestId is not null }));
    }

    // This endpoint is for the Volt.az confirmation page only. The regular
    // status route deliberately returns no customer data, so MCP status checks
    // cannot reveal details after a draft has been prepared.
    [HttpGet("contact-drafts/{publicId:guid}/review")]
    [EnableRateLimiting("public-agent-status")]
    public async Task<ActionResult<ApiResponse<object>>> ReviewDraft(Guid publicId, [FromQuery] string token, CancellationToken ct)
    {
        var draft = await FindDraft(publicId, token, ct);
        if (draft is null) return NotFound(ApiResponse<object>.ErrorResponse("CONTACT_DRAFT_NOT_FOUND", "The draft was not found."));
        await MarkExpiredIfNeeded(draft, ct);
        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            draftId = draft.PublicId,
            status = draft.Status,
            expiresAt = draft.ExpiresAt,
            review = new
            {
                name = draft.Name,
                surname = draft.Surname,
                email = draft.Email,
                phone = draft.Phone,
                message = draft.Message,
                applicationTypeId = draft.ApplicationTypeId
            }
        }));
    }

    [HttpPost("contact-drafts/{publicId:guid}/confirm")]
    [EnableRateLimiting("public-agent-confirm")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmDraft(Guid publicId, [FromBody] PublicAgentConfirmDraftRequest request, CancellationToken ct)
    {
        var draft = await FindDraft(publicId, request.Token, ct);
        if (draft is null) return NotFound(ApiResponse<object>.ErrorResponse("CONTACT_DRAFT_NOT_FOUND", "The draft was not found."));
        await MarkExpiredIfNeeded(draft, ct);
        if (!string.Equals(draft.Status, "PendingConfirmation", StringComparison.Ordinal))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("CONTACT_DRAFT_NOT_CONFIRMABLE", "This draft is no longer awaiting confirmation."));
        }

        var validApplicationType = await _db.ApplicationTypes.AnyAsync(x => x.Id == draft.ApplicationTypeId && x.IsActive, ct);
        if (!validApplicationType)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("APPLICATION_TYPE_NOT_FOUND", "The selected request type is no longer available."));
        }

        var contactRequest = new ContactRequst
        {
            Name = draft.Name,
            Surname = draft.Surname,
            Email = draft.Email,
            Phone = draft.Phone,
            Message = draft.Message,
            ApplicationTypeId = draft.ApplicationTypeId,
            Status = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.ContactRequsts.Add(contactRequest);
        await _db.SaveChangesAsync(ct);
        draft.Status = "Submitted";
        draft.ConfirmedAt = DateTime.UtcNow;
        draft.SubmittedContactRequestId = contactRequest.Id;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Public agent contact draft submitted. DraftId={DraftId} Source={Source}", draft.PublicId, draft.Source);
        return Ok(ApiResponse<object>.SuccessResponse(new { draftId = draft.PublicId, status = draft.Status, submitted = true }));
    }

    private async Task<PublicAgentContactDraft?> FindDraft(Guid publicId, string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var draft = await _db.PublicAgentContactDrafts.FirstOrDefaultAsync(x => x.PublicId == publicId, ct);
        if (draft is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(draft.AccessTokenHash), Encoding.UTF8.GetBytes(HashToken(token)))) return null;
        return draft;
    }

    private async Task MarkExpiredIfNeeded(PublicAgentContactDraft draft, CancellationToken ct)
    {
        if (draft.Status == "PendingConfirmation" && draft.ExpiresAt <= DateTime.UtcNow)
        {
            draft.Status = "Expired";
            await _db.SaveChangesAsync(ct);
        }
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

public sealed class PublicAgentContactDraftRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Surname { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(200)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string Phone { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Message { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int ApplicationTypeId { get; set; }
    [Required, MaxLength(32)] public string Source { get; set; } = string.Empty;
    [MaxLength(200)] public string? Website { get; set; }
}

public sealed class PublicAgentConfirmDraftRequest
{
    [Required, MinLength(32), MaxLength(128)] public string Token { get; set; } = string.Empty;
}
