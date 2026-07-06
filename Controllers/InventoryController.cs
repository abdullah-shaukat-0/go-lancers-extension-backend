using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SHMS.Backend.Data;
using SHMS.Backend.Hubs;
using SHMS.Backend.Models;
using System.Threading.Tasks;

namespace SHMS.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InventoryController : ControllerBase
    {
        private readonly SHMSDbContext _context;
        private readonly IHubContext<HospitalHub> _hubContext;

        public InventoryController(SHMSDbContext context, IHubContext<HospitalHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllInventory()
        {
            var inventory = await _context.InventoryItems.ToListAsync();
            return Ok(inventory);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetItemById(int id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null) return NotFound(new { Message = "Item not found" });
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> AddItem([FromBody] InventoryItem item)
        {
            _context.InventoryItems.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetItemById), new { id = item.Id }, item);
        }

        [HttpPut("{id}/stock")]
        public async Task<IActionResult> UpdateStock(int id, [FromBody] StockUpdateModel model)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null) return NotFound(new { Message = "Item not found" });

            item.Quantity = model.Quantity;
            await _context.SaveChangesAsync();

            // Broadcast alert if quantity goes below threshold
            if (item.Quantity <= item.ThresholdValue)
            {
                await _hubContext.Clients.All.SendAsync("InventoryAlert", item.Name, item.Quantity);
            }

            return Ok(item);
        }
    }

    public class StockUpdateModel
    {
        public int Quantity { get; set; }
    }
}
