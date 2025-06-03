using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Licitaciones.Models;

[Table("tender_results")]
public partial class TenderResult
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tender_id")]
    public Guid? TenderId { get; set; }

    [Column("winning_proposal_id")]
    public Guid? WinningProposalId { get; set; }

    [Column("published_at", TypeName = "timestamp without time zone")]
    public DateTime? PublishedAt { get; set; }

    [ForeignKey("TenderId")]
    [InverseProperty("Results")]
    public virtual Tender? Tender { get; set; }

    [ForeignKey("WinningProposalId")]
    public virtual Proposal? WinningProposal { get; set; }
}
