IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'BankName')
    ALTER TABLE Users ADD BankName NVARCHAR(120) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'BankAccountNumber')
    ALTER TABLE Users ADD BankAccountNumber NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'BankAccountName')
    ALTER TABLE Users ADD BankAccountName NVARCHAR(150) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'BankBranch')
    ALTER TABLE Users ADD BankBranch NVARCHAR(150) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'BankUpdatedDate')
    ALTER TABLE Users ADD BankUpdatedDate DATETIME NULL;
