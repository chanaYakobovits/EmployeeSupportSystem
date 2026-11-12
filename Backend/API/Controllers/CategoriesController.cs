using API.Auth;
using API.Extensions;
using Common.DTOs.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

/// <summary>Categories and routing (spec 9.3): read by every signed-in user, changed by Admin only. No delete.</summary>
[ApiController]
[Route("api/categories")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    /// <summary>Active categories, by name, the fallback last. <paramref name="includeInactive"/> is honored for Admin only.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll([FromQuery] bool includeInactive,
        CancellationToken cancellationToken) =>
        Ok(await categoryService.GetAllAsync(includeInactive, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<CategorySaveResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategorySaveResult>> Create(CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await categoryService.CreateAsync(request, HttpContext.ClientIp(), cancellationToken);
        // No GET by id: the list is the read endpoint.
        return Created("/api/categories", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<CategorySaveResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CategorySaveResult>> Update(int id, UpdateCategoryRequest request,
        CancellationToken cancellationToken) =>
        Ok(await categoryService.UpdateAsync(id, request, HttpContext.ClientIp(), cancellationToken));
}
