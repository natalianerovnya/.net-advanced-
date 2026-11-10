using JobScheduler.Application.Abstractions;
using JobScheduler.Application.Jobs;
using JobScheduler.Infrastructure.Data;
using JobScheduler.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobScheduler.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJobSchedulerDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        DapperConfiguration.Register();

        services.AddOptions<JobDbOptions>()
            .Bind(configuration.GetSection(JobDbOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<ScheduleDbOptions>()
            .Bind(configuration.GetSection(ScheduleDbOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<IDbExecutor, DbExecutor>();

        services.AddSingleton<UserRepository>();
        services.AddSingleton<JobRepository>();
        services.AddSingleton<ScheduledJobSnapshotRepository>();
        services.AddSingleton<IUserReader>(sp => sp.GetRequiredService<UserRepository>());
        services.AddSingleton<IJobReader>(sp => sp.GetRequiredService<JobRepository>());
        services.AddSingleton<IJobWriter>(sp => sp.GetRequiredService<JobRepository>());
        services.AddSingleton<IScheduledJobSnapshotReader>(sp => sp.GetRequiredService<ScheduledJobSnapshotRepository>());
        services.AddSingleton<IScheduledJobSnapshotWriter>(sp => sp.GetRequiredService<ScheduledJobSnapshotRepository>());
        services.TryAddSingleton<IReconciliationFlagger, LoggingReconciliationFlagger>();

        services.AddSingleton<CreateJobService>();
        return services;
    }
}
