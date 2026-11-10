-- ScheduleDb schema (Orchestrator snapshots). Run on the PRIMARY server only; the failover group replicates it to the secondary.
-- Idempotent: safe to run more than once. JobId has no cross-database FK by design.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

IF OBJECT_ID(N'dbo.ScheduledJobSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScheduledJobSnapshots
    (
        JobId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ScheduledJobSnapshots PRIMARY KEY,
        ScheduleType       NVARCHAR(20)     NOT NULL,
        ScheduleExpression NVARCHAR(200)    NOT NULL,
        NextExecutionAt    DATETIMEOFFSET(7) NULL,
        JobVersion         INT              NOT NULL,
        IsEnabled          BIT              NOT NULL,
        Configuration      NVARCHAR(MAX)    NOT NULL,
        ConcurrencyMode    NVARCHAR(20)     NOT NULL,
        SyncedAt           DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_ScheduledJobSnapshots_SyncedAt DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT CK_ScheduledJobSnapshots_ScheduleType CHECK (ScheduleType IN (N'Cron', N'OneTime')),
        CONSTRAINT CK_ScheduledJobSnapshots_ConcurrencyMode CHECK (ConcurrencyMode IN (N'Singleton', N'Concurrent')),
        CONSTRAINT CK_ScheduledJobSnapshots_Configuration_IsJson CHECK (ISJSON(Configuration) = 1)
    );
END;

-- Supports UC2.1: find enabled jobs that are due.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ScheduledJobSnapshots_IsEnabled_NextExecutionAt' AND object_id = OBJECT_ID(N'dbo.ScheduledJobSnapshots'))
BEGIN
    CREATE INDEX IX_ScheduledJobSnapshots_IsEnabled_NextExecutionAt
        ON dbo.ScheduledJobSnapshots (IsEnabled, NextExecutionAt);
END;
