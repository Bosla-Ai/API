using Shared.DTOs.ApplicationUserDTOs;
using Shared.Enums;

namespace Shared.DTOs.CustomerDTOs;

public sealed class CustomerDTO
{
    public ApplicationUserDTO ApplicationUser { get; set; }
    public LevelType? UserLevel { get; set; }
    public string? PreferredFramework { get; set; }
    public Domains? SelectedDomain { get; set; }
    public bool WantsDeepDive { get; set; } = false;
    public int? AvailableHoursPerWeek { get; set; }
    public BudgetPreference? BudgetPreference { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
}