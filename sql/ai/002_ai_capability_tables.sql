IF OBJECT_ID('ai.Capability', 'U') IS NULL
BEGIN
    CREATE TABLE ai.Capability
    (
        CapabilityID BIGINT IDENTITY CONSTRAINT PK_ai_Capability PRIMARY KEY,
        CapabilityKey NVARCHAR(200) NOT NULL,
        Domain NVARCHAR(100) NOT NULL,
        BusinessArea NVARCHAR(100) NULL,
        FunctionName NVARCHAR(200) NOT NULL,
        AdapterType NVARCHAR(50) NOT NULL,
        Operation NVARCHAR(100) NOT NULL,
        StoredProcedure SYSNAME NULL,
        ExecutionMode NVARCHAR(50) NOT NULL,
        ArgumentContract NVARCHAR(MAX) NULL,
        ResultSchema NVARCHAR(MAX) NULL,
        AnswerPolicy NVARCHAR(MAX) NULL,
        SensitivityPolicy NVARCHAR(MAX) NULL,
        RequiredRoles NVARCHAR(MAX) NULL,
        AllowedTenants NVARCHAR(MAX) NULL,
        AllowMultiCall BIT NOT NULL CONSTRAINT DF_ai_Capability_AllowMultiCall DEFAULT (0),
        IsActive BIT NOT NULL CONSTRAINT DF_ai_Capability_IsActive DEFAULT (1),
        VersionNo INT NOT NULL CONSTRAINT DF_ai_Capability_VersionNo DEFAULT (1),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_ai_Capability_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UX_ai_Capability_CapabilityKey UNIQUE (CapabilityKey),
        CONSTRAINT CK_ai_Capability_ArgumentContract_Json CHECK (ArgumentContract IS NULL OR ISJSON(ArgumentContract) = 1),
        CONSTRAINT CK_ai_Capability_ResultSchema_Json CHECK (ResultSchema IS NULL OR ISJSON(ResultSchema) = 1),
        CONSTRAINT CK_ai_Capability_AnswerPolicy_Json CHECK (AnswerPolicy IS NULL OR ISJSON(AnswerPolicy) = 1),
        CONSTRAINT CK_ai_Capability_SensitivityPolicy_Json CHECK (SensitivityPolicy IS NULL OR ISJSON(SensitivityPolicy) = 1),
        CONSTRAINT CK_ai_Capability_RequiredRoles_Json CHECK (RequiredRoles IS NULL OR ISJSON(RequiredRoles) = 1),
        CONSTRAINT CK_ai_Capability_AllowedTenants_Json CHECK (AllowedTenants IS NULL OR ISJSON(AllowedTenants) = 1)
    );
END;
GO

IF OBJECT_ID('ai.CapabilityText', 'U') IS NULL
BEGIN
    CREATE TABLE ai.CapabilityText
    (
        CapabilityTextID BIGINT IDENTITY CONSTRAINT PK_ai_CapabilityText PRIMARY KEY,
        CapabilityKey NVARCHAR(200) NOT NULL,
        Locale NVARCHAR(20) NOT NULL,
        ShortName NVARCHAR(300) NULL,
        Description NVARCHAR(MAX) NOT NULL,
        UseWhen NVARCHAR(MAX) NULL,
        DoNotUseWhen NVARCHAR(MAX) NULL,
        BusinessNotes NVARCHAR(MAX) NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_ai_CapabilityText_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ai_CapabilityText_Capability
            FOREIGN KEY (CapabilityKey) REFERENCES ai.Capability(CapabilityKey),
        CONSTRAINT UX_ai_CapabilityText_KeyLocale UNIQUE (CapabilityKey, Locale)
    );
END;
GO

