using DayPlannio.Api.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace DayPlannio.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TiposServicoController : ControllerBase
    {
        private readonly ContextMongodb _context = new ContextMongodb();

        [HttpGet("{usuarioId}")]
        public async Task<IActionResult> GetAll(Guid usuarioId)
        {
            var tipos = await _context.TipoServico
                .Find(t => t.UsuarioId == usuarioId)
                .ToListAsync();

            return Ok(tipos);
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] TipoServico tipoServico)
        {
            if (tipoServico == null)
                return BadRequest(new { message = "Dados inválidos." });

            if (string.IsNullOrWhiteSpace(tipoServico.Tipo))
                return BadRequest(new { message = "O nome é obrigatório." });

            if (tipoServico.TempoEstimado <= 0)
                return BadRequest(new { message = "O tempo estimado deve ser maior que zero." });

            tipoServico.Id = Guid.NewGuid();
            tipoServico.CreatedAt = DateTime.UtcNow;

            await _context.TipoServico.InsertOneAsync(tipoServico);

            return Ok(new { message = "Tipo de serviço cadastrado com sucesso.", id = tipoServico.Id });
        }

        [HttpPut("edit/{id}")]
        public async Task<IActionResult> Edit(Guid id, [FromBody] TipoServico tipoServico)
        {
            var existing = await _context.TipoServico.Find(t => t.Id == id).FirstOrDefaultAsync();
            if (existing == null)
                return NotFound(new { message = "Tipo de serviço não encontrado." });

            if (string.IsNullOrWhiteSpace(tipoServico.Tipo))
                return BadRequest(new { message = "O nome é obrigatório." });

            if (tipoServico.TempoEstimado <= 0)
                return BadRequest(new { message = "O tempo estimado deve ser maior que zero." });

            existing.Tipo = tipoServico.Tipo;
            existing.Descricao = tipoServico.Descricao;
            existing.TempoEstimado = tipoServico.TempoEstimado;

            await _context.TipoServico.ReplaceOneAsync(t => t.Id == id, existing);

            return Ok(new { message = "Tipo de serviço atualizado com sucesso." });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existing = await _context.TipoServico.Find(t => t.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Tipo de serviço não encontrado." });

            await _context.TipoServico.DeleteOneAsync(t => t.Id == id);
            return Ok(new { message = "Tipo de serviço deletado com sucesso." });
        }
    }
}