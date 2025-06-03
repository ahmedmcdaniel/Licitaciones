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
    public class TendersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public TendersController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Tenders
        public async Task<IActionResult> Index()
        {
            var tenders = await _context.Tenders
                .Include(t => t.CreatedByNavigation)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return View(tenders);
        }

        // GET: Tenders/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                TempData["Error"] = "ID de licitación no proporcionado.";
                return RedirectToAction(nameof(Index));
            }

            var tender = await _context.Tenders
                .Include(t => t.CreatedByNavigation)
                .Include(t => t.Documents)
                .Include(t => t.Proposals)
                    .ThenInclude(p => p.Provider)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (tender == null)
            {
                TempData["Error"] = "Licitación no encontrada.";
                return RedirectToAction(nameof(Index));
            }

            return View(tender);
        }

        // GET: Tenders/Create
        [Authorize(Roles = "admin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Tenders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Create(Tender tender, List<IFormFile> documents)
        {
            try
            {
                // Remover los campos calculados de la validación del modelo
                ModelState.Remove("CreatedBy");
                ModelState.Remove("CreatedByNavigation");
                ModelState.Remove("Status");

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    TempData["Error"] = "Por favor, corrija los siguientes errores: " + string.Join(", ", errors);
                    return View(tender);
                }

                // Convertir fechas locales a UTC
                tender.StartDate = DateTime.SpecifyKind(tender.StartDate, DateTimeKind.Utc);
                tender.EndDate = DateTime.SpecifyKind(tender.EndDate, DateTimeKind.Utc);

                // Validaciones adicionales (usando las fechas UTC)
                if (tender.StartDate >= tender.EndDate)
                {
                    TempData["Error"] = "La fecha de cierre debe ser posterior a la fecha de inicio.";
                    return View(tender);
                }

                if (tender.StartDate.Date < DateTime.UtcNow.Date)
                {
                    TempData["Error"] = "La fecha de inicio no puede ser anterior a hoy.";
                    return View(tender);
                }

                if (tender.Budget.HasValue && tender.Budget.Value <= 0)
                {
                    TempData["Error"] = "El presupuesto debe ser mayor que cero.";
                    return View(tender);
                }

                // Configurar campos automáticos
                tender.Id = Guid.NewGuid();
                tender.CreatedAt = DateTime.UtcNow;
                tender.Status = "abierta";
                tender.CreatedById = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

                // Verificar que el usuario existe
                var userExists = await _context.Users.AnyAsync(u => u.Id == tender.CreatedById);
                if (!userExists)
                {
                    TempData["Error"] = "Error al obtener la información del usuario creador.";
                    return View(tender);
                }

                _context.Add(tender);

                // Procesar documentos
                if (documents != null && documents.Count > 0)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tenders", tender.Id.ToString());
                    Directory.CreateDirectory(uploadsFolder);

                    foreach (var document in documents.Where(d => d.Length > 0))
                    {
                        // Validar tipo de archivo
                        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
                        var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
                        
                        if (!allowedExtensions.Contains(extension))
                        {
                            TempData["Error"] = $"El archivo {document.FileName} no es de un tipo permitido. Solo se permiten archivos PDF, Word y Excel.";
                            return View(tender);
                        }

                        string fileName = Path.GetFileName(document.FileName);
                        string filePath = Path.Combine(uploadsFolder, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await document.CopyToAsync(stream);
                        }

                        var tenderDocument = new TenderDocument
                        {
                            Id = Guid.NewGuid(),
                            TenderId = tender.Id,
                            FileName = fileName,
                            FilePath = Path.Combine("uploads", "tenders", tender.Id.ToString(), fileName),
                            DocumentType = extension.TrimStart('.').ToUpper(),
                            UploadedAt = DateTime.UtcNow
                        };

                        _context.TenderDocuments.Add(tenderDocument);
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Licitación creada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                var innerException = ex.InnerException;
                while (innerException != null)
                {
                    errorMessage += " -> " + innerException.Message;
                    innerException = innerException.InnerException;
                }
                TempData["Error"] = "Error al crear la licitación: " + errorMessage;
                return View(tender);
            }
        }

        // GET: Tenders/Edit/5
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                TempData["Error"] = "ID de licitación no proporcionado.";
                return RedirectToAction(nameof(Index));
            }

            var tender = await _context.Tenders
                .Include(t => t.Documents)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tender == null)
            {
                TempData["Error"] = "Licitación no encontrada.";
                return RedirectToAction(nameof(Index));
            }

            return View(tender);
        }

        // POST: Tenders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Title,Description,Requirements,StartDate,EndDate,Status")] Tender tender, List<IFormFile> newDocuments)
        {
            if (id != tender.Id)
            {
                TempData["Error"] = "ID de licitación no coincide.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var existingTender = await _context.Tenders
                    .Include(t => t.Documents)
                    .Include(t => t.CreatedByNavigation)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (existingTender == null)
                {
                    TempData["Error"] = "Licitación no encontrada.";
                    return RedirectToAction(nameof(Index));
                }

                // Actualizar los campos modificables
                existingTender.Title = tender.Title;
                existingTender.Description = tender.Description;
                existingTender.Requirements = tender.Requirements;
                existingTender.StartDate = tender.StartDate;
                existingTender.EndDate = tender.EndDate;
                existingTender.Status = tender.Status;

                // Procesar nuevos documentos si hay
                if (newDocuments != null && newDocuments.Count > 0)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tenders", tender.Id.ToString());
                    Directory.CreateDirectory(uploadsFolder);

                    foreach (var document in newDocuments.Where(d => d.Length > 0))
                    {
                        // Validar tipo de archivo
                        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
                        var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
                        
                        if (!allowedExtensions.Contains(extension))
                        {
                            TempData["Error"] = $"El archivo {document.FileName} no es de un tipo permitido. Solo se permiten archivos PDF, Word y Excel.";
                            return View(tender);
                        }

                        string fileName = Path.GetFileName(document.FileName);
                        string filePath = Path.Combine(uploadsFolder, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await document.CopyToAsync(stream);
                        }

                        var tenderDocument = new TenderDocument
                        {
                            Id = Guid.NewGuid(),
                            TenderId = tender.Id,
                            FileName = fileName,
                            FilePath = Path.Combine("uploads", "tenders", tender.Id.ToString(), fileName),
                            DocumentType = extension.TrimStart('.').ToUpper(),
                            UploadedAt = DateTime.UtcNow
                        };

                        _context.TenderDocuments.Add(tenderDocument);
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Licitación actualizada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TenderExists(tender.Id))
                {
                    TempData["Error"] = "Licitación no encontrada.";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar la licitación: " + ex.Message;
                return View(tender);
            }
        }

        // POST: Tenders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            try
            {
                var tender = await _context.Tenders
                    .Include(t => t.Documents)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (tender == null)
                {
                    TempData["Error"] = "Licitación no encontrada.";
                    return RedirectToAction(nameof(Index));
                }

                // Eliminar archivos físicos
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tenders", tender.Id.ToString());
                if (Directory.Exists(uploadsFolder))
                {
                    Directory.Delete(uploadsFolder, true);
                }

                _context.Tenders.Remove(tender);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Licitación eliminada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar la licitación: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        private bool TenderExists(Guid id)
        {
            return _context.Tenders.Any(e => e.Id == id);
        }

        // POST: Tenders/DeleteDocument/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteDocument(Guid id)
        {
            try
            {
                var document = await _context.TenderDocuments.FindAsync(id);
                if (document == null)
                {
                    TempData["Error"] = "Documento no encontrado.";
                    return RedirectToAction(nameof(Index));
                }

                // Eliminar archivo físico
                string filePath = Path.Combine(_webHostEnvironment.WebRootPath, document.FilePath);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                _context.TenderDocuments.Remove(document);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Documento eliminado correctamente.";
                return RedirectToAction(nameof(Edit), new { id = document.TenderId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar el documento: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> SelectWinner(Guid tenderId, Guid proposalId)
        {
            try
            {
                var tender = await _context.Tenders
                    .Include(t => t.Proposals)
                    .FirstOrDefaultAsync(t => t.Id == tenderId);

                if (tender == null)
                {
                    TempData["Error"] = "Licitación no encontrada.";
                    return RedirectToAction(nameof(Index));
                }

                if (tender.Status != "evaluada")
                {
                    TempData["Error"] = "La licitación debe estar evaluada para seleccionar un ganador.";
                    return RedirectToAction(nameof(Details), new { id = tenderId });
                }

                var winningProposal = await _context.Proposals
                    .FirstOrDefaultAsync(p => p.Id == proposalId && p.TenderId == tenderId);

                if (winningProposal == null)
                {
                    TempData["Error"] = "Propuesta no encontrada.";
                    return RedirectToAction(nameof(Details), new { id = tenderId });
                }

                // Crear el resultado
                var result = new TenderResult
                {
                    Id = Guid.NewGuid(),
                    TenderId = tenderId,
                    WinningProposalId = proposalId,
                    PublishedAt = DateTime.UtcNow
                };

                _context.TenderResults.Add(result);

                // Actualizar estado de la licitación
                tender.Status = "cerrada";
                _context.Entry(tender).State = EntityState.Modified;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Ganador seleccionado y licitación cerrada correctamente.";
                return RedirectToAction(nameof(Details), new { id = tenderId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al seleccionar el ganador: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id = tenderId });
            }
        }
    }
} 