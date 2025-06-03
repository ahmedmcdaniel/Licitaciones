using Microsoft.EntityFrameworkCore;
using Licitaciones.Models;

namespace Licitaciones.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Tender> Tenders { get; set; }
        public DbSet<TenderDocument> TenderDocuments { get; set; }
        public DbSet<Proposal> Proposals { get; set; }
        public DbSet<ProposalDocument> ProposalDocuments { get; set; }
        public DbSet<Evaluation> Evaluations { get; set; }
        public DbSet<TenderResult> TenderResults { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql("Host=dpg-d0vic615pdvs738ie3fg-a;Database=licitaciones_sql;Username=admin;Password=lCdDlOQn5EECDYXb0Z8U0XGJfl04GqfA");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.Role).IsRequired();
            });

            // Configuración de Tender
            modelBuilder.Entity<Tender>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired();
                entity.Property(e => e.Description).IsRequired();
                entity.Property(e => e.Status).IsRequired();

                entity.HasOne(t => t.CreatedByNavigation)
                      .WithMany(u => u.Tenders)
                      .HasForeignKey(t => t.CreatedById)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuración de TenderResult
            modelBuilder.Entity<TenderResult>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(tr => tr.Tender)
                      .WithMany(t => t.Results)
                      .HasForeignKey(tr => tr.TenderId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(tr => tr.WinningProposal)
                      .WithMany(p => p.WonTenders)
                      .HasForeignKey(tr => tr.WinningProposalId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuración de Proposal
            modelBuilder.Entity<Proposal>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(p => p.Tender)
                      .WithMany(t => t.Proposals)
                      .HasForeignKey(p => p.TenderId)
                      .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(p => p.Provider)
                      .WithMany(u => u.Proposals)
                      .HasForeignKey(p => p.ProviderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuración de Evaluation
            modelBuilder.Entity<Evaluation>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.Proposal)
                      .WithMany(p => p.Evaluations)
                      .HasForeignKey(e => e.ProposalId)
                      .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.Evaluator)
                      .WithMany(u => u.Evaluations)
                      .HasForeignKey(e => e.EvaluatorId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
} 