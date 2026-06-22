using System;
using EXE.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EXE.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260620030000_AddNotifications")]
    public partial class AddNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('Notifications', 'U') IS NULL
BEGIN
    CREATE TABLE [Notifications](
        [NotificationId] int NOT NULL IDENTITY(1,1),
        [UserId] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [Type] nvarchar(50) NOT NULL CONSTRAINT [DF_Notifications_Type] DEFAULT(N'System'),
        [RelatedEntityType] nvarchar(100) NULL,
        [RelatedEntityId] int NULL,
        [IsRead] bit NOT NULL CONSTRAINT [DF_Notifications_IsRead] DEFAULT(0),
        [CreatedAt] datetime NOT NULL CONSTRAINT [DF_Notifications_CreatedAt] DEFAULT(getdate()),
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([NotificationId]),
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notifications_UserId_IsRead_CreatedAt' AND object_id = OBJECT_ID('Notifications'))
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead_CreatedAt] ON [Notifications] ([UserId], [IsRead], [CreatedAt]);
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('Notifications', 'U') IS NOT NULL
BEGIN
    DROP TABLE [Notifications];
END
");
        }
    }
}
