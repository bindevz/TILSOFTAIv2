SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT N'Dropping only TILSOFTAI-owned framework procedures/views that are recreated by sql/current.';
GO

DROP PROCEDURE IF EXISTS dbo.ai_model_compare_models;
DROP PROCEDURE IF EXISTS dbo.ai_model_compare;
DROP PROCEDURE IF EXISTS dbo.ai_model_count;
DROP PROCEDURE IF EXISTS dbo.ai_model_get_overview;
DROP PROCEDURE IF EXISTS dbo.ai_model_get_pieces;
DROP PROCEDURE IF EXISTS dbo.ai_model_get_materials;
DROP PROCEDURE IF EXISTS dbo.ai_model_get_packaging;
GO

DROP PROCEDURE IF EXISTS dbo.app_actionrequest_create;
DROP PROCEDURE IF EXISTS dbo.app_actionrequest_get;
DROP PROCEDURE IF EXISTS dbo.app_actionrequest_get_active_for_conversation;
DROP PROCEDURE IF EXISTS dbo.app_actionrequest_confirm;
DROP PROCEDURE IF EXISTS dbo.app_actionrequest_approve;
DROP PROCEDURE IF EXISTS dbo.app_actionrequest_reject;
DROP PROCEDURE IF EXISTS dbo.app_actionrequest_mark_executed;
DROP PROCEDURE IF EXISTS dbo.app_actionrequest_expire_old;
GO

DROP VIEW IF EXISTS dbo.vw_ModelSemantic;
GO

PRINT N'Project-owned procedure/view cleanup complete. No ERP source tables were dropped.';
GO
