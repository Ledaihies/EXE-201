using System;
using EXE.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EXE.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260620040000_AddWebsiteVisits")]
    public partial class AddWebsiteVisits : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('WebsiteVisits', 'U') IS NULL
BEGIN
    CREATE TABLE [WebsiteVisits](
        [WebsiteVisitId] int NOT NULL IDENTITY(1,1),
        [VisitorKey] nvarchar(128) NOT NULL,
        [UserId] int NULL,
        [SessionId] nvarchar(128) NULL,
        [IpAddress] nvarchar(45) NULL,
        [UserAgent] nvarchar(255) NULL,
        [Path] nvarchar(255) NULL,
        [VisitDate] date NOT NULL,
        [VisitedAt] datetime NOT NULL CONSTRAINT [DF_WebsiteVisits_VisitedAt] DEFAULT(getdate()),
        CONSTRAINT [PK_WebsiteVisits] PRIMARY KEY ([WebsiteVisitId]),
        CONSTRAINT [FK_WebsiteVisits_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE SET NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WebsiteVisits_VisitDate_VisitorKey' AND object_id = OBJECT_ID('WebsiteVisits'))
BEGIN
    CREATE UNIQUE INDEX [IX_WebsiteVisits_VisitDate_VisitorKey] ON [WebsiteVisits] ([VisitDate], [VisitorKey]);
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('WebsiteVisits', 'U') IS NOT NULL
BEGIN
    DROP TABLE [WebsiteVisits];
END
");
        }
    }
}
