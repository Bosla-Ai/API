namespace Domain.Contracts;

public interface IDbInitializer
{
    public Task InitializeDbAsync();
    public Task InitializeRolesAsync();
}