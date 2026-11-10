using System.ComponentModel.DataAnnotations;
using API.Filters;
using Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Dev;

/// <summary>
/// TEMPORARY – development only. Throws every kind of error so the error pipeline can be checked.
/// Delete once the real controllers have integration tests.
/// </summary>
[ApiController]
[Route("api/dev/errors")]
[DevelopmentOnly]
[AllowAnonymous]
public class DevErrorsController : ControllerBase
{
    [HttpGet("not-found")]
    public IActionResult ThrowNotFound() =>
        throw new NotFoundException("הפנייה לא נמצאה");

    [HttpGet("conflict")]
    public IActionResult ThrowConflict() =>
        throw new ConflictException(
            "הפנייה עודכנה על ידי משתמש אחר",
            "יש לרענן את הפנייה ולנסות שוב.",
            "concurrency");

    [HttpGet("business-rule")]
    public IActionResult ThrowBusinessRule() =>
        throw new BusinessRuleException(
            "לא ניתן להעביר פנייה סגורה לטיפול",
            errorType: "invalid-transition");

    [HttpGet("unexpected")]
    public IActionResult ThrowUnexpected() =>
        throw new InvalidOperationException("SECRET technical details – must never reach the client");

    [HttpPost("model-validation")]
    public IActionResult ModelValidation(DevValidationRequest request) => Ok(request);

    public record DevValidationRequest([Required, StringLength(10)] string Name);
}
