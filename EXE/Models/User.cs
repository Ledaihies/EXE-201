using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

[Index("Email", Name = "UQ__Users__A9D10534EEEA413F", IsUnique = true)]
public partial class User
{
    [Key]
    public int UserId { get; set; }

    [StringLength(150)]
    public string? FullName { get; set; }

    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(255)]
    public string? PasswordHash { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(120)]
    public string? BankName { get; set; }

    [StringLength(50)]
    public string? BankAccountNumber { get; set; }

    [StringLength(150)]
    public string? BankAccountName { get; set; }

    [StringLength(150)]
    public string? BankBranch { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? BankUpdatedDate { get; set; }

    public int? RoleId { get; set; }

    [StringLength(20)]
    public string? SellerApprovalStatus { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? SellerApprovedAt { get; set; }

    public int? SellerApprovedByAdminId { get; set; }

    [StringLength(500)]
    public string? SellerRejectReason { get; set; }

    [StringLength(255)]
    public string? SellerLicenseImageUrl { get; set; }

    [StringLength(255)]
    public string? SellerOriginProofImageUrl { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    [InverseProperty("User")]
    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    [InverseProperty("User")]
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    [InverseProperty("User")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    [InverseProperty("User")]
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    [ForeignKey("RoleId")]
    [InverseProperty("Users")]
    public virtual Role? Role { get; set; }

    [ForeignKey("SellerApprovedByAdminId")]
    [InverseProperty("SellerApprovedUsers")]
    public virtual User? SellerApprovedByAdmin { get; set; }

    [InverseProperty("Seller")]
    public virtual ICollection<Product> SellerProducts { get; set; } = new List<Product>();

    [InverseProperty("SellerApprovedByAdmin")]
    public virtual ICollection<User> SellerApprovedUsers { get; set; } = new List<User>();

    [InverseProperty("User")]
    public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
}
