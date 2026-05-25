USE [Clyiqhub];
SET NOCOUNT ON;

-- 1) Ensure unique indexes on Tenants
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Tenants_Domain' AND object_id=OBJECT_ID('dbo.Tenants'))
  CREATE UNIQUE INDEX UX_Tenants_Domain ON dbo.Tenants(Domain);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Tenants_Email' AND object_id=OBJECT_ID('dbo.Tenants'))
  CREATE UNIQUE INDEX UX_Tenants_Email ON dbo.Tenants(Email);

-- 2) FK: Tenants.Country -> Country.CountryId (your column is named "Country")
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Tenants_Country')
BEGIN
  ALTER TABLE dbo.Tenants
    ADD CONSTRAINT FK_Tenants_Country FOREIGN KEY ([Country]) REFERENCES dbo.Country(CountryId);
END

-- 3) ApprovalRequests table exists (generic approvals). If not, create minimal shape.
IF OBJECT_ID(N'dbo.ApprovalRequests', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.ApprovalRequests
  (
    ApprovalReqestId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    EntityType NVARCHAR(50) NOT NULL,     -- 'tenant'
    EntityId   UNIQUEIDENTIFIER NOT NULL, -- Tenants.TenantId
    Action     NVARCHAR(50) NOT NULL,     -- 'create' | 'update' | 'suspend' | ...
    Status     NVARCHAR(20) NOT NULL,     -- 'pending' | 'approved' | 'rejected'
    RequestedBy UNIQUEIDENTIFIER NOT NULL,
    RequestedAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    ReviewerId  UNIQUEIDENTIFIER NULL,
    ReviewedAt  DATETIME2(0) NULL,
    RejectionReason NVARCHAR(255) NULL,
    PayloadSnapshot NVARCHAR(MAX) NULL
  );
  CREATE INDEX IX_ApprovalRequests_Entity ON dbo.ApprovalRequests(EntityType, EntityId, Status);
END