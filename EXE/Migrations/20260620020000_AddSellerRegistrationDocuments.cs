using System;
using EXE.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EXE.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260620020000_AddSellerRegistrationDocuments")]
    public partial class AddSellerRegistrationDocuments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'SellerLicenseImageUrl') IS NULL
BEGIN
    ALTER TABLE [Users] ADD [SellerLicenseImageUrl] nvarchar(255) NULL;
END

IF COL_LENGTH('Users', 'SellerOriginProofImageUrl') IS NULL
BEGIN
    ALTER TABLE [Users] ADD [SellerOriginProofImageUrl] nvarchar(255) NULL;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'SellerOriginProofImageUrl') IS NOT NULL
BEGIN
    ALTER TABLE [Users] DROP COLUMN [SellerOriginProofImageUrl];
END

IF COL_LENGTH('Users', 'SellerLicenseImageUrl') IS NOT NULL
BEGIN
    ALTER TABLE [Users] DROP COLUMN [SellerLicenseImageUrl];
END
");
        }
    }
}
