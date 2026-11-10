-- ScheduleDb seed data (mirrors the seeded Jobs in JobDb). Run on the PRIMARY server only. Idempotent.
IF NOT EXISTS (SELECT 1 FROM dbo.ScheduledJobSnapshots WHERE JobId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa')
    INSERT dbo.ScheduledJobSnapshots (JobId, ScheduleType, ScheduleExpression, NextExecutionAt, JobVersion, IsEnabled, Configuration, ConcurrencyMode)
    VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', N'Cron', N'0 2 * * *', '2030-01-01T02:00:00.0000000+00:00', 1, 1, N'{"target":"reports"}', N'Singleton');
IF NOT EXISTS (SELECT 1 FROM dbo.ScheduledJobSnapshots WHERE JobId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb')
    INSERT dbo.ScheduledJobSnapshots (JobId, ScheduleType, ScheduleExpression, NextExecutionAt, JobVersion, IsEnabled, Configuration, ConcurrencyMode)
    VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', N'Cron', N'0 3 * * 1', '2030-01-07T03:00:00.0000000+00:00', 1, 1, N'{"retentionDays":30}', N'Concurrent');
IF NOT EXISTS (SELECT 1 FROM dbo.ScheduledJobSnapshots WHERE JobId = 'cccccccc-cccc-cccc-cccc-cccccccccccc')
    INSERT dbo.ScheduledJobSnapshots (JobId, ScheduleType, ScheduleExpression, NextExecutionAt, JobVersion, IsEnabled, Configuration, ConcurrencyMode)
    VALUES ('cccccccc-cccc-cccc-cccc-cccccccccccc', N'OneTime', N'2030-06-01T00:00:00.0000000+00:00', '2030-06-01T00:00:00.0000000+00:00', 1, 1, N'{}', N'Singleton');