IF OBJECT_ID('ai.CapabilityArgument', 'U') IS NULL
BEGIN
    CREATE TABLE ai.CapabilityArgument
    (
        ArgumentID BIGINT IDENTITY CONSTRAINT PK_ai_CapabilityArgument PRIMARY KEY,
        CapabilityKey NVARCHAR(200) NOT NULL,
        ArgumentName NVARCHAR(128) NOT NULL,
        ProcParameterName NVARCHAR(128) NOT NULL,
        DataType NVARCHAR(50) NOT NULL,
        IsRequired BIT NOT NULL,
        DefaultSource NVARCHAR(100) NULL,
        ValidationRule NVARCHAR(MAX) NULL,
        ClarificationPolicy NVARCHAR(MAX) NULL,
        DisplayOrder INT NOT NULL CONSTRAINT DF_ai_CapabilityArgument_DisplayOrder DEFAULT (0),
        IsActive BIT NOT NULL CONSTRAINT DF_ai_CapabilityArgument_IsActive DEFAULT (1),
        CONSTRAINT FK_ai_CapabilityArgument_Capability
            FOREIGN KEY (CapabilityKey) REFERENCES ai.Capability(CapabilityKey),
        CONSTRAINT UX_ai_CapabilityArgument_KeyName UNIQUE (CapabilityKey, ArgumentName),
        CONSTRAINT CK_ai_CapabilityArgument_ValidationRule_Json CHECK (ValidationRule IS NULL OR ISJSON(ValidationRule) = 1),
        CONSTRAINT CK_ai_CapabilityArgument_ClarificationPolicy_Json CHECK (ClarificationPolicy IS NULL OR ISJSON(ClarificationPolicy) = 1)
    );
END;
GO

IF OBJECT_ID('ai.ArgumentText', 'U') IS NULL
BEGIN
    CREATE TABLE ai.ArgumentText
    (
        ArgumentTextID BIGINT IDENTITY CONSTRAINT PK_ai_ArgumentText PRIMARY KEY,
        CapabilityKey NVARCHAR(200) NOT NULL,
        ArgumentName NVARCHAR(128) NOT NULL,
        Locale NVARCHAR(20) NOT NULL,
        Description NVARCHAR(MAX) NOT NULL,
        Aliases NVARCHAR(MAX) NULL,
        Examples NVARCHAR(MAX) NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_ai_ArgumentText_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ai_ArgumentText_CapabilityArgument
            FOREIGN KEY (CapabilityKey, ArgumentName)
            REFERENCES ai.CapabilityArgument(CapabilityKey, ArgumentName),
        CONSTRAINT UX_ai_ArgumentText_KeyNameLocale UNIQUE (CapabilityKey, ArgumentName, Locale),
        CONSTRAINT CK_ai_ArgumentText_Aliases_Json CHECK (Aliases IS NULL OR ISJSON(Aliases) = 1),
        CONSTRAINT CK_ai_ArgumentText_Examples_Json CHECK (Examples IS NULL OR ISJSON(Examples) = 1)
    );
END;
GO

