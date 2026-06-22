using System;
using EXE.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EXE.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260620010000_AddSellerApprovalStatus")]
    public partial class AddSellerApprovalStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'SellerApprovalStatus') IS NULL
BEGIN
    ALTER TABLE [Users] ADD [SellerApprovalStatus] nvarchar(20) NOT NULL CONSTRAINT [DF_Users_SellerApprovalStatus] DEFAULT N'Approved';
END

IF COL_LENGTH('Users', 'SellerApprovedAt') IS NULL
BEGIN
    ALTER TABLE [Users] ADD [SellerApprovedAt] datetime NULL;
END

IF COL_LENGTH('Users', 'SellerApprovedByAdminId') IS NULL
BEGIN
    ALTER TABLE [Users] ADD [SellerApprovedByAdminId] int NULL;
END

IF COL_LENGTH('Users', 'SellerRejectReason') IS NULL
BEGIN
    ALTER TABLE [Users] ADD [SellerRejectReason] nvarchar(500) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_SellerApprovedByAdminId' AND object_id = OBJECT_ID('Users'))
BEGIN
    CREATE INDEX [IX_Users_SellerApprovedByAdminId] ON [Users] ([SellerApprovedByAdminId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Users_SellerApprovedByAdminId')
BEGIN
    ALTER TABLE [Users]
    ADD CONSTRAINT [FK_Users_Users_SellerApprovedByAdminId]
    FOREIGN KEY ([SellerApprovedByAdminId]) REFERENCES [Users] ([UserId]);
END
");

            migrationBuilder.Sql(@"
UPDATE u
SET SellerApprovalStatus = 'Approved',
    SellerApprovedAt = COALESCE(SellerApprovedAt, GETDATE())
FROM Users u
INNER JOIN Roles r ON u.RoleId = r.RoleId
WHERE LOWER(LTRIM(RTRIM(r.RoleName))) = 'seller';
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_SellerApprovedByAdminId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_SellerApprovedByAdminId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SellerApprovalStatus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SellerApprovedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SellerApprovedByAdminId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SellerRejectReason",
                table: "Users");
        }
    }
}
