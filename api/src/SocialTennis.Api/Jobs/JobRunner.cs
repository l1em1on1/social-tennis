namespace SocialTennis.Api.Jobs;

public static class JobRunner
{
    /// <summary>Exit code for a job that finished.</summary>
    public const int Succeeded = 0;

    /// <summary>Exit code for a job that threw. The Container Apps Job counts it a failed execution.</summary>
    public const int Failed = 1;

    /// <summary>Exit code for a name no job has.</summary>
    public const int UnknownJob = 2;

    /// <summary>Registers every job. Adding one is a class beside these and a line here.</summary>
    public static IServiceCollection AddJobs(this IServiceCollection services) =>
        services.AddScoped<IJob, PurgeTokensJob>();

    /// <summary>
    /// Runs the named job in its own scope and returns the process exit code.
    /// The web host is never started: no requests are served and migrations
    /// are not applied (the API applies them when it starts).
    /// </summary>
    public static async Task<int> RunAsync(IServiceProvider services, string name, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(JobRunner));
        var jobs = scope.ServiceProvider.GetServices<IJob>().ToList();

        var job = jobs.SingleOrDefault(j => j.Name == name);
        if (job is null)
        {
            logger.LogError(
                "There is no job named {Name}. Jobs: {Jobs}.", name, string.Join(", ", jobs.Select(j => j.Name)));
            return UnknownJob;
        }

        logger.LogInformation("Job {Name} starting.", name);
        try
        {
            await job.RunAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Job {Name} failed.", name);
            return Failed;
        }

        logger.LogInformation("Job {Name} finished.", name);
        return Succeeded;
    }
}