MERGE ai.Capability AS target
USING (VALUES
    (N'warehouse.inventory.by-item', N'warehouse', N'inventory', N'warehouse_inventory_by_item', N'sql', N'read', N'ai_warehouse_inventory_by_item', N'read', N'{"type":"object","required":["item"],"properties":{"item":{"type":"string"},"warehouse":{"type":"string"},"asOfDate":{"type":"string","format":"date"}}}', N'{"type":"array"}', N'{"mode":"structured","defaultLocale":"vi-VN"}', N'{"maskFields":["cost","unitCost"]}', N'["warehouse_read"]'),
    (N'warehouse.stock-movement.by-item', N'warehouse', N'inventory', N'warehouse_stock_movement_by_item', N'sql', N'read', N'ai_warehouse_stock_movement_by_item', N'read', N'{"type":"object","required":["item"],"properties":{"item":{"type":"string"},"fromDate":{"type":"string","format":"date"},"toDate":{"type":"string","format":"date"}}}', N'{"type":"array"}', N'{"mode":"structured","defaultLocale":"vi-VN"}', N'{"maskFields":["cost","unitCost"]}', N'["warehouse_read"]'),
    (N'purchasing.po.open-by-supplier', N'purchasing', N'purchase_order', N'purchasing_open_po_by_supplier', N'sql', N'read', N'ai_purchasing_open_po_by_supplier', N'read', N'{"type":"object","required":["supplier"],"properties":{"supplier":{"type":"string"},"season":{"type":"string"}}}', N'{"type":"array"}', N'{"mode":"structured","defaultLocale":"vi-VN"}', N'{"maskFields":[]}', N'["purchasing_read"]'),
    (N'sales.orders.by-customer', N'sales', N'sales_order', N'sales_orders_by_customer', N'sql', N'read', N'ai_sales_orders_by_customer', N'read', N'{"type":"object","required":["customer"],"properties":{"customer":{"type":"string"},"season":{"type":"string"},"status":{"type":"string"}}}', N'{"type":"array"}', N'{"mode":"structured","defaultLocale":"vi-VN"}', N'{"maskFields":["margin"]}', N'["sales_read"]'),
    (N'sales.order.create-preview', N'sales', N'sales_order', N'sales_order_create_preview', N'sql', N'write_preview', N'app_sales_order_create', N'write_preview', N'{"type":"object","required":["customer","item","quantity"],"properties":{"customer":{"type":"string"},"item":{"type":"string"},"quantity":{"type":"number","minimum":0.01},"requestedShipDate":{"type":"string","format":"date"}}}', N'{"type":"object"}', N'{"mode":"structured","defaultLocale":"vi-VN"}', N'{"maskFields":["margin","unitCost"]}', N'["sales_write"]'),
    (N'accounting.receivables.by-customer', N'accounting', N'receivables', N'accounting_receivables_by_customer', N'sql', N'read', N'ai_accounting_receivables_by_customer', N'read', N'{"type":"object","required":["customer"],"properties":{"customer":{"type":"string"},"currency":{"type":"string"},"asOfDate":{"type":"string","format":"date"}}}', N'{"type":"array"}', N'{"mode":"structured","defaultLocale":"vi-VN"}', N'{"maskFields":["creditLimit"]}', N'["accounting_read"]'),
    (N'product_model.by-code', N'product_model', N'product_model', N'product_model_by_code', N'sql', N'read', N'ai_product_model_by_code', N'read', N'{"type":"object","required":["modelCode"],"properties":{"modelCode":{"type":"string"},"season":{"type":"string"}}}', N'{"type":"object"}', N'{"mode":"structured","defaultLocale":"vi-VN"}', N'{"maskFields":[]}', N'["model_read"]')
) AS source (CapabilityKey, Domain, BusinessArea, FunctionName, AdapterType, Operation, StoredProcedure, ExecutionMode, ArgumentContract, ResultSchema, AnswerPolicy, SensitivityPolicy, RequiredRoles)
ON target.CapabilityKey = source.CapabilityKey
WHEN MATCHED THEN UPDATE SET
    Domain = source.Domain,
    BusinessArea = source.BusinessArea,
    FunctionName = source.FunctionName,
    AdapterType = source.AdapterType,
    Operation = source.Operation,
    StoredProcedure = source.StoredProcedure,
    ExecutionMode = source.ExecutionMode,
    ArgumentContract = source.ArgumentContract,
    ResultSchema = source.ResultSchema,
    AnswerPolicy = source.AnswerPolicy,
    SensitivityPolicy = source.SensitivityPolicy,
    RequiredRoles = source.RequiredRoles,
    IsActive = 1,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (CapabilityKey, Domain, BusinessArea, FunctionName, AdapterType, Operation, StoredProcedure, ExecutionMode, ArgumentContract, ResultSchema, AnswerPolicy, SensitivityPolicy, RequiredRoles)
    VALUES (source.CapabilityKey, source.Domain, source.BusinessArea, source.FunctionName, source.AdapterType, source.Operation, source.StoredProcedure, source.ExecutionMode, source.ArgumentContract, source.ResultSchema, source.AnswerPolicy, source.SensitivityPolicy, source.RequiredRoles);
GO

