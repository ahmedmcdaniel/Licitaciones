using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Licitaciones.Data;
using Licitaciones.Models;

namespace Licitaciones.Controllers
{
    [Authorize]
    public class ProposalsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProposalsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Proposals
        public async Task<IActionResult> Index()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var proposals = new List<Proposal>();

            if (User.IsInRole("admin") || User.IsInRole("evaluador"))
            {
                proposals = await _context.Proposals
                    .Include(p => p.Tender)
                    .Include(p => p.Provider)
                    .Include(p => p.Documents)
                    .Include(p => p.Evaluations)
                    .OrderByDescending(p => p.SubmittedAt)
                    .ToListAsync();
            }
            else
            {
                proposals = await _context.Proposals
                    .Include(p => p.Tender)
                    .Include(p => p.Provider)
                    .Include(p => p.Documents)
                    .Include(p => p.Evaluations)
                    .Where(p => p.ProviderId == userId)
                    .OrderByDescending(p => p.SubmittedAt)
                    .ToListAsync();
            }

            return View(proposals);
        }

        // GET: Proposals/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Asegurarnos de cargar los datos más recientes
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var proposal = await _context.Proposals
                .Include(p => p.Tender)
                .Include(p => p.Provider)
                .Include(p => p.Documents)
                .Include(p => p.Evaluations)
                    .ThenInclude(e => e.Evaluator)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (proposal == null)
            {
                return NotFound();
            }

            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            if (!User.IsInRole("admin") && !User.IsInRole("evaluador") && proposal.ProviderId != userId)
            {
                return Forbid();
            }

            return View(proposal);
        }

        // GET: Proposals/Create/5 (5 es el ID de la licitación)
        [Authorize(Roles = "proveedor")]
        public async Task<IActionResult> Create(Guid? tenderId)
        {
            if (tenderId == null)
            {
                return NotFound();
            }

            var tender = await _context.Tenders
                .FirstOrDefaultAsync(t => t.Id == tenderId);

            if (tender == null)
            {
                return NotFound();
            }

            if (tender.Status != "abierta")
            {
                return BadRequest("La licitación no está abierta para propuestas.");
            }

            ViewBag.Tender = tender;
            return View();
        }

        // POST: Proposals/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "proveedor")]
        public async Task<IActionResult> Create(Guid tenderId, string description, decimal amount, IFormFile technicalDoc, IFormFile financialDoc, List<IFormFile> additionalDocs)
        {
            try
            {
                // Validar que la licitación existe y está abierta
                var tender = await _context.Tenders.FindAsync(tenderId);
                if (tender == null)
                {
                    TempData["Error"] = "La licitación no existe.";
                    return RedirectToAction("Index", "Tenders");
                }

                if (tender.Status != "abierta")
                {
                    TempData["Error"] = "La licitación no está abierta para propuestas.";
                    return RedirectToAction("Details", "Tenders", new { id = tenderId });
                }

                if (DateTime.UtcNow > tender.EndDate)
                {
                    TempData["Error"] = "La fecha límite para enviar propuestas ha expirado.";
                    return RedirectToAction("Details", "Tenders", new { id = tenderId });
                }

                var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

                // Verificar si ya existe una propuesta para esta licitación
                var existingProposal = await _context.Proposals
                    .FirstOrDefaultAsync(p => p.TenderId == tenderId && p.ProviderId == userId);

                if (existingProposal != null)
                {
                    TempData["Error"] = "Ya has enviado una propuesta para esta licitación.";
                    return RedirectToAction("Details", "Tenders", new { id = tenderId });
                }

                // Validar el monto
                if (amount <= 0)
                {
                    TempData["Error"] = "El monto propuesto debe ser mayor que cero.";
                    return RedirectToAction("Create", new { tenderId });
                }

                // Verificar el presupuesto máximo
                if (tender.Budget.HasValue && amount > tender.Budget.Value)
                {
                    TempData["Error"] = $"El monto propuesto ({amount:C}) supera el presupuesto máximo establecido ({tender.Budget.Value:C}).";
                    return RedirectToAction("Create", new { tenderId });
                }

                // Validar documentos requeridos
                if (technicalDoc == null || technicalDoc.Length == 0)
                {
                    TempData["Error"] = "Debe adjuntar el documento técnico.";
                    return RedirectToAction("Create", new { tenderId });
                }

                if (financialDoc == null || financialDoc.Length == 0)
                {
                    TempData["Error"] = "Debe adjuntar el documento financiero.";
                    return RedirectToAction("Create", new { tenderId });
                }

                // Validar tipos de archivo
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
                
                if (!ValidateFileExtension(technicalDoc, allowedExtensions))
                {
                    TempData["Error"] = "El documento técnico debe estar en formato PDF, Word o Excel.";
                    return RedirectToAction("Create", new { tenderId });
                }

                if (!ValidateFileExtension(financialDoc, allowedExtensions))
                {
                    TempData["Error"] = "El documento financiero debe estar en formato PDF, Word o Excel.";
                    return RedirectToAction("Create", new { tenderId });
                }

                if (additionalDocs != null)
                {
                    foreach (var doc in additionalDocs)
                    {
                        if (!ValidateFileExtension(doc, allowedExtensions))
                        {
                            TempData["Error"] = $"El documento adicional {doc.FileName} debe estar en formato PDF, Word o Excel.";
                            return RedirectToAction("Create", new { tenderId });
                        }
                    }
                }

                var proposal = new Proposal
                {
                    Id = Guid.NewGuid(),
                    TenderId = tenderId,
                    ProviderId = userId,
                    Description = description,
                    Amount = amount,
                    Status = "enviada",
                    SubmittedAt = DateTime.UtcNow
                };

                _context.Add(proposal);

                // Crear el directorio para los documentos
                var proposalPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "proposals", proposal.Id.ToString());
                Directory.CreateDirectory(proposalPath);

                // Procesar documento técnico
                var technicalDocument = await SaveProposalDocument(proposal.Id, technicalDoc, "technical");
                _context.Add(technicalDocument);

                // Procesar documento financiero
                var financialDocument = await SaveProposalDocument(proposal.Id, financialDoc, "financial");
                _context.Add(financialDocument);

                // Procesar documentos adicionales
                if (additionalDocs != null && additionalDocs.Count > 0)
                {
                    foreach (var doc in additionalDocs.Where(d => d.Length > 0))
                    {
                        var additionalDocument = await SaveProposalDocument(proposal.Id, doc, "additional");
                        _context.Add(additionalDocument);
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Propuesta enviada correctamente. Los evaluadores serán notificados.";
                return RedirectToAction("Details", "Tenders", new { id = tenderId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al enviar la propuesta: " + ex.Message;
                return RedirectToAction("Create", new { tenderId });
            }
        }

        private bool ValidateFileExtension(IFormFile file, string[] allowedExtensions)
        {
            if (file == null || file.Length == 0) return false;
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return allowedExtensions.Contains(extension);
        }

        private async Task<ProposalDocument> SaveProposalDocument(Guid proposalId, IFormFile file, string type)
        {
            var fileName = Path.GetFileName(file.FileName);
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "proposals", proposalId.ToString(), fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return new ProposalDocument
            {
                Id = Guid.NewGuid(),
                ProposalId = proposalId,
                FileName = fileName,
                FilePath = filePath,
                DocumentType = type,
                UploadedAt = DateTime.UtcNow
            };
        }

        // POST: Proposals/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,proveedor")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var proposal = await _context.Proposals
                .Include(p => p.Documents)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposal == null)
            {
                return NotFound();
            }

            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            if (!User.IsInRole("admin") && proposal.ProviderId != userId)
            {
                return Forbid();
            }

            // Eliminar archivos físicos
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "proposals", proposal.Id.ToString());
            if (Directory.Exists(uploadsFolder))
            {
                Directory.Delete(uploadsFolder, true);
            }

            _context.Proposals.Remove(proposal);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
} 