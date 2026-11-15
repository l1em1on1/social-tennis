using SocialTennis.Api.Email;

namespace SocialTennis.Api.UnitTests;

public class EmailOptionsTests
{
    [Fact]
    public void In_development_the_log_provider_needs_no_settings()
    {
        Assert.Null(new EmailOptions().Problem(isDevelopment: true));
    }

    [Fact]
    public void Outside_development_the_log_provider_must_be_allowed_explicitly()
    {
        Assert.Contains("Email__AllowLogProvider", new EmailOptions().Problem(isDevelopment: false), StringComparison.Ordinal);
        Assert.Null(new EmailOptions { AllowLogProvider = true }.Problem(isDevelopment: false));
    }

    [Fact]
    public void A_real_provider_needs_a_from_address()
    {
        var options = new EmailOptions
        {
            Provider = EmailProvider.AzureCommunicationServices,
            AzureCommunicationServices = { ConnectionString = "endpoint=https://example.test/;accesskey=a2V5" },
        };

        Assert.Contains("FromAddress", options.Problem(isDevelopment: false), StringComparison.Ordinal);
    }

    [Fact]
    public void Azure_Communication_Services_needs_a_connection_string()
    {
        var options = new EmailOptions
        {
            Provider = EmailProvider.AzureCommunicationServices,
            FromAddress = "DoNotReply@example.test",
        };

        Assert.Contains("ConnectionString", options.Problem(isDevelopment: false), StringComparison.Ordinal);
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

        Assert.Null(options.Problem(isDevelopment: false));
    }
}
