using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Licitaciones.Models;

[Table("proposals")]
public partial class Proposal
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("tender_id")]
    public Guid TenderId { get; set; }

    [Required]
    [Column("provider_id")]
    public Guid ProviderId { get; set; }

    [Required]
    [Column("description")]
    public string Description { get; set; }

    [Column("amount")]
    [Precision(18, 2)]
    public decimal Amount { get; set; }

    [Column("status")]
    [StringLength(50)]
    [Required]
    public string Status { get; set; }

    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    [ForeignKey("TenderId")]
    public virtual Tender Tender { get; set; }

    [ForeignKey("ProviderId")]
    public virtual User Provider { get; set; }

    [InverseProperty("Proposal")]
    public virtual ICollection<ProposalDocument> Documents { get; set; }

    [InverseProperty("Proposal")]
    public virtual ICollection<Evaluation> Evaluations { get; set; }

    [InverseProperty("WinningProposal")]
    public virtual ICollection<TenderResult> WonTenders { get; set; }

    public Proposal()
    {
        Documents = new HashSet<ProposalDocument>();
        Evaluations = new HashSet<Evaluation>();
        WonTenders = new HashSet<TenderResult>();
    }
}
