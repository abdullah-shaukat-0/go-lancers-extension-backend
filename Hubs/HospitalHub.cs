using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace SHMS.Backend.Hubs
{
    public class HospitalHub : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        public async Task BroadcastDoctorStatus(int doctorId, bool isAvailable)
        {
            await Clients.All.SendAsync("DoctorStatusChanged", doctorId, isAvailable);
        }

        public async Task BroadcastBedStatus(int bedId, bool isOccupied, string patientName)
        {
            await Clients.All.SendAsync("BedStatusChanged", bedId, isOccupied, patientName);
        }

        public async Task BroadcastInventoryAlert(string itemName, int currentStock)
        {
            await Clients.All.SendAsync("InventoryAlert", itemName, currentStock);
        }
    }
}