MERGE ai.CapabilityText AS target
USING (VALUES
    (N'warehouse.inventory.by-item', N'en-US', N'Inventory by item', N'Checks stock on hand for an item, optionally filtered by warehouse and date.', N'Use when the user asks available stock, inventory balance, or quantity on hand for an item.', N'Do not use for movement history or valuation.', N'ERP-safe read-only inventory lookup.'),
    (N'warehouse.inventory.by-item', N'vi-VN', N'Tồn kho theo mã hàng', N'Kiểm tra tồn kho hiện có của một mã hàng, có thể lọc theo kho và ngày.', N'Dùng khi người dùng hỏi tồn kho, số lượng khả dụng, hoặc số lượng hiện có của mã hàng.', N'Không dùng cho lịch sử nhập xuất hoặc định giá.', N'Tra cứu tồn kho chỉ đọc, an toàn cho ERP.'),
    (N'warehouse.stock-movement.by-item', N'en-US', N'Stock movement by item', N'Lists inbound and outbound warehouse movements for an item over a date range.', N'Use for receiving, issuing, transfer, and movement-history questions.', N'Do not use for current stock only.', N'Read-only movement trace.'),
    (N'warehouse.stock-movement.by-item', N'vi-VN', N'Nhập xuất kho theo mã hàng', N'Liệt kê các phát sinh nhập, xuất, chuyển kho của một mã hàng trong khoảng ngày.', N'Dùng cho câu hỏi lịch sử nhập xuất, chuyển kho, hoặc phát sinh kho.', N'Không dùng khi chỉ hỏi tồn hiện tại.', N'Truy vết phát sinh kho chỉ đọc.'),
    (N'purchasing.po.open-by-supplier', N'en-US', N'Open POs by supplier', N'Finds open purchase orders for a supplier, optionally by season.', N'Use when the user asks pending, open, or not-yet-completed purchase orders by supplier.', N'Do not use for sales orders or invoices.', N'Purchasing read lookup.'),
    (N'purchasing.po.open-by-supplier', N'vi-VN', N'PO chưa hoàn tất theo nhà cung cấp', N'Tìm các đơn mua hàng còn mở của một nhà cung cấp, có thể lọc theo mùa.', N'Dùng khi hỏi PO còn mở, đang chờ, hoặc chưa hoàn tất theo nhà cung cấp.', N'Không dùng cho đơn bán hàng hoặc hóa đơn.', N'Tra cứu mua hàng chỉ đọc.'),
    (N'sales.orders.by-customer', N'en-US', N'Sales orders by customer', N'Lists sales orders for a customer with optional season and status filters.', N'Use for customer order status, booked orders, and sales order lists.', N'Do not use for accounts receivable balances.', N'Sales read lookup.'),
    (N'sales.orders.by-customer', N'vi-VN', N'Đơn bán hàng theo khách hàng', N'Liệt kê đơn bán hàng của một khách hàng, có thể lọc theo mùa và trạng thái.', N'Dùng cho tình trạng đơn hàng, danh sách đơn bán, hoặc đơn đã nhận theo khách hàng.', N'Không dùng cho công nợ phải thu.', N'Tra cứu bán hàng chỉ đọc.'),
    (N'sales.order.create-preview', N'en-US', N'Preview sales order creation', N'Prepares a sales order create action for explicit user confirmation without executing the write.', N'Use only when the user asks to create a sales order and provides customer, item, and quantity.', N'Do not use for listing orders, updating existing orders, or executing writes directly.', N'Preview-only write capability. Execution requires an approved pending action.'),
    (N'sales.order.create-preview', N'vi-VN', N'Xem trước tạo đơn bán hàng', N'Chuẩn bị thao tác tạo đơn bán hàng để người dùng xác nhận rõ ràng, không thực thi ghi dữ liệu.', N'Chỉ dùng khi người dùng yêu cầu tạo đơn bán hàng và cung cấp khách hàng, mã hàng, số lượng.', N'Không dùng để liệt kê đơn hàng, cập nhật đơn có sẵn, hoặc thực thi ghi trực tiếp.', N'Công cụ ghi dạng xem trước. Thực thi cần pending action đã được phê duyệt.'),
    (N'accounting.receivables.by-customer', N'en-US', N'Receivables by customer', N'Checks accounts receivable balance and aging for a customer.', N'Use when the user asks debt, outstanding balance, overdue amount, or AR by customer.', N'Do not use for sales order lists.', N'Accounting read lookup with sensitive-field masking policy.'),
    (N'accounting.receivables.by-customer', N'vi-VN', N'Công nợ phải thu theo khách hàng', N'Kiểm tra số dư công nợ phải thu và tuổi nợ của một khách hàng.', N'Dùng khi hỏi công nợ, số dư chưa thu, quá hạn, hoặc phải thu theo khách hàng.', N'Không dùng cho danh sách đơn bán hàng.', N'Tra cứu kế toán chỉ đọc với chính sách che trường nhạy cảm.'),
    (N'product_model.by-code', N'en-US', N'Product model by code', N'Looks up product model details by model code, optionally by season.', N'Use for product model, item model, code, construction, and specification questions.', N'Do not use for generic AI model/provider questions.', N'Use product_model naming to avoid conflict with technical model/provider modules.'),
    (N'product_model.by-code', N'vi-VN', N'Mẫu sản phẩm theo mã', N'Tra cứu thông tin mẫu sản phẩm theo mã model, có thể lọc theo mùa.', N'Dùng cho câu hỏi mã mẫu, model sản phẩm, cấu trúc hoặc thông số mẫu.', N'Không dùng cho câu hỏi về AI model/provider kỹ thuật.', N'Dùng tên product_model để tránh nhầm với module model kỹ thuật.')
) AS source (CapabilityKey, Locale, ShortName, Description, UseWhen, DoNotUseWhen, BusinessNotes)
ON target.CapabilityKey = source.CapabilityKey AND target.Locale = source.Locale
WHEN MATCHED THEN UPDATE SET
    ShortName = source.ShortName,
    Description = source.Description,
    UseWhen = source.UseWhen,
    DoNotUseWhen = source.DoNotUseWhen,
    BusinessNotes = source.BusinessNotes,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (CapabilityKey, Locale, ShortName, Description, UseWhen, DoNotUseWhen, BusinessNotes)
    VALUES (source.CapabilityKey, source.Locale, source.ShortName, source.Description, source.UseWhen, source.DoNotUseWhen, source.BusinessNotes);
