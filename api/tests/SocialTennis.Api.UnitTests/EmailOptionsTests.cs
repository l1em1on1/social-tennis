using SocialTennis.Api.Email;

namespace SocialTennis.Api.UnitTests;

public class EmailOptionsTests
{
    [Fact]
    public void The_log_provider_needs_no_settings()
    {
        Assert.Null(new EmailOptions().Problem());
    }

    [Fact]
    public void A_real_provider_needs_a_from_address()
    {
        var options = new EmailOptions
        {
            Provider = EmailProvider.AzureCommunicationServices,
            AzureCommunicationServices = { ConnectionString = "endpoint=https://example.test/;accesskey=a2V5" },
        };

        Assert.Contains("FromAddress", options.Problem(), StringComparison.Ordinal);
    }

    [Fact]
    public void Azure_Communication_Services_needs_a_connection_string()
    {
        var options = new EmailOptions
        {
            Provider = EmailProvider.AzureCommunicationServices,
            FromAddress = "DoNotReply@example.test",
        };

        Assert.Contains("ConnectionString", options.Problem(), StringComparison.Ordinal);
    }

    [Fact]
    public void Azure_Communication_Services_with_both_is_complete()
    {
        var options = new EmailOptions
        {
            Provider = EmailProvider.AzureCommunicationServices,
            FromAddress = "DoNotReply@example.test",
            AzureCommunicationServices = { ConnectionString = "endpoint=https://example.test/;accesskey=a2V5" },
        };

        Assert.Null(options.Problem());
    }
}
