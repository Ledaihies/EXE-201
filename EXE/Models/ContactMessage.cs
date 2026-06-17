using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EXE.Models;

public partial class ContactMessage
{
    [Key]
    public int MessageId { get; set; }

    [StringLength(150)]
    public string? Name { get; set; }

    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(200)]
    public string? Subject { get; set; }

    public string? Message { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }
}
