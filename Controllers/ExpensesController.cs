using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHMS.Backend.Data;
using SHMS.Backend.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SHMS.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ExpensesController : ControllerBase
    {
        private readonly SHMSDbContext _context;

        public ExpensesController(SHMSDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetExpenses(string category, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.Expenses.AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(e => e.Category == category);

            if (fromDate.HasValue)
                query = query.Where(e => e.ExpenseDate >= fromDate.Value.Date);

            if (toDate.HasValue)
            {
                var exclusiveEndDate = toDate.Value.Date.AddDays(1);
                query = query.Where(e => e.ExpenseDate < exclusiveEndDate);
            }

            var expenses = await query
                .OrderByDescending(e => e.ExpenseDate)
                .ThenByDescending(e => e.Id)
                .ToListAsync();

            return Ok(expenses);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetExpenseById(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null) return NotFound(new { Message = "Expense not found" });

            return Ok(expense);
        }

        [HttpPost]
        public async Task<IActionResult> CreateExpense([FromBody] ExpenseModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Title) || model.Amount <= 0)
                return BadRequest(new { Message = "Title and a positive Amount are required." });

            var expense = new Expense
            {
                Title = model.Title,
                Category = string.IsNullOrWhiteSpace(model.Category) ? "General" : model.Category,
                Amount = model.Amount,
                ExpenseDate = model.ExpenseDate == default(DateTime) ? DateTime.UtcNow.Date : model.ExpenseDate,
                Notes = model.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetExpenseById), new { id = expense.Id }, expense);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExpense(int id, [FromBody] ExpenseModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Title) || model.Amount <= 0)
                return BadRequest(new { Message = "Title and a positive Amount are required." });

            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null) return NotFound(new { Message = "Expense not found" });

            expense.Title = model.Title;
            expense.Category = string.IsNullOrWhiteSpace(model.Category) ? "General" : model.Category;
            expense.Amount = model.Amount;
            expense.ExpenseDate = model.ExpenseDate == default(DateTime) ? expense.ExpenseDate : model.ExpenseDate;
            expense.Notes = model.Notes;

            await _context.SaveChangesAsync();

            return Ok(expense);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null) return NotFound(new { Message = "Expense not found" });

            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Expense deleted" });
        }
    }

    public class ExpenseModel
    {
        public string Title { get; set; }
        public string Category { get; set; }
        public decimal Amount { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string Notes { get; set; }
    }
}
