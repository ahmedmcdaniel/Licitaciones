using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Licitaciones.Models;

[Table("evaluations")]
public partial class Evaluation
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("proposal_id")]
    public Guid ProposalId { get; set; }

    [Required]
    [Column("evaluator_id")]
    public Guid EvaluatorId { get; set; }

    [Required]
    [Column("technical_score")]
    [Range(0, 100)]
    public decimal TechnicalScore { get; set; }

    [Required]
    [Column("financial_score")]
    [Range(0, 100)]
    public decimal FinancialScore { get; set; }

    [Required]
    [Column("comments")]
    public string Comments { get; set; }

    [Column("evaluated_at")]
    public DateTime EvaluatedAt { get; set; }

    [ForeignKey("ProposalId")]
    [InverseProperty("Evaluations")]
    public virtual Proposal Proposal { get; set; }

    [ForeignKey("EvaluatorId")]
    [InverseProperty("Evaluations")]
    public virtual User Evaluator { get; set; }

    [NotMapped]
    public decimal TotalScore => (TechnicalScore * 0.7m) + (FinancialScore * 0.3m);
}
