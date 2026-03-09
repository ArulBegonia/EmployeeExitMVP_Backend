using Microsoft.EntityFrameworkCore;
using Relevantz.ExitManagement.Common.Entities;
using Relevantz.ExitManagement.Data.DBContexts;

namespace Relevantz.ExitManagement.Data.DBContexts;

public static class DbInitializer
{
    public static async Task SeedAsync(ExitManagementDbContext context)
    {
        // Apply pending migrations automatically
        await context.Database.MigrateAsync();

        // Roles
        var roleNames = new[] { "ADMIN", "HR", "MANAGER", "EMPLOYEE", "IT" };
        foreach (var name in roleNames)
        {
            if (!context.Roles.Any(r => r.Name == name))
                context.Roles.Add(new Role { Name = name });
        }
        await context.SaveChangesAsync();

        int RoleId(string name) => context.Roles.First(r => r.Name == name).Id;

        async Task EnsureEmployee(
            string code, string first, string last,
            string email, string roleName,
            string? department   = null,
            bool isActive        = true,
            int? l1ManagerId     = null,
            int? l2ManagerId     = null)
        {
            if (!context.Employees.Any(e => e.Email == email))
            {
                context.Employees.Add(new Employee
                {
                    EmployeeCode = code,
                    FirstName    = first,
                    LastName     = last,
                    Email        = email,
                    Password     = "Admin@123456",  
                    RoleId       = RoleId(roleName),
                    Department   = department,
                    IsActive     = isActive,
                    L1ManagerId  = l1ManagerId,
                    L2ManagerId  = l2ManagerId,
                });
                await context.SaveChangesAsync();
            }
        }

        // ── Seed in dependency order (managers first) ──
        await EnsureEmployee("EMP001", "John",  "Doe",      "l1manager@test.com", "MANAGER",  department: "Engineering");
        await EnsureEmployee("EMP003", "David", "Smith",    "l2manager@test.com", "MANAGER",  department: "Engineering");
        await EnsureEmployee("EMP004", "Helen", "HR",       "hr@test.com",        "HR",       department: "Human Resources");
        await EnsureEmployee("EMP005", "Adam",  "Admin",    "admin@test.com",     "ADMIN",    department: "Operations");
        await EnsureEmployee("EMP006", "Ivy",   "IT",       "it@test.com",        "IT",       department: "IT");

        // ── Employees with manager references ──
        var l1 = context.Employees.FirstOrDefault(e => e.Email == "l1manager@test.com");
        var l2 = context.Employees.FirstOrDefault(e => e.Email == "l2manager@test.com");

        await EnsureEmployee("EMP002", "Alice",  "Johnson", "employee@test.com",  "EMPLOYEE",
            department: "Engineering",
            l1ManagerId: l1?.Id, l2ManagerId: l2?.Id);

        await EnsureEmployee("EMP013", "Kavya",  "Kumar",   "employee1@test.com", "EMPLOYEE",
            department: "Engineering",
            l1ManagerId: l1?.Id, l2ManagerId: l2?.Id);

        await EnsureEmployee("EMP014", "Begonia","Rose",    "employee2@test.com", "EMPLOYEE",
            department: "Engineering",
            l1ManagerId: l1?.Id, l2ManagerId: l2?.Id);
    }
}
