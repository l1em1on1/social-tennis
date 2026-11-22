using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// The test app on a clock the tests set (ADR-0016), for rules that turn on
/// what day it is where the Club is. It stands still unless a test moves it.
/// </summary>
public class ClockedTestFactory : AuthTestFactory
{
    /// <summary>
    /// 23:30 UTC on 10 July next year: 00:30 on 11 July in London, on summer
    /// time. Ahead of the real clock, which the token purge job's tests run
    /// on: a clock behind it would make this factory's sessions look ended to
    /// that job, which deletes them from the shared database mid-run.
    /// </summary>
    public static readonly DateTimeOffset Start = new(DateTime.UtcNow.Year + 1, 7, 10, 23, 30, 0, TimeSpan.Zero);

    public MovableClock Clock { get; } = new(Start);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public sealed class MovableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
