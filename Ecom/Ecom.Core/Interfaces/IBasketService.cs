using Ecom.Core.DTO.Basket;

namespace Ecom.Core.Interfaces;

public record BasketOwnerContext(string? UserId, string? GuestCredential, bool IsAuthenticated);
public record BasketOperationResult(bool Success, int StatusCode, string? Message, CustomerBasketDto? Basket = null, string? NewGuestCredential = null, bool ClearGuestCookie = false);

public interface IBasketService
{
    Task<BasketOperationResult> GetBasketAsync(BasketOwnerContext owner, CancellationToken ct = default);
    Task<BasketOperationResult> AddItemAsync(BasketOwnerContext owner, int productId, int quantity, CancellationToken ct = default);
    Task<BasketOperationResult> UpdateItemQuantityAsync(BasketOwnerContext owner, int productId, int quantity, CancellationToken ct = default);
    Task<BasketOperationResult> RemoveItemAsync(BasketOwnerContext owner, int productId, CancellationToken ct = default);
    Task<BasketOperationResult> ClearBasketAsync(BasketOwnerContext owner, CancellationToken ct = default);
    Task<BasketOperationResult> ReplaceItemsAsync(BasketOwnerContext owner, IEnumerable<(int ProductId, int Quantity)> items, CancellationToken ct = default);
    Task<BasketOperationResult> MergeGuestBasketAsync(string userId, string? guestCredential, CancellationToken ct = default);
}
