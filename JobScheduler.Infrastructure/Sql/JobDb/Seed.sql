-- JobDb seed data. Run on the PRIMARY server only. Idempotent.
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = '11111111-1111-1111-1111-111111111111')
    INSERT dbo.Users (UserId, Name, Email, IsActive) VALUES ('11111111-1111-1111-1111-111111111111', N'Alice Example', N'alice@example.com', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = '22222222-2222-2222-2222-222222222222')
    INSERT dbo.Users (UserId, Name, Email, IsActive) VALUES ('22222222-2222-2222-2222-222222222222', N'Bob Example', N'bob@example.com', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = '33333333-3333-3333-3333-333333333333')
    INSERT dbo.Users (UserId, Name, Email, IsActive) VALUES ('33333333-3333-3333-3333-333333333333', N'Carol Inactive', N'carol@example.com', 0);

IF NOT EXISTS (SELECT 1 FROM dbo.Jobs WHERE JobId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa')
    INSERT dbo.Jobs (JobId, UserId, Name, Description, SchedulerType, ScheduleExpression, NextExecutionAtUtc, Status, ConcurrencyMode, Configuration, JobVersion)
    VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '11111111-1111-1111-1111-111111111111', N'Nightly report', N'Builds the nightly report',
            N'Cron', N'0 2 * * *', '2030-01-01T02:00:00.0000000+00:00', N'Active', N'Singleton', N'{"target":"reports"}', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Jobs WHERE JobId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb')
    INSERT dbo.Jobs (JobId, UserId, Name, Description, SchedulerType, ScheduleExpression, NextExecutionAtUtc, Status, ConcurrencyMode, Configuration, JobVersion)
    VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '22222222-2222-2222-2222-222222222222', N'Weekly cleanup', NULL,
            N'Cron', N'0 3 * * 1', '2030-01-07T03:00:00.0000000+00:00', N'Active', N'Concurrent', N'{"retentionDays":30}', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Jobs WHERE JobId = 'cccccccc-cccc-cccc-cccc-cccccccccccc')
    INSERT dbo.Jobs (JobId, UserId, Name, Description, SchedulerType, ScheduleExpression, NextExecutionAtUtc, Status, ConcurrencyMode, Configuration, JobVersion)
    VALUES ('cccccccc-cccc-cccc-cccc-cccccccccccc', '11111111-1111-1111-1111-111111111111', N'One-time migration', N'Runs once',
            N'OneTime', N'2030-06-01T00:00:00.0000000+00:00', '2030-06-01T00:00:00.0000000+00:00', N'Active', N'Singleton', N'{}', 1);
