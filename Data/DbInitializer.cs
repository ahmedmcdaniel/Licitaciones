using Microsoft.EntityFrameworkCore;
using Licitaciones.Models;
using Licitaciones.Services;

namespace Licitaciones.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context, IPasswordHasher passwordHasher)
        {
            context.Database.EnsureCreated();

            // Si ya existen usuarios, no hacer nada
            if (context.Users.Any())
            {
                return;
            }

            var users = new User[]
            {
                new User
                {
                    Id = Guid.NewGuid(),
                    Name = "Administrador",
                    Email = "admin@licitaciones.com",
                    PasswordHash = passwordHasher.HashPassword("Admin123!"),
                    Role = "admin",
                    CreatedAt = DateTime.Now
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Name = "Evaluador",
                    Email = "evaluador@licitaciones.com",
                    PasswordHash = passwordHasher.HashPassword("Evaluador123!"),
                    Role = "evaluador",
                    CreatedAt = DateTime.Now
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Name = "Proveedor",
                    Email = "proveedor@licitaciones.com",
                    PasswordHash = passwordHasher.HashPassword("Proveedor123!"),
                    Role = "proveedor",
                    CreatedAt = DateTime.Now
                }
            };

            context.Users.AddRange(users);
            context.SaveChanges();
        }
    }
} 