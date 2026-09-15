using CrmIntegration.Application.Deals;
using Microsoft.AspNetCore.Mvc;

namespace CrmIntegration.Api.Controllers;

[ApiController]
[Route("api/deals")]
public class DealsController : ControllerBase
{
    private readonly IDealService _dealService;

    public DealsController(IDealService dealService)
    {
        _dealService = dealService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DealResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await _dealService.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DealResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var deal = await _dealService.GetByIdAsync(id, cancellationToken);
        return deal is null ? NotFound() : Ok(deal);
    }

    [HttpPost]
    public async Task<ActionResult<DealResponse>> Create(CreateDealRequest request, CancellationToken cancellationToken)
    {
        var created = await _dealService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DealResponse>> Update(Guid id, UpdateDealRequest request, CancellationToken cancellationToken) =>
        Ok(await _dealService.UpdateAsync(id, request, cancellationToken));
}
