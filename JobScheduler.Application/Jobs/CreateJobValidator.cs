using System.Globalization;
using System.Text.Json;
using Cronos;
using JobScheduler.Domain;

namespace JobScheduler.Application.Jobs;

internal static class CreateJobValidator
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 1000;
    public const int MaxExpressionLength = 200;

    public static (List<string> Errors, string? Expression, DateTimeOffset? NextExecutionAt) Validate(
        CreateJobRequest request, DateTimeOffset now)
    {
        var errors = new List<string>();

        if (request.UserId == Guid.Empty)
            errors.Add("UserId is required.");

        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add("Name is required.");
        else if (request.Name.Trim().Length > MaxNameLength)
            errors.Add($"Name must be at most {MaxNameLength} characters.");

        if (request.Description is { Length: > MaxDescriptionLength })
            errors.Add($"Description must be at most {MaxDescriptionLength} characters.");

        if (!Enum.IsDefined(request.ScheduleType))
            errors.Add("ScheduleType is invalid.");

        if (!Enum.IsDefined(request.ConcurrencyMode))
            errors.Add("ConcurrencyMode is invalid.");

        if (string.IsNullOrWhiteSpace(request.Configuration) || !IsJson(request.Configuration))
            errors.Add("Configuration must be valid JSON.");

        string? expression = null;
        DateTimeOffset? next = null;

        if (string.IsNullOrWhiteSpace(request.ScheduleExpression))
        {
            errors.Add("ScheduleExpression is required.");
        }
        else if (request.ScheduleExpression.Trim().Length > MaxExpressionLength)
        {
            errors.Add($"ScheduleExpression must be at most {MaxExpressionLength} characters.");
        }
        else if (Enum.IsDefined(request.ScheduleType))
        {
            var candidate = request.ScheduleExpression.Trim();
            if (request.ScheduleType == ScheduleType.Cron)
            {
                if (TryGetNextCron(candidate, now, out next))
                    expression = candidate;
                else
                    errors.Add("ScheduleExpression is not a valid cron expression with a future occurrence.");
            }
            else if (DateTimeOffset.TryParse(candidate, CultureInfo.InvariantCulture,
                         DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at) && at > now)
            {
                next = at;
                expression = at.ToString("O", CultureInfo.InvariantCulture);
            }
            else
            {
                errors.Add("ScheduleExpression must be a future date/time for a one-time job.");
            }
        }

        return (errors, expression, next);
    }

    private static bool TryGetNextCron(string expression, DateTimeOffset now, out DateTimeOffset? next)
    {
        next = null;
        var fields = expression.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var format = fields switch
        {
            5 => CronFormat.Standard,
            6 => CronFormat.IncludeSeconds,
            _ => (CronFormat?)null
        };
        if (format is null || !CronExpression.TryParse(expression, format.Value, out var cron))
            return false;

        next = cron.GetNextOccurrence(now, TimeZoneInfo.Utc);
        return next is not null;
    }

    private static bool IsJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
