// Hubs/AuctionHub.cs
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Group1_PRN222.Hubs
{
    public class AuctionHub : Hub
    {
        public async Task SendBid(int productId, string username, decimal amount)
        {
            await Clients.Group($"Product_{productId}").SendAsync("ReceiveBid", username, amount);
        }

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            if (httpContext.Request.Query.TryGetValue("productId", out var productId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Product_{productId}");
            }

            await base.OnConnectedAsync();
        }
    }
}
