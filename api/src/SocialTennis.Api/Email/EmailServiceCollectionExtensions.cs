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
        // Checked at startup: a production host with half its mail settings
        // should fail to start, not fail on the first sign-in.
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(options => options.Problem() is null, "The Email configuration section is incomplete for its Provider.")
            .ValidateOnStart();

        // Chosen when first resolved rather than here, so the choice reads the
        // final configuration, including a test host's overrides.
        services.AddSingleton<IEmailSender>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<EmailOptions>>().Value;
            return options.Provider switch
            {
                EmailProvider.AzureCommunicationServices => ActivatorUtilities.CreateInstance<AzureCommunicationEmailSender>(
                    provider, new EmailClient(options.AzureCommunicationServices.ConnectionString)),
                _ => ActivatorUtilities.CreateInstance<LoggingEmailSender>(provider),
            };
        });

        return services;
    }
}
