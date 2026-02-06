-- =============================================
-- SQL Agent Job: Observability Data Purge
-- =============================================
-- Purpose: Schedule daily purge of old observability data
-- Schedule: Daily at 02:00 UTC
-- Prerequisites: SQL Server Agent must be enabled
-- =============================================

-- IMPORTANT: SQL Agent must be enabled for this job to run.
-- If SQL Agent is not available, use the ObservabilityPurgeHostedService instead
-- by setting Observability:PurgeEnabled=true in appsettings.json

USE msdb;
GO

-- Create the job if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'TILSOFTAI_ObservabilityPurge')
BEGIN
    DECLARE @jobId BINARY(16);
    
    EXEC msdb.dbo.sp_add_job
        @job_name = N'TILSOFTAI_ObservabilityPurge',
        @enabled = 1,
        @description = N'Purges old observability data (messages, tool executions, errors, conversations) based on retention policy',
        @category_name = N'Database Maintenance',
        @owner_login_name = N'sa',
        @job_id = @jobId OUTPUT;
    
    -- Add job step
    EXEC msdb.dbo.sp_add_jobstep
        @job_id = @jobId,
        @step_name = N'Purge Old Data',
        @step_id = 1,
        @cmdexec_success_code = 0,
        @on_success_action = 1, -- Quit with success
        @on_fail_action = 2,     -- Quit with failure
        @retry_attempts = 0,
        @retry_interval = 0,
        @subsystem = N'TSQL',
        @command = N'EXEC dbo.app_observability_purge @RetentionDays = 30, @BatchSize = 5000, @TenantId = NULL;',
        @database_name = N'TILSOFTAI';
    
    -- Schedule: Daily at 02:00 UTC
    EXEC msdb.dbo.sp_add_jobschedule
        @job_id = @jobId,
        @name = N'Daily at 02:00 UTC',
        @enabled = 1,
        @freq_type = 4,              -- Daily
        @freq_interval = 1,          -- Every day
        @freq_subday_type = 1,       -- At the specified time
        @freq_subday_interval = 0,
        @freq_relative_interval = 0,
        @freq_recurrence_factor = 1,
        @active_start_date = 20260101,
        @active_end_date = 99991231,
        @active_start_time = 20000,  -- 02:00:00 (HHMMSS format)
        @active_end_time = 235959;
    
    -- Add job to local server
    EXEC msdb.dbo.sp_add_jobserver
        @job_id = @jobId,
        @server_name = N'(local)';
    
    PRINT 'Job "TILSOFTAI_ObservabilityPurge" created successfully.';
END
ELSE
BEGIN
    PRINT 'Job "TILSOFTAI_ObservabilityPurge" already exists.';
END
GO

-- =============================================
-- To disable the job (without deleting it):
-- EXEC msdb.dbo.sp_update_job @job_name = N'TILSOFTAI_ObservabilityPurge', @enabled = 0;
--
-- To enable the job:
-- EXEC msdb.dbo.sp_update_job @job_name = N'TILSOFTAI_ObservabilityPurge', @enabled = 1;
--
-- To delete the job:
-- EXEC msdb.dbo.sp_delete_job @job_name = N'TILSOFTAI_ObservabilityPurge';
--
-- To run the job manually:
-- EXEC msdb.dbo.sp_start_job @job_name = N'TILSOFTAI_ObservabilityPurge';
--
-- To view job history:
-- SELECT * FROM msdb.dbo.sysjobhistory 
-- WHERE job_id = (SELECT job_id FROM msdb.dbo.sysjobs WHERE name = N'TILSOFTAI_ObservabilityPurge')
-- ORDER BY run_date DESC, run_time DESC;
-- =============================================

-- Switch back to TILSOFTAI for DbUp journal tracking
USE TILSOFTAI;
GO
