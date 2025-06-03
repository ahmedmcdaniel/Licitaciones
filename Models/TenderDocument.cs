using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Licitaciones.Models;

[Table("tender_documents")]
public class TenderDocument
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("tender_id")]
    public Guid TenderId { get; set; }

    [Required]
    [Column("file_name")]
    [StringLength(255)]
    public string FileName { get; set; }

    [Required]
    [Column("file_path")]
    public string FilePath { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; }

    [Column("document_type")]
    [StringLength(50)]
    public string DocumentType { get; set; }

    [ForeignKey("TenderId")]
    public virtual Tender Tender { get; set; }
}
