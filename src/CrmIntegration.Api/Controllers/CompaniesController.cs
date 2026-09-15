using CrmIntegration.Application.Companies;
using Microsoft.AspNetCore.Mvc;

namespace CrmIntegration.Api.Controllers;

[ApiController]
[Route("api/companies")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public CompaniesController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CompanyResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await _companyService.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompanyResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var company = await _companyService.GetByIdAsync(id, cancellationToken);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpPost]
    public async Task<ActionResult<CompanyResponse>> Create(CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        var created = await _companyService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CompanyResponse>> Update(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken) =>
        Ok(await _companyService.UpdateAsync(id, request, cancellationToken));
}