GO

MERGE ai.CapabilityArgument AS target
USING (VALUES
    (N'warehouse.inventory.by-item', N'item', N'@Item', N'string', 1, NULL, N'{"minLength":1}', N'{"askWhenMissing":true}', 1),
    (N'warehouse.inventory.by-item', N'warehouse', N'@Warehouse', N'string', 0, NULL, N'{"minLength":1}', NULL, 2),
    (N'warehouse.stock-movement.by-item', N'item', N'@Item', N'string', 1, NULL, N'{"minLength":1}', N'{"askWhenMissing":true}', 1),
    (N'warehouse.stock-movement.by-item', N'fromDate', N'@FromDate', N'date', 0, N'default_time_window', N'{"format":"date"}', NULL, 2),
    (N'warehouse.stock-movement.by-item', N'toDate', N'@ToDate', N'date', 0, N'today', N'{"format":"date"}', NULL, 3),
    (N'purchasing.po.open-by-supplier', N'supplier', N'@Supplier', N'string', 1, NULL, N'{"minLength":1}', N'{"askWhenMissing":true}', 1),
    (N'purchasing.po.open-by-supplier', N'season', N'@Season', N'string', 0, NULL, N'{"minLength":1}', NULL, 2),
    (N'sales.orders.by-customer', N'customer', N'@Customer', N'string', 1, NULL, N'{"minLength":1}', N'{"askWhenMissing":true}', 1),
    (N'sales.orders.by-customer', N'season', N'@Season', N'string', 0, NULL, N'{"minLength":1}', NULL, 2),
    (N'sales.orders.by-customer', N'status', N'@Status', N'string', 0, NULL, N'{"allowed":["open","closed","cancelled","all"]}', NULL, 3),
    (N'sales.order.create-preview', N'customer', N'@Customer', N'string', 1, NULL, N'{"minLength":1}', N'{"askWhenMissing":true}', 1),
    (N'sales.order.create-preview', N'item', N'@Item', N'string', 1, NULL, N'{"minLength":1}', N'{"askWhenMissing":true}', 2),
    (N'sales.order.create-preview', N'quantity', N'@Quantity', N'decimal', 1, NULL, N'{"min":0.01}', N'{"askWhenMissing":true}', 3),
    (N'sales.order.create-preview', N'requestedShipDate', N'@RequestedShipDate', N'date', 0, NULL, N'{"format":"date"}', NULL, 4),
    (N'accounting.receivables.by-customer', N'customer', N'@Customer', N'string', 1, NULL, N'{"minLength":1}', N'{"askWhenMissing":true}', 1),
    (N'accounting.receivables.by-customer', N'currency', N'@Currency', N'string', 0, NULL, N'{"pattern":"^[A-Z]{3}$"}', NULL, 2),
    (N'product_model.by-code', N'modelCode', N'@ModelCode', N'string', 1, NULL, N'{"minLength":1}', N'{"askWhenMissing":true}', 1),
    (N'product_model.by-code', N'season', N'@Season', N'string', 0, NULL, N'{"minLength":1}', NULL, 2)
) AS source (CapabilityKey, ArgumentName, ProcParameterName, DataType, IsRequired, DefaultSource, ValidationRule, ClarificationPolicy, DisplayOrder)
ON target.CapabilityKey = source.CapabilityKey AND target.ArgumentName = source.ArgumentName
WHEN MATCHED THEN UPDATE SET
    ProcParameterName = source.ProcParameterName,
    DataType = source.DataType,
    IsRequired = source.IsRequired,
    DefaultSource = source.DefaultSource,
    ValidationRule = source.ValidationRule,
    ClarificationPolicy = source.ClarificationPolicy,
    DisplayOrder = source.DisplayOrder,
    IsActive = 1
WHEN NOT MATCHED THEN INSERT
    (CapabilityKey, ArgumentName, ProcParameterName, DataType, IsRequired, DefaultSource, ValidationRule, ClarificationPolicy, DisplayOrder)
    VALUES (source.CapabilityKey, source.ArgumentName, source.ProcParameterName, source.DataType, source.IsRequired, source.DefaultSource, source.ValidationRule, source.ClarificationPolicy, source.DisplayOrder);
GO
