using Azure.Communication.Email;
using Microsoft.Extensions.Options;

namespace SocialTennis.Api.Email;

public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IEmailSender"/> with the provider named by
    /// Email:Provider. This method is the only place that knows the providers;
    /// adding one is a new case here and a new class beside it.
    /// </summary>
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        // Checked at startup: a production host with half its mail settings, or
        // with no provider at all, should fail to start, not fail (or log a
        // sign-in link) on the first sign-in.
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();

        // Chosen when first resolved rather than here, so the choice reads the
        // final configuration, including a test host's overrides.
        services.AddSingleton<IEmailSender>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<EmailOptions>>().Value;
            return options.Provider switch
            {
                EmailProvider.AzureCommunicationServices => ActivatorUtilities.CreateInstance<AzureCommunicationEmailSender>(
                    provider,
                    new EmailClient(options.AzureCommunicationServices.ConnectionString, AzureClientOptions())),
                _ => ActivatorUtilities.CreateInstance<LoggingEmailSender>(provider),
            };
        });

        return services;
    }

    /// <summary>
    /// Someone is waiting at the login form, so give up quickly: the SDK's
    /// defaults (three retries, 100 seconds each) could hold the request for
    /// minutes. One retry of ten seconds, then a 503 they can retry themselves.
    /// </summary>
    private static EmailClientOptions AzureClientOptions()
    {
        var options = new EmailClientOptions();
        options.Retry.MaxRetries = 1;
        options.Retry.NetworkTimeout = TimeSpan.FromSeconds(10);
        return options;
    }
}
