using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SocialTennis.Api.Email;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// The provider is chosen by configuration alone (#19). Nothing here sends:
/// the tests only ask the host which sender it built.
/// </summary>
public class EmailConfigurationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void With_no_email_settings_the_email_is_logged()
    {
        Assert.IsType<LoggingEmailSender>(factory.Services.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void Naming_Azure_Communication_Services_selects_it()
    {
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Email:Provider", "AzureCommunicationServices");
            builder.UseSetting("Email:FromAddress", "DoNotReply@example.test");
            builder.UseSetting(
                "Email:AzureCommunicationServices:ConnectionString",
                "endpoint=https://example.test/;accesskey=a2V5");
        });

        Assert.IsType<AzureCommunicationEmailSender>(configured.Services.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void A_real_provider_with_settings_missing_stops_the_host_starting()
    {
        using var misconfigured = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Email:Provider", "AzureCommunicationServices"));

        Assert.Throws<OptionsValidationException>(() => misconfigured.Services);
    }

    // #107: outside Development a missing provider must not quietly log links.
    [Fact]
    public void Outside_development_the_log_provider_stops_the_host_starting()
    {
        using var production = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment(Environments.Production));

        var error = Assert.Throws<OptionsValidationException>(() => production.Services);
        Assert.Contains("Email__Provider", error.Message, StringComparison.Ordinal);
        Assert.Contains("Email__AllowLogProvider", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Outside_development_the_log_provider_runs_when_allowed_explicitly()
    {
        using var production = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting("Email:AllowLogProvider", "true");
        });

        Assert.IsType<LoggingEmailSender>(production.Services.GetRequiredService<IEmailSender>());
    }
}
