using DayPlannio.Api.Models;
using MongoDB.Driver;
using DayPlannio.Api.Services;

namespace DayPlannio.Api.Helpers
{
    public static class ContaClienteHelper
    {
       public static async Task<int> EncerrarENotificarAsync(
            ContextMongodb context,
            Cliente cliente,
            AzureBlobStorageService blobService,
            bool encerradaPeloAdmin = false)
        {
            var afetados = await BuscarFuturosAsync(context, cliente.Id);

           await EncerrarAsync(context, blobService, cliente.Id);
            await AvisarPrestadoresAsync(context, afetados, cliente.UsuarioId, cliente.Nome, encerradaPeloAdmin);

            return afetados.Count;
        }

        private static async Task<List<Agendamento>> BuscarFuturosAsync(ContextMongodb context, Guid clienteId)
        {
            var filtro = Builders<Agendamento>.Filter.And(
                Builders<Agendamento>.Filter.Eq(a => a.ClienteId, clienteId),
                Builders<Agendamento>.Filter.Gte(a => a.DataHora, DateTime.UtcNow),
                Builders<Agendamento>.Filter.Nin(a => a.Status,
                    new[] { StatusAgendamento.Cancelado, StatusAgendamento.Concluido }));

            return await context.Agendamento.Find(filtro).ToListAsync();
        }

        private static async Task AvisarPrestadoresAsync(
            ContextMongodb context,
            List<Agendamento> agendamentos,
            Guid prestadorDonoId,
            string clienteNome,
            bool encerradaPeloAdmin)
        {
            var quantidades = agendamentos
                .GroupBy(a => a.UsuarioId)
                .ToDictionary(g => g.Key, g => g.Count());

            if (!quantidades.ContainsKey(prestadorDonoId))
                quantidades[prestadorDonoId] = 0;

            var agora = DateTime.UtcNow;
            var nome = string.IsNullOrWhiteSpace(clienteNome) ? "Um cliente" : clienteNome.Trim();
            var acao = encerradaPeloAdmin ? "teve a conta encerrada" : "encerrou a conta";

            var lista = quantidades.Select(item =>
            {
                var mensagem = item.Value == 0
                    ? $"{nome} {acao}."
                    : item.Value == 1
                        ? $"{nome} {acao} e 1 agendamento futuro foi cancelado e removido da sua agenda."
                        : $"{nome} {acao} e {item.Value} agendamentos futuros foram cancelados e removidos da sua agenda.";

                return new Notificacao
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = item.Key,
                    Titulo = "Cliente encerrou a conta",
                    Mensagem = mensagem,
                    Tipo = "cliente_encerrou_conta",
                    Lida = false,
                    DataCriacao = agora
                };
            }).ToList();

            await context.Notificacao.InsertManyAsync(lista);
        }

        private static async Task RemoverFotosAsync(
            ContextMongodb context,
            AzureBlobStorageService blobService,
            Guid clienteId)
        {
            var agendamentoIds = await context.Agendamento
                .Find(a => a.ClienteId == clienteId)
                .Project(a => a.Id)
                .ToListAsync();
        
            if (agendamentoIds.Count == 0) return;
        
            var filtroFotos = Builders<Foto>.Filter.In(f => f.AgendamentoId, agendamentoIds);
            var fotos = await context.Foto.Find(filtroFotos).ToListAsync();
        
            foreach (var foto in fotos)
            {
                try
                {
                    await blobService.DeleteAsync(foto.Url);
                }
                catch
                {
                }
            }
        
            await context.Foto.DeleteManyAsync(filtroFotos);
        }

        public static async Task<int> EncerrarAsync(
            ContextMongodb context,
            AzureBlobStorageService blobService,
            Guid clienteId)
        {
            var agora = DateTime.UtcNow;
        
            await RemoverFotosAsync(context, blobService, clienteId);

            var futuros = Builders<Agendamento>.Filter.And(
                Builders<Agendamento>.Filter.Eq(a => a.ClienteId, clienteId),
                Builders<Agendamento>.Filter.Gte(a => a.DataHora, agora),
                Builders<Agendamento>.Filter.Nin(a => a.Status,
                    new[] { StatusAgendamento.Cancelado, StatusAgendamento.Concluido }));

            var removidos = await context.Agendamento.DeleteManyAsync(futuros);

            var limpar = Builders<Agendamento>.Update
                .Set(a => a.ClienteEncerradoEm, agora)
                .Set(a => a.EnderecoAtendimento, null)
                .Set(a => a.Latitude, null)
                .Set(a => a.Longitude, null)
                .Set(a => a.Observacoes, null);

            await context.Agendamento.UpdateManyAsync(a => a.ClienteId == clienteId, limpar);

            await context.Cliente.DeleteOneAsync(c => c.Id == clienteId);

            return (int)removidos.DeletedCount;
        }
    }
}
