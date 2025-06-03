using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Licitaciones.Data;
using Licitaciones.Models;
using Licitaciones.Services;

namespace Licitaciones.Controllers
{
    [Authorize(Roles = "admin")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;

        public UsersController(ApplicationDbContext context, IPasswordHasher passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Name)
                .ToListAsync();
            return View(users);
        }

        // GET: Users/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .Include(u => u.Tenders)
                .Include(u => u.Proposals)
                .Include(u => u.Evaluations)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Email,Role")] User user, string password)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    if (string.IsNullOrEmpty(password))
                    {
                        TempData["Error"] = "La contraseña es requerida.";
                        return RedirectToAction(nameof(Index));
                    }

                    if (await _context.Users.AnyAsync(u => u.Email == user.Email))
                    {
                        TempData["Error"] = "Este correo electrónico ya está registrado.";
                        return RedirectToAction(nameof(Index));
                    }

                    user.Id = Guid.NewGuid();
                    user.PasswordHash = _passwordHasher.HashPassword(password);
                    user.CreatedAt = DateTime.UtcNow;

                    _context.Add(user);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Usuario creado exitosamente.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al crear el usuario: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name,Email,Role")] User user, string password)
        {
            try
            {
                if (id != user.Id)
                {
                    TempData["Error"] = "ID de usuario no válido.";
                    return RedirectToAction(nameof(Index));
                }

                if (ModelState.IsValid)
                {
                    var existingUser = await _context.Users.FindAsync(id);
                    if (existingUser == null)
                    {
                        TempData["Error"] = "Usuario no encontrado.";
                        return RedirectToAction(nameof(Index));
                    }

                    if (await _context.Users.AnyAsync(u => u.Email == user.Email && u.Id != id))
                    {
                        TempData["Error"] = "Este correo electrónico ya está registrado.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Verificar si es el último administrador y está cambiando su rol
                    if (existingUser.Role == "admin" && user.Role != "admin")
                    {
                        var adminCount = await _context.Users.CountAsync(u => u.Role == "admin");
                        if (adminCount <= 1)
                        {
                            TempData["Error"] = "No se puede cambiar el rol del último administrador.";
                            return RedirectToAction(nameof(Index));
                        }
                    }

                    existingUser.Name = user.Name;
                    existingUser.Email = user.Email;
                    existingUser.Role = user.Role;

                    if (!string.IsNullOrEmpty(password))
                    {
                        existingUser.PasswordHash = _passwordHasher.HashPassword(password);
                    }

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Usuario actualizado exitosamente.";
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(user.Id))
                {
                    TempData["Error"] = "Usuario no encontrado.";
                }
                else
                {
                    TempData["Error"] = "Error al actualizar el usuario. Intente nuevamente.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar el usuario: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    TempData["Error"] = "Usuario no encontrado.";
                    return RedirectToAction(nameof(Index));
                }

                // Verificar si es el último administrador
                if (user.Role == "admin")
                {
                    var adminCount = await _context.Users.CountAsync(u => u.Role == "admin");
                    if (adminCount <= 1)
                    {
                        TempData["Error"] = "No se puede eliminar el último administrador del sistema.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Usuario eliminado exitosamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar el usuario: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(Guid id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
} 