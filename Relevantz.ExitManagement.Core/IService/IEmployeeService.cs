using Relevantz.ExitManagement.Common.DTOs;

namespace Relevantz.ExitManagement.Core.IService;

public interface IEmployeeService
{
    Task<EmployeeLookupDto?> LookupByCodeAsync(string employeeCode);
}
