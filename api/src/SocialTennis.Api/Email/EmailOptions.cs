namespace SocialTennis.Api.Email;

public enum EmailProvider
{
    /// <summary>Writes the email to the application log. Needs no credentials.</summary>
    Log,

    /// <summary>Sends through Azure Communication Services Email (ADR-0020).</summary>
    AzureCommunicationServices,
}

/// <summary>Bound from the "Email" configuration section.</summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Which provider carries the email. Defaults to the log, so dev needs no mail account.</summary>
    public EmailProvider Provider { get; set; } = EmailProvider.Log;

    /// <summary>
    /// The address email is sent from. Required by every real provider, and
    /// must be on a domain that provider has verified.
    /// </summary>
    public string FromAddress { get; set; } = "";

    public AzureCommunicationServicesOptions AzureCommunicationServices { get; set; } = new();

    public class AzureCommunicationServicesOptions
    {
        /// <summary>Connection string of the Communication Services resource. A secret.</summary>
        public string ConnectionString { get; set; } = "";
    }

    /// <summary>What is missing for the chosen provider, or null when it is fully configured.</summary>
    public string? Problem() => Provider switch
    {
        EmailProvider.Log => null,
        _ when string.IsNullOrWhiteSpace(FromAddress) =>
            $"Email:FromAddress is required when Email:Provider is {Provider}.",
        EmailProvider.AzureCommunicationServices
            when string.IsNullOrWhiteSpace(AzureCommunicationServices.ConnectionString) =>
            "Email:AzureCommunicationServices:ConnectionString is required when Email:Provider is AzureCommunicationServices.",
        _ => null,
    };
}
