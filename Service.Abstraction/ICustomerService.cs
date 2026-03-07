using Domain.Entities;
using Domain.Responses;
using Shared;
using Shared.DTOs;
using Shared.DTOs.CustomerDTOs;
using Shared.Enums;

namespace Service.Abstraction;

public interface ICustomerService
{
    Task<APIResponse<string>> ProcessUserQueryAsync(string userId, string query, string? sessionId = null);
    IAsyncEnumerable<string> ProcessUserQueryStreamAsync(string userId, string query, string? sessionId = null, CancellationToken cancellationToken = default, ChatMode chatMode = ChatMode.Normal);

    Task<APIResponse<AiIntentDetectionResponse>> ProcessUserQueryWithIntentDetectionAsync(string userId, string query, string? sessionId = null);

    string CreateAiRequest(string userId, AiQueryRequest request);

    (string UserId, AiQueryRequest Request) GetAiRequest(string requestId);

    Task<APIResponse> GetCustomerProfileAsync(string customerId);
    Task<IEnumerable<Customer>> GetAllAsync();
    Task<Customer> GetByIdAsync(string id);
    Task CreateAsync(Customer customer);
    Task UpdateAsync(Customer customer);
    Task DeleteAsync(Customer customer);
    Task<CustomerDTO> GetALlCustomerDetailsAsync(string id);
}
