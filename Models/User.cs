using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Licitaciones.Models;

[Table("users")]
[Index("Email", Name = "users_email_key", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    [StringLength(100)]
    [Required]
    public string Name { get; set; } = null!;

    [Column("email")]
    [StringLength(100)]
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Column("password_hash")]
    [Required]
    public string PasswordHash { get; set; } = null!;

    [Column("role")]
    [StringLength(20)]
    [Required]
    public string Role { get; set; } = null!;

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Evaluator")]
    public virtual ICollection<Evaluation> Evaluations { get; set; }

    [InverseProperty("Provider")]
    public virtual ICollection<Proposal> Proposals { get; set; }

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<Tender> Tenders { get; set; }

    public User()
    {
        Proposals = new HashSet<Proposal>();
        Evaluations = new HashSet<Evaluation>();
        Tenders = new HashSet<Tender>();
    }
}
