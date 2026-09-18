using CrmIntegration.Application.Deals;
using CrmIntegration.Application.Security;
using Microsoft.AspNetCore.Authorization;
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
    [Authorize(Policy = AuthorizationPolicies.CanReadCrm)]
    public async Task<ActionResult<IReadOnlyList<DealResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await _dealService.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanReadCrm)]
    public async Task<ActionResult<DealResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var deal = await _dealService.GetByIdAsync(id, cancellationToken);
        return deal is null ? NotFound() : Ok(deal);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanWriteCrm)]
    public async Task<ActionResult<DealResponse>> Create(CreateDealRequest request, CancellationToken cancellationToken)
    {
        var created = await _dealService.CreateAsync(request, cancellationToken, CorrelationId());
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanWriteCrm)]
    public async Task<ActionResult<DealResponse>> Update(Guid id, UpdateDealRequest request, CancellationToken cancellationToken) =>
        Ok(await _dealService.UpdateAsync(id, request, cancellationToken, CorrelationId()));

    private string CorrelationId() =>
        Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
}
