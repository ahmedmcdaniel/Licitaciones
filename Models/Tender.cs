using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Licitaciones.Models;

[Table("tenders")]
public partial class Tender
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("title")]
    [StringLength(200)]
    public string Title { get; set; }

    [Required]
    [Column("description")]
    public string Description { get; set; }

    [Column("requirements")]
    public string Requirements { get; set; }

    [Required]
    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; }

    [Column("budget")]
    [Precision(18, 2)]
    public decimal? Budget { get; set; }

    [Column("start_date")]
    public DateTime StartDate { get; set; }

    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by_id")]
    public Guid CreatedById { get; set; }

    [ForeignKey("CreatedById")]
    public virtual User CreatedByNavigation { get; set; }

    // Propiedad de navegación adicional para facilitar el acceso
    public virtual User CreatedBy => CreatedByNavigation;

    [InverseProperty("Tender")]
    public virtual ICollection<TenderDocument> Documents { get; set; }

    [InverseProperty("Tender")]
    public virtual ICollection<Proposal> Proposals { get; set; }

    [InverseProperty("Tender")]
    public virtual ICollection<TenderResult> Results { get; set; }

    public Tender()
    {
        Documents = new HashSet<TenderDocument>();
        Proposals = new HashSet<Proposal>();
        Results = new HashSet<TenderResult>();
    }
}
