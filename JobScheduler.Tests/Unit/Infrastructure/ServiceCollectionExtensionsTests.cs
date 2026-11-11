using JobScheduler.Application.Jobs;
using JobScheduler.Infrastructure;
using JobScheduler.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobScheduler.Tests.Unit.Infrastructure;

public class ServiceCollectionExtensionsTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJobSchedulerDataAccess(configuration);
        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["JobScheduler:JobDb:Primary"] = "Server=job-primary;Database=JobDb;",
        ["JobScheduler:JobDb:Replica"] = "Server=job-replica;Database=JobDb;ApplicationIntent=ReadOnly;",
        ["JobScheduler:ScheduleDb:Primary"] = "Server=schedule-primary;Database=ScheduleDb;",
        ["JobScheduler:ScheduleDb:Replica"] = "Server=schedule-replica;Database=ScheduleDb;ApplicationIntent=ReadOnly;",
    };

    [Fact]
    public void ResolvesUseCaseAndConnectionFactory_WhenConfigured()
    {
        using var provider = Build(ValidSettings());

        Assert.NotNull(provider.GetRequiredService<ICreateJobService>());
        Assert.NotNull(provider.GetRequiredService<IDbConnectionFactory>());
        Assert.Equal("Server=job-primary;Database=JobDb;", provider.GetRequiredService<IOptions<JobDbOptions>>().Value.Primary);
    }

    [Theory]
    [InlineData("JobScheduler:JobDb:Primary")]
    [InlineData("JobScheduler:JobDb:Replica")]
    [InlineData("JobScheduler:ScheduleDb:Primary")]
    [InlineData("JobScheduler:ScheduleDb:Replica")]
    public void OptionsValidation_Fails_WhenAConnectionStringIsMissing(string missingKey)
    {
        var settings = ValidSettings();
        settings.Remove(missingKey);
        using var provider = Build(settings);

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider.GetRequiredService<IOptions<JobDbOptions>>().Value;
            _ = provider.GetRequiredService<IOptions<ScheduleDbOptions>>().Value;
        });
    }
}
