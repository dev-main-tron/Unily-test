-- Unily-insert.sql
-- Test data for the optional SQL Server storage (db/Unily.sql): 20 products and 20 reservations.
-- Consistent with the business rules: Products.Stock = initial stock - SUM(Quantity) of Active + Confirmed
-- reservations; Released and Expired reservations no longer hold stock. Dates are UTC and relative to the
-- moment the script runs, so Active reservations are still within their TTL right after loading.
-- WARNING: deletes the existing rows of both tables before inserting (re-runnable).
USE [Unily];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;



DELETE FROM dbo.Reservations;
DELETE FROM dbo.Products;

-- ───── Products (Stock = available units after the reservations below) ─────
INSERT INTO dbo.Products (Id, Name, Stock) VALUES
    ('10000000-0000-0000-0000-000000000001', N'Notebook ThinkPad T14', 37),  -- initial 40, reserved 3
    ('10000000-0000-0000-0000-000000000002', N'Monitor Dell 27" 4K', 22),  -- initial 25, reserved 3
    ('10000000-0000-0000-0000-000000000003', N'Teclado mecánico Logitech', 60),  -- initial 60, reserved 0
    ('10000000-0000-0000-0000-000000000004', N'Mouse inalámbrico MX Master', 76),  -- initial 80, reserved 4
    ('10000000-0000-0000-0000-000000000005', N'Auriculares Sony WH-1000XM5', 30),  -- initial 30, reserved 0
    ('10000000-0000-0000-0000-000000000006', N'Webcam Logitech C920', 43),  -- initial 45, reserved 2
    ('10000000-0000-0000-0000-000000000007', N'Dock USB-C Lenovo', 34),  -- initial 35, reserved 1
    ('10000000-0000-0000-0000-000000000008', N'Disco SSD Samsung 1TB', 64),  -- initial 70, reserved 6
    ('10000000-0000-0000-0000-000000000009', N'Memoria RAM 32GB DDR5', 50),  -- initial 50, reserved 0
    ('10000000-0000-0000-0000-000000000010', N'Silla ergonómica Herman Miller', 11),  -- initial 12, reserved 1
    ('10000000-0000-0000-0000-000000000011', N'Escritorio regulable', 15),  -- initial 15, reserved 0
    ('10000000-0000-0000-0000-000000000012', N'Impresora HP LaserJet', 19),  -- initial 20, reserved 1
    ('10000000-0000-0000-0000-000000000013', N'Router Wi-Fi 6 TP-Link', 37),  -- initial 40, reserved 3
    ('10000000-0000-0000-0000-000000000014', N'Tablet Samsung Galaxy Tab S9', 18),  -- initial 18, reserved 0
    ('10000000-0000-0000-0000-000000000015', N'Smartphone Pixel 9', 21),  -- initial 22, reserved 1
    ('10000000-0000-0000-0000-000000000016', N'Proyector Epson', 10),  -- initial 10, reserved 0
    ('10000000-0000-0000-0000-000000000017', N'Parlante Bluetooth JBL', 50),  -- initial 55, reserved 5
    ('10000000-0000-0000-0000-000000000018', N'Cargador GaN 100W', 80),  -- initial 90, reserved 10
    ('10000000-0000-0000-0000-000000000019', N'Hub USB 7 puertos', 65),  -- initial 65, reserved 0
    ('10000000-0000-0000-0000-000000000020', N'Licencia Office 365 (1 año)', 100);  -- initial 100, reserved 0

    DECLARE @now datetime2 = SYSUTCDATETIME();

