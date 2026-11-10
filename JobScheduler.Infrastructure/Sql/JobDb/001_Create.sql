-- JobDb schema (Job Store). Run on the PRIMARY server only; the failover group replicates it to the secondary.
-- Idempotent: safe to run more than once.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        UserId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        Name      NVARCHAR(200)    NOT NULL,
        Email     NVARCHAR(320)    NOT NULL,
        CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
        IsActive  BIT              NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
        CONSTRAINT UQ_Users_Email UNIQUE (Email)
    );
END;

IF OBJECT_ID(N'dbo.Jobs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Jobs
    (
        JobId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Jobs PRIMARY KEY,
        UserId             UNIQUEIDENTIFIER NOT NULL,
        Name               NVARCHAR(200)    NOT NULL,
        Description        NVARCHAR(1000)   NULL,
        SchedulerType      NVARCHAR(20)     NOT NULL,
        ScheduleExpression NVARCHAR(200)    NOT NULL,
        NextExecutionAtUtc DATETIMEOFFSET(7) NULL,
        Status             NVARCHAR(20)     NOT NULL,
        ConcurrencyMode    NVARCHAR(20)     NOT NULL,
        Configuration      NVARCHAR(MAX)    NOT NULL,
        JobVersion         INT              NOT NULL CONSTRAINT DF_Jobs_JobVersion DEFAULT 1,
        CreatedAt          DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_Jobs_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt          DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_Jobs_UpdatedAt DEFAULT SYSDATETIMEOFFSET(),
        DeletedAt          DATETIMEOFFSET(7) NULL,
        CONSTRAINT FK_Jobs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
        CONSTRAINT CK_Jobs_SchedulerType CHECK (SchedulerType IN (N'Cron', N'OneTime')),
        CONSTRAINT CK_Jobs_Status CHECK (Status IN (N'Active', N'Paused', N'Stopped')),
        CONSTRAINT CK_Jobs_ConcurrencyMode CHECK (ConcurrencyMode IN (N'Singleton', N'Concurrent')),
        CONSTRAINT CK_Jobs_Configuration_IsJson CHECK (ISJSON(Configuration) = 1)
    );
END;

-- Prevents duplicate job names per user among non-deleted jobs; also serves the FK and duplicate-check lookups.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Jobs_UserId_Name_Active' AND object_id = OBJECT_ID(N'dbo.Jobs'))
BEGIN
    CREATE UNIQUE INDEX UX_Jobs_UserId_Name_Active
        ON dbo.Jobs (UserId, Name)
        WHERE DeletedAt IS NULL;
END;
