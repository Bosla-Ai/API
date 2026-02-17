using Domain.Contracts;
using Domain.Entities;
using Shared;

namespace Domain.ModelsSpecifications.Administration.AdminsSpecifications;

public class GetAdminsByRoleSpecification : Specifications<ApplicationUser>

{
    public GetAdminsByRoleSpecification(string role)
            : base(u => u.UserRoles.Any(ur =>
                new[]
                {
                    StaticData.AdminRoleName,
                    StaticData.SuperAdminRoleName
                }.Contains(ur.Role.Name)
                &&
                (string.IsNullOrEmpty(role) || ur.Role.Name == role)
            ))

    {
        AddInclude(c => c.UserRoles);
        AddInclude("UserRoles.Role");
    }
}
