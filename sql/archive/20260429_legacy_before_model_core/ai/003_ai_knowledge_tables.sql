IF OBJECT_ID('ai.KnowledgeChunk', 'U') IS NULL
BEGIN
    EXEC(N'
    CREATE TABLE ai.KnowledgeChunk
    (
        ChunkID BIGINT IDENTITY CONSTRAINT PK_ai_KnowledgeChunk PRIMARY KEY,
        TenantID NVARCHAR(100) NULL,
        ChunkType NVARCHAR(50) NOT NULL,
        ObjectKey NVARCHAR(300) NOT NULL,
        Domain NVARCHAR(100) NULL,
        Locale NVARCHAR(20) NULL,
        Title NVARCHAR(300) NULL,
        ContentText NVARCHAR(MAX) NOT NULL,
        Metadata NVARCHAR(MAX) NULL,
        Embedding VECTOR(1536) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_ai_KnowledgeChunk_IsActive DEFAULT (1),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_ai_KnowledgeChunk_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_ai_KnowledgeChunk_Metadata_Json CHECK (Metadata IS NULL OR ISJSON(Metadata) = 1),
        CONSTRAINT CK_ai_KnowledgeChunk_ChunkType CHECK (ChunkType IN (N''domain'', N''capability'', N''argument'', N''result_field'', N''glossary'', N''example'', N''entity_alias''))
    );');
END;
GO

MERGE ai.ArgumentText AS target
USING (VALUES
    (N'warehouse.inventory.by-item', N'item', N'en-US', N'Item code, item name, SKU, or product identifier to check inventory for.', N'["item","sku","product","material"]', N'["stock for CHAIR-001","inventory of fabric code F001"]'),
    (N'warehouse.inventory.by-item', N'item', N'vi-VN', N'Mã hàng, tên hàng, SKU, hoặc định danh sản phẩm cần kiểm tra tồn kho.', N'["mã hàng","hàng","sku","vật tư"]', N'["tồn kho mã CHAIR-001","tồn vải F001"]'),
    (N'warehouse.stock-movement.by-item', N'item', N'en-US', N'Item code or SKU whose warehouse movements should be listed.', N'["item","sku","movement item"]', N'["movement history for CHAIR-001"]'),
    (N'warehouse.stock-movement.by-item', N'item', N'vi-VN', N'Mã hàng hoặc SKU cần xem lịch sử nhập xuất kho.', N'["mã hàng","nhập xuất","phát sinh kho"]', N'["lịch sử nhập xuất CHAIR-001"]'),
    (N'purchasing.po.open-by-supplier', N'supplier', N'en-US', N'Supplier code, supplier name, or known supplier alias.', N'["supplier","vendor","factory"]', N'["open POs for ABC Supplier"]'),
    (N'purchasing.po.open-by-supplier', N'supplier', N'vi-VN', N'Mã nhà cung cấp, tên nhà cung cấp, hoặc tên gọi thường dùng.', N'["nhà cung cấp","vendor","xưởng"]', N'["PO còn mở của ABC"]'),
    (N'sales.orders.by-customer', N'customer', N'en-US', N'Customer code, customer name, buyer, or known customer alias.', N'["customer","buyer","client"]', N'["orders for IKEA"]'),
    (N'sales.orders.by-customer', N'customer', N'vi-VN', N'Mã khách hàng, tên khách hàng, buyer, hoặc tên gọi thường dùng.', N'["khách hàng","buyer","người mua"]', N'["đơn hàng của IKEA"]'),
    (N'sales.order.create-preview', N'customer', N'en-US', N'Customer code, customer name, buyer, or known customer alias for the new sales order.', N'["customer","buyer","client"]', N'["create sales order for IKEA"]'),
    (N'sales.order.create-preview', N'customer', N'vi-VN', N'Mã khách hàng, tên khách hàng, buyer, hoặc tên gọi thường dùng cho đơn bán hàng mới.', N'["khách hàng","buyer","người mua"]', N'["tạo đơn bán hàng cho IKEA"]'),
    (N'sales.order.create-preview', N'item', N'en-US', N'Item code, SKU, or product identifier to add to the sales order.', N'["item","sku","product","material"]', N'["item CHAIR-001"]'),
    (N'sales.order.create-preview', N'item', N'vi-VN', N'Mã hàng, SKU, hoặc định danh sản phẩm cần thêm vào đơn bán hàng.', N'["mã hàng","hàng","sku","vật tư"]', N'["mã hàng CHAIR-001"]'),
    (N'sales.order.create-preview', N'quantity', N'en-US', N'Quantity to create on the sales order line.', N'["quantity","qty","amount"]', N'["quantity 500"]'),
    (N'sales.order.create-preview', N'quantity', N'vi-VN', N'Số lượng cần tạo trên dòng đơn bán hàng.', N'["số lượng","sl","qty"]', N'["số lượng 500"]'),
    (N'accounting.receivables.by-customer', N'customer', N'en-US', N'Customer code, customer name, buyer, or known customer alias for receivables lookup.', N'["customer","buyer","client","debtor"]', N'["receivables for IKEA"]'),
    (N'accounting.receivables.by-customer', N'customer', N'vi-VN', N'Mã khách hàng, tên khách hàng, buyer, hoặc tên gọi dùng để tra công nợ phải thu.', N'["khách hàng","công nợ","phải thu"]', N'["công nợ IKEA"]'),
    (N'product_model.by-code', N'modelCode', N'en-US', N'Product model code or item model identifier.', N'["model","product model","model code"]', N'["model M123 details"]'),
    (N'product_model.by-code', N'modelCode', N'vi-VN', N'Mã model sản phẩm hoặc định danh mẫu hàng.', N'["mã mẫu","model","mã model"]', N'["thông tin model M123"]')
) AS source (CapabilityKey, ArgumentName, Locale, Description, Aliases, Examples)
ON target.CapabilityKey = source.CapabilityKey AND target.ArgumentName = source.ArgumentName AND target.Locale = source.Locale
WHEN MATCHED THEN UPDATE SET
    Description = source.Description,
    Aliases = source.Aliases,
    Examples = source.Examples,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (CapabilityKey, ArgumentName, Locale, Description, Aliases, Examples)
    VALUES (source.CapabilityKey, source.ArgumentName, source.Locale, source.Description, source.Aliases, source.Examples);
GO

MERGE ai.KnowledgeChunk AS target
USING (VALUES
    (NULL, N'capability', N'warehouse.inventory.by-item', N'warehouse', N'en-US', N'Inventory by item', N'Use warehouse.inventory.by-item for stock on hand, inventory balance, available quantity, or current warehouse stock for an item.', N'{"capabilityKey":"warehouse.inventory.by-item"}'),
    (NULL, N'capability', N'warehouse.inventory.by-item', N'warehouse', N'vi-VN', N'Tồn kho theo mã hàng', N'Dùng warehouse.inventory.by-item cho tồn kho, số lượng hiện có, số lượng khả dụng, hoặc tồn hiện tại theo mã hàng.', N'{"capabilityKey":"warehouse.inventory.by-item"}'),
    (NULL, N'capability', N'warehouse.stock-movement.by-item', N'warehouse', N'en-US', N'Stock movement by item', N'Use warehouse.stock-movement.by-item for receiving, issuing, transfer, stock card, or movement history questions.', N'{"capabilityKey":"warehouse.stock-movement.by-item"}'),
    (NULL, N'capability', N'warehouse.stock-movement.by-item', N'warehouse', N'vi-VN', N'Nhập xuất kho theo mã hàng', N'Dùng warehouse.stock-movement.by-item cho nhập xuất, chuyển kho, thẻ kho, hoặc lịch sử phát sinh kho.', N'{"capabilityKey":"warehouse.stock-movement.by-item"}'),
    (NULL, N'capability', N'purchasing.po.open-by-supplier', N'purchasing', N'en-US', N'Open POs by supplier', N'Use purchasing.po.open-by-supplier for open purchase orders, pending PO, not completed PO, or supplier backlog.', N'{"capabilityKey":"purchasing.po.open-by-supplier"}'),
    (NULL, N'capability', N'purchasing.po.open-by-supplier', N'purchasing', N'vi-VN', N'PO chưa hoàn tất theo nhà cung cấp', N'Dùng purchasing.po.open-by-supplier cho PO còn mở, đơn mua đang chờ, hoặc đơn mua chưa hoàn tất theo nhà cung cấp.', N'{"capabilityKey":"purchasing.po.open-by-supplier"}'),
    (NULL, N'capability', N'sales.orders.by-customer', N'sales', N'en-US', N'Sales orders by customer', N'Use sales.orders.by-customer for sales orders, customer order status, booked order, or buyer order list.', N'{"capabilityKey":"sales.orders.by-customer"}'),
    (NULL, N'capability', N'sales.orders.by-customer', N'sales', N'vi-VN', N'Đơn bán hàng theo khách hàng', N'Dùng sales.orders.by-customer cho đơn bán hàng, tình trạng đơn hàng, đơn đã nhận, hoặc danh sách đơn theo khách hàng.', N'{"capabilityKey":"sales.orders.by-customer"}'),
    (NULL, N'capability', N'sales.order.create-preview', N'sales', N'en-US', N'Preview sales order creation', N'Use sales.order.create-preview only to prepare a new sales order create action for confirmation. It creates a pending action and does not execute the write.', N'{"capabilityKey":"sales.order.create-preview"}'),
    (NULL, N'capability', N'sales.order.create-preview', N'sales', N'vi-VN', N'Xem trước tạo đơn bán hàng', N'Dùng sales.order.create-preview chỉ để chuẩn bị thao tác tạo đơn bán hàng cần xác nhận. Công cụ tạo pending action và không ghi dữ liệu trực tiếp.', N'{"capabilityKey":"sales.order.create-preview"}'),
    (NULL, N'capability', N'accounting.receivables.by-customer', N'accounting', N'en-US', N'Receivables by customer', N'Use accounting.receivables.by-customer for AR, receivables, debt, outstanding balance, overdue balance, or customer aging.', N'{"capabilityKey":"accounting.receivables.by-customer"}'),
    (NULL, N'capability', N'accounting.receivables.by-customer', N'accounting', N'vi-VN', N'Công nợ phải thu theo khách hàng', N'Dùng accounting.receivables.by-customer cho công nợ phải thu, dư nợ, số tiền chưa thu, quá hạn, hoặc tuổi nợ khách hàng.', N'{"capabilityKey":"accounting.receivables.by-customer"}'),
    (NULL, N'capability', N'product_model.by-code', N'product_model', N'en-US', N'Product model by code', N'Use product_model.by-code for product model, model code, item model, product construction, or model specification lookup.', N'{"capabilityKey":"product_model.by-code"}'),
    (NULL, N'capability', N'product_model.by-code', N'product_model', N'vi-VN', N'Mẫu sản phẩm theo mã', N'Dùng product_model.by-code cho mã mẫu, model sản phẩm, cấu trúc sản phẩm, hoặc thông số mẫu hàng.', N'{"capabilityKey":"product_model.by-code"}'),
    (NULL, N'glossary', N'warehouse.glossary.stock', N'warehouse', N'en-US', N'Warehouse glossary', N'Stock, inventory, on hand, available quantity, balance, warehouse quantity all indicate the warehouse domain.', N'{"domains":["warehouse"]}'),
    (NULL, N'glossary', N'accounting.glossary.receivable', N'accounting', N'en-US', N'Accounting glossary', N'Receivable, AR, outstanding, overdue, debt, aging, payment due indicate accounting receivables.', N'{"domains":["accounting"]}'),
    (NULL, N'example', N'sales.orders.by-customer.example', N'sales', N'en-US', N'Example sales order request', N'User says show all open orders for IKEA this season. Route to sales.orders.by-customer with customer IKEA and status open.', N'{"capabilityKey":"sales.orders.by-customer"}'),
    (NULL, N'example', N'sales.order.create-preview.example', N'sales', N'en-US', N'Example sales order create preview', N'User says create sales order for customer IKEA item CHAIR-001 quantity 500. Route to sales.order.create-preview and return a confirmation card.', N'{"capabilityKey":"sales.order.create-preview"}')
) AS source (TenantID, ChunkType, ObjectKey, Domain, Locale, Title, ContentText, Metadata)
ON ISNULL(target.TenantID, N'') = ISNULL(source.TenantID, N'')
   AND target.ChunkType = source.ChunkType
   AND target.ObjectKey = source.ObjectKey
   AND ISNULL(target.Locale, N'') = ISNULL(source.Locale, N'')
WHEN MATCHED THEN UPDATE SET
    Domain = source.Domain,
    Title = source.Title,
    ContentText = source.ContentText,
    Metadata = source.Metadata,
    IsActive = 1,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (TenantID, ChunkType, ObjectKey, Domain, Locale, Title, ContentText, Metadata)
    VALUES (source.TenantID, source.ChunkType, source.ObjectKey, source.Domain, source.Locale, source.Title, source.ContentText, source.Metadata);
GO
