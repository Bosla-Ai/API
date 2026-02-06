using Domain.Responses;
using Microsoft.Extensions.Caching.Memory;
using Service.Abstraction;

namespace Service.Implementations;

public class AuthTicketStore(IMemoryCache cache) : IAuthTicketStore
{
    public Task<string> StoreTicketAsync(LoginServerResponse response)
    {
        var ticket = Guid.NewGuid().ToString("N");
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(1))
            .SetSize(1); // To track estimated memory usage if needed

        cache.Set(ticket, response, cacheOptions);
        return Task.FromResult(ticket);
    }

    public Task<LoginServerResponse?> RetrieveTicketAsync(string ticketId)
    {
        cache.TryGetValue(ticketId, out LoginServerResponse? response);
        if (response != null)
        {
            // Ticket is single-use
            cache.Remove(ticketId);
        }
        return Task.FromResult(response);
    }
}
