using System;
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
    [Authorize(Roles = "admin,evaluador")]
    public class EvaluationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EvaluationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Evaluations/Create/5 (5 es el ID de la propuesta)
        public async Task<IActionResult> Create(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var proposal = await _context.Proposals
                .Include(p => p.Tender)
                .Include(p => p.Provider)
                .Include(p => p.Documents)
                .Include(p => p.Evaluations)
                    .ThenInclude(e => e.Evaluator)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposal == null)
            {
                return NotFound();
            }

            // Verificar si la licitación está abierta para evaluación
            if (proposal.Tender.Status != "evaluacion")
            {
                TempData["Error"] = "La licitación no está en fase de evaluación.";
                return RedirectToAction("Details", "Proposals", new { id = proposal.Id });
            }

            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            if (proposal.Evaluations.Any(e => e.EvaluatorId == userId))
            {
                TempData["Error"] = "Ya has evaluado esta propuesta.";
                return RedirectToAction("Details", "Proposals", new { id = proposal.Id });
            }

            // Asegurarnos de que la propuesta se pase a la vista
            ViewData["Proposal"] = proposal;  // Agregar también en ViewData por si acaso
            ViewBag.Proposal = proposal;      // Mantener en ViewBag

            return View(new Evaluation());  // Crear una nueva instancia de Evaluation para el formulario
        }

        // POST: Evaluations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm]Guid proposalId, [FromForm]decimal technicalScore, [FromForm]decimal financialScore, [FromForm]string comments)
        {
            try
            {
                var proposal = await _context.Proposals
                    .Include(p => p.Tender)
                    .Include(p => p.Provider)
                    .Include(p => p.Documents)
                    .Include(p => p.Evaluations)
                        .ThenInclude(e => e.Evaluator)
                    .FirstOrDefaultAsync(p => p.Id == proposalId);

                if (proposal == null)
                {
                    return Json(new { success = false, message = "Propuesta no encontrada" });
                }

                // Verificar si la licitación está abierta para evaluación
                if (proposal.Tender.Status != "evaluacion")
                {
                    return Json(new { success = false, message = "La licitación no está en fase de evaluación" });
                }

                var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                if (proposal.Evaluations.Any(e => e.EvaluatorId == userId))
                {
                    return Json(new { success = false, message = "Ya has evaluado esta propuesta" });
                }

                // Validar puntuaciones
                if (technicalScore < 0 || technicalScore > 100)
                {
                    return Json(new { success = false, message = "La puntuación técnica debe estar entre 0 y 100" });
                }

                if (financialScore < 0 || financialScore > 100)
                {
                    return Json(new { success = false, message = "La puntuación financiera debe estar entre 0 y 100" });
                }

                using (var transaction = _context.Database.BeginTransaction())
                {
                    try
                    {
                        // 1. Crear y guardar la evaluación
                        var evaluation = new Evaluation
                        {
                            Id = Guid.NewGuid(),
                            ProposalId = proposalId,
                            EvaluatorId = userId,
                            TechnicalScore = technicalScore,
                            FinancialScore = financialScore,
                            Comments = comments,
                            EvaluatedAt = DateTime.UtcNow
                        };
                        
                        _context.Add(evaluation);
                        await _context.SaveChangesAsync();

                        // 2. Actualizar estado de la propuesta
                        proposal.Status = "evaluada";
                        _context.Entry(proposal).State = EntityState.Modified;
                        await _context.SaveChangesAsync();

                        // 3. Verificar si todas las propuestas están evaluadas
                        var allProposals = await _context.Proposals
                            .Where(p => p.TenderId == proposal.TenderId)
                            .ToListAsync();

                        var allEvaluated = allProposals.All(p => p.Status == "evaluada" || p.Status == "rechazada");

                        // 4. Si todas están evaluadas, actualizar la licitación
                        if (allEvaluated)
                        {
                            var tender = await _context.Tenders.FindAsync(proposal.TenderId);
                            if (tender != null)
                            {
                                tender.Status = "evaluada";
                                _context.Entry(tender).State = EntityState.Modified;
                                await _context.SaveChangesAsync();
                            }
                        }

                        await transaction.CommitAsync();

                        return Json(new { 
                            success = true, 
                            message = "La evaluación se ha guardado correctamente",
                            redirectUrl = Url.Action("Details", "Proposals", new { id = proposalId })
                        });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = "Error al guardar la evaluación: " + ex.Message });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al guardar la evaluación: " + ex.Message });
            }
        }

        // GET: Evaluations/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var evaluation = await _context.Evaluations
                .Include(e => e.Proposal)
                    .ThenInclude(p => p.Tender)
                .Include(e => e.Proposal)
                    .ThenInclude(p => p.Provider)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evaluation == null)
            {
                return NotFound();
            }

            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            if (!User.IsInRole("admin") && evaluation.EvaluatorId != userId)
            {
                return Forbid();
            }

            if (evaluation.Proposal.Tender.Status != "evaluacion")
            {
                TempData["Error"] = "La licitación ya no está en fase de evaluación.";
                return RedirectToAction("Details", "Proposals", new { id = evaluation.ProposalId });
            }

            return View(evaluation);
        }

        // POST: Evaluations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,TechnicalScore,FinancialScore,Comments")] Evaluation evaluation)
        {
            var existingEvaluation = await _context.Evaluations
                .Include(e => e.Proposal)
                    .ThenInclude(p => p.Tender)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (existingEvaluation == null)
            {
                return NotFound();
            }

            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            if (!User.IsInRole("admin") && existingEvaluation.EvaluatorId != userId)
            {
                return Forbid();
            }

            if (existingEvaluation.Proposal.Tender.Status != "evaluacion")
            {
                TempData["Error"] = "La licitación ya no está en fase de evaluación.";
                return RedirectToAction("Details", "Proposals", new { id = existingEvaluation.ProposalId });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Validar puntuaciones
                    if (evaluation.TechnicalScore < 0 || evaluation.TechnicalScore > 100)
                    {
                        ModelState.AddModelError("TechnicalScore", "La puntuación técnica debe estar entre 0 y 100.");
                        return View(evaluation);
                    }

                    if (evaluation.FinancialScore < 0 || evaluation.FinancialScore > 100)
                    {
                        ModelState.AddModelError("FinancialScore", "La puntuación financiera debe estar entre 0 y 100.");
                        return View(evaluation);
                    }

                    existingEvaluation.TechnicalScore = evaluation.TechnicalScore;
                    existingEvaluation.FinancialScore = evaluation.FinancialScore;
                    existingEvaluation.Comments = evaluation.Comments;

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Evaluación actualizada correctamente.";
                    return RedirectToAction("Details", "Proposals", new { id = existingEvaluation.ProposalId });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EvaluationExists(evaluation.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return View(evaluation);
        }

        // POST: Evaluations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var evaluation = await _context.Evaluations
                .Include(e => e.Proposal)
                    .ThenInclude(p => p.Tender)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evaluation == null)
            {
                return NotFound();
            }

            if (evaluation.Proposal.Tender.Status != "evaluacion")
            {
                TempData["Error"] = "No se puede eliminar la evaluación porque la licitación ya no está en fase de evaluación.";
                return RedirectToAction("Details", "Proposals", new { id = evaluation.ProposalId });
            }

            _context.Evaluations.Remove(evaluation);
            
            // Actualizar estado de la propuesta si era la única evaluación
            if (!await _context.Evaluations.AnyAsync(e => e.ProposalId == evaluation.ProposalId && e.Id != id))
            {
                evaluation.Proposal.Status = "enviada";
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Evaluación eliminada correctamente.";
            return RedirectToAction("Details", "Proposals", new { id = evaluation.ProposalId });
        }

        private bool EvaluationExists(Guid id)
        {
            return _context.Evaluations.Any(e => e.Id == id);
        }
    }
} 