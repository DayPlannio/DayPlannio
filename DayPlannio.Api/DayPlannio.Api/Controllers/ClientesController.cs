using DayPlannio.Api.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace DayPlannio.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClienteController : ControllerBase
    {
        private readonly ContextMongodb _context = new ContextMongodb();

        [HttpGet("{usuarioId}")]
        public async Task<IActionResult> GetAll(Guid usuarioId)
        {
            var clientes = await _context.Cliente
                .Find(c => c.UsuarioId == usuarioId)
                .ToListAsync();

            return Ok(clientes);
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] Cliente cliente)
        {
            cliente.Id = Guid.NewGuid();
            cliente.CreatedAt = DateTime.UtcNow;

            await _context.Cliente.InsertOneAsync(cliente);
            return Ok(new { message = "Cliente cadastrado com sucesso.", id = cliente.Id });
        }

        [HttpPut("edit/{id}")]
        public async Task<IActionResult> Edit(Guid id, [FromBody] Cliente cliente)
        {
            var existing = await _context.Cliente.Find(c => c.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Cliente não encontrado." });

            existing.Nome = cliente.Nome;
            existing.Telefone = cliente.Telefone;
            existing.Endereco = cliente.Endereco;
            existing.Observacoes = cliente.Observacoes;

            await _context.Cliente.ReplaceOneAsync(c => c.Id == id, existing);
            return Ok(new { message = "Cliente atualizado com sucesso." });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existing = await _context.Cliente.Find(c => c.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Cliente não encontrado." });

            await _context.Cliente.DeleteOneAsync(c => c.Id == id);
            return Ok(new { message = "Cliente deletado com sucesso." });
        }
    }
}