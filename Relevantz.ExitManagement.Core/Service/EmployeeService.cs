using Relevantz.ExitManagement.Common.DTOs;
using Relevantz.ExitManagement.Core.IService;
using Relevantz.ExitManagement.Data.IRepository;

namespace Relevantz.ExitManagement.Core.Service;

public class EmployeeService : IEmployeeService
{
    private readonly IExitRequestRepository _repository;

    public EmployeeService(IExitRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<EmployeeLookupDto?> LookupByCodeAsync(string employeeCode)
    {
        var employee = await _repository.GetEmployeeByCodeAsync(employeeCode);
        if (employee == null || !employee.IsActive)
            return null;

        return new EmployeeLookupDto
        {
            EmployeeCode = employee.EmployeeCode,
            Name         = $"{employee.FirstName} {employee.LastName}",
            Department   = employee.Department,
            IsActive     = employee.IsActive,
        };
    }
}
