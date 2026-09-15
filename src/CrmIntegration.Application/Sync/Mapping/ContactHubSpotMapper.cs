using CrmIntegration.Application.Common;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Sync.Mapping;

public class ContactHubSpotMapper : IContactHubSpotMapper
{
    private readonly IHubSpotStageMapper _stageMapper;

    public ContactHubSpotMapper(IHubSpotStageMapper stageMapper)
    {
        _stageMapper = stageMapper;
    }

    public IReadOnlyList<string> HubSpotProperties { get; } =
        ["email", "firstname", "lastname", "phone", "jobtitle", "lifecyclestage"];

    public IReadOnlyDictionary<string, string?> ToHubSpotProperties(Contact contact) => new Dictionary<string, string?>
    {
        ["email"] = contact.Email,
        ["firstname"] = contact.FirstName,
        ["lastname"] = contact.LastName,
        ["phone"] = contact.Phone,
        ["jobtitle"] = contact.JobTitle,
        ["lifecyclestage"] = _stageMapper.ToHubSpotLifecycleStage(contact.LifecycleStage)
    };

    public void ApplyHubSpotProperties(Contact contact, HubSpotRecord record)
    {
        if (record.Properties.TryGetValue("email", out var email) && !string.IsNullOrWhiteSpace(email))
        {
            contact.Email = Normalization.NormalizeEmail(email);
        }

        if (record.Properties.TryGetValue("firstname", out var firstName))
        {
            contact.FirstName = firstName?.Trim() ?? contact.FirstName;
        }

        if (record.Properties.TryGetValue("lastname", out var lastName))
        {
            contact.LastName = lastName?.Trim() ?? contact.LastName;
        }

        if (record.Properties.TryGetValue("phone", out var phone))
        {
            contact.Phone = Normalization.NormalizePhone(phone);
        }

        if (record.Properties.TryGetValue("jobtitle", out var jobTitle))
        {
            contact.JobTitle = jobTitle?.Trim();
        }

        if (record.Properties.TryGetValue("lifecyclestage", out var lifecycleStage) && !string.IsNullOrWhiteSpace(lifecycleStage))
        {
            var mapped = _stageMapper.FromHubSpotLifecycleStage(lifecycleStage);
            if (mapped is not null)
            {
                contact.LifecycleStage = mapped.Value;
            }
        }
    }
}
