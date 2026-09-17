using CrmIntegration.Application.Contacts;
using Microsoft.AspNetCore.Mvc;

namespace CrmIntegration.Api.Controllers;

[ApiController]
[Route("api/contacts")]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contactService;

    public ContactsController(IContactService contactService)
    {
        _contactService = contactService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContactResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await _contactService.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContactResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var contact = await _contactService.GetByIdAsync(id, cancellationToken);
        return contact is null ? NotFound() : Ok(contact);
    }

    [HttpPost]
    public async Task<ActionResult<ContactResponse>> Create(CreateContactRequest request, CancellationToken cancellationToken)
    {
        var created = await _contactService.CreateAsync(request, cancellationToken, CorrelationId());
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContactResponse>> Update(Guid id, UpdateContactRequest request, CancellationToken cancellationToken) =>
        Ok(await _contactService.UpdateAsync(id, request, cancellationToken, CorrelationId()));

    private string CorrelationId() =>
        Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
}