-- ───── Reservations: 8 Active, 5 Confirmed, 4 Released, 3 Expired ─────
INSERT INTO dbo.Reservations (Id, ProductId, Quantity, CreatedAt, ExpiresAt, Status) VALUES
    ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 2, DATEADD(MINUTE, -5, @now), DATEADD(SECOND, 1800, DATEADD(MINUTE, -5, @now)), 'Active'),
    ('20000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 1, DATEADD(MINUTE, -120, @now), DATEADD(SECOND, 600, DATEADD(MINUTE, -120, @now)), 'Confirmed'),
    ('20000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000002', 3, DATEADD(MINUTE, -2, @now), DATEADD(SECOND, 3600, DATEADD(MINUTE, -2, @now)), 'Active'),
    ('20000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000003', 5, DATEADD(MINUTE, -300, @now), DATEADD(SECOND, 900, DATEADD(MINUTE, -300, @now)), 'Released'),
    ('20000000-0000-0000-0000-000000000005', '10000000-0000-0000-0000-000000000004', 4, DATEADD(MINUTE, -60, @now), DATEADD(SECOND, 600, DATEADD(MINUTE, -60, @now)), 'Confirmed'),
    ('20000000-0000-0000-0000-000000000006', '10000000-0000-0000-0000-000000000005', 1, DATEADD(MINUTE, -90, @now), DATEADD(SECOND, 300, DATEADD(MINUTE, -90, @now)), 'Expired'),
    ('20000000-0000-0000-0000-000000000007', '10000000-0000-0000-0000-000000000006', 2, DATEADD(MINUTE, -10, @now), DATEADD(SECOND, 1800, DATEADD(MINUTE, -10, @now)), 'Active'),
    ('20000000-0000-0000-0000-000000000008', '10000000-0000-0000-0000-000000000007', 1, DATEADD(MINUTE, -30, @now), DATEADD(SECOND, 900, DATEADD(MINUTE, -30, @now)), 'Confirmed'),
    ('20000000-0000-0000-0000-000000000009', '10000000-0000-0000-0000-000000000008', 6, DATEADD(MINUTE, -1, @now), DATEADD(SECOND, 3600, DATEADD(MINUTE, -1, @now)), 'Active'),
    ('20000000-0000-0000-0000-000000000010', '10000000-0000-0000-0000-000000000009', 2, DATEADD(MINUTE, -45, @now), DATEADD(SECOND, 600, DATEADD(MINUTE, -45, @now)), 'Released'),
    ('20000000-0000-0000-0000-000000000011', '10000000-0000-0000-0000-000000000010', 1, DATEADD(MINUTE, -3, @now), DATEADD(SECOND, 1200, DATEADD(MINUTE, -3, @now)), 'Active'),
    ('20000000-0000-0000-0000-000000000012', '10000000-0000-0000-0000-000000000011', 2, DATEADD(MINUTE, -200, @now), DATEADD(SECOND, 600, DATEADD(MINUTE, -200, @now)), 'Expired'),
    ('20000000-0000-0000-0000-000000000013', '10000000-0000-0000-0000-000000000012', 1, DATEADD(MINUTE, -15, @now), DATEADD(SECOND, 900, DATEADD(MINUTE, -15, @now)), 'Confirmed'),
    ('20000000-0000-0000-0000-000000000014', '10000000-0000-0000-0000-000000000013', 3, DATEADD(MINUTE, -7, @now), DATEADD(SECOND, 2400, DATEADD(MINUTE, -7, @now)), 'Active'),
    ('20000000-0000-0000-0000-000000000015', '10000000-0000-0000-0000-000000000014', 2, DATEADD(MINUTE, -25, @now), DATEADD(SECOND, 600, DATEADD(MINUTE, -25, @now)), 'Released'),
    ('20000000-0000-0000-0000-000000000016', '10000000-0000-0000-0000-000000000015', 1, DATEADD(MINUTE, -4, @now), DATEADD(SECOND, 1800, DATEADD(MINUTE, -4, @now)), 'Active'),
    ('20000000-0000-0000-0000-000000000017', '10000000-0000-0000-0000-000000000016', 1, DATEADD(MINUTE, -180, @now), DATEADD(SECOND, 300, DATEADD(MINUTE, -180, @now)), 'Expired'),
    ('20000000-0000-0000-0000-000000000018', '10000000-0000-0000-0000-000000000017', 5, DATEADD(MINUTE, -20, @now), DATEADD(SECOND, 900, DATEADD(MINUTE, -20, @now)), 'Confirmed'),
    ('20000000-0000-0000-0000-000000000019', '10000000-0000-0000-0000-000000000018', 10, DATEADD(MINUTE, -6, @now), DATEADD(SECOND, 3600, DATEADD(MINUTE, -6, @now)), 'Active'),
    ('20000000-0000-0000-0000-000000000020', '10000000-0000-0000-0000-000000000019', 3, DATEADD(MINUTE, -40, @now), DATEADD(SECOND, 600, DATEADD(MINUTE, -40, @now)), 'Released');

COMMIT TRANSACTION;
GO


