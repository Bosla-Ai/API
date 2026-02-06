using Domain.Responses;

namespace Service.Abstraction;

public interface IAuthTicketStore
{
    Task<string> StoreTicketAsync(LoginServerResponse response);
    Task<LoginServerResponse?> RetrieveTicketAsync(string ticketId);
}
