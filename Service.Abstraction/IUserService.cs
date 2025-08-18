using Shared;

namespace Service.Abstraction;

public interface IUserService
{
    Task<AiQueryResponse> ProcessUserQueryAsync(string query);
}