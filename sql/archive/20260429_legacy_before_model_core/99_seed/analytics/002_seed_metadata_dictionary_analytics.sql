/*******************************************************************************
* TILSOFTAI Analytics Module - Metadata Dictionary Seeds
* Purpose: Localized labels for analytics output fields
*******************************************************************************/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Insight output labels
MERGE dbo.MetadataDictionary AS target
USING (VALUES
    (NULL, 'analytics.headline', 'en', 'Headline', 'Summary headline for the insight'),
    (NULL, 'analytics.headline', 'vi', 'Tiêu đề', 'Tiêu đề tóm tắt insight'),
    (NULL, 'analytics.breakdown', 'en', 'Breakdown', 'Detailed breakdown by category'),
    (NULL, 'analytics.breakdown', 'vi', 'Phân tích chi tiết', 'Phân tích chi tiết theo danh mục'),
    (NULL, 'analytics.total', 'en', 'Total', 'Grand total'),
    (NULL, 'analytics.total', 'vi', 'Tổng', 'Tổng số'),
    (NULL, 'analytics.notes', 'en', 'Notes', 'Additional notes and warnings'),
    (NULL, 'analytics.notes', 'vi', 'Ghi chú', 'Ghi chú và cảnh báo bổ sung'),
    (NULL, 'analytics.filter', 'en', 'Filter', 'Applied filter'),
    (NULL, 'analytics.filter', 'vi', 'Bộ lọc', 'Bộ lọc đã áp dụng'),
    (NULL, 'analytics.limit', 'en', 'Limit', 'Result limit applied'),
    (NULL, 'analytics.limit', 'vi', 'Giới hạn', 'Giới hạn kết quả đã áp dụng'),
    (NULL, 'analytics.freshness', 'en', 'Data freshness', 'When data was last updated'),
    (NULL, 'analytics.freshness', 'vi', 'Độ tươi dữ liệu', 'Thời điểm dữ liệu được cập nhật'),
    (NULL, 'analytics.truncated', 'en', 'Results truncated', 'Not all results shown'),
    (NULL, 'analytics.truncated', 'vi', 'Kết quả bị cắt', 'Không hiển thị hết kết quả'),
    (NULL, 'analytics.season', 'en', 'Season', 'Business season'),
    (NULL, 'analytics.season', 'vi', 'Mùa', 'Mùa kinh doanh'),
    (NULL, 'analytics.count', 'en', 'Count', 'Number of items'),
    (NULL, 'analytics.count', 'vi', 'Số lượng', 'Số mục')
) AS source (TenantId, [Key], Language, DisplayName, Description)
ON target.TenantId IS NOT DISTINCT FROM source.TenantId 
   AND target.[Key] = source.[Key] 
   AND target.Language = source.Language
WHEN NOT MATCHED THEN
    INSERT (TenantId, [Key], Language, DisplayName, Description)
    VALUES (source.TenantId, source.[Key], source.Language, source.DisplayName, source.Description);
GO
