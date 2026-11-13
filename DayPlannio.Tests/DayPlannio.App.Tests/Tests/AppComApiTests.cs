using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DayPlannio.Api.IntegrationTests.Infra;
using DayPlannio.App.Models;
using DayPlannio.App.Services;
using Xunit;

namespace DayPlannio.App.Tests.Tests;

[CollectionDefinition(Nome)]
public sealed class AppApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Nome = "App MAUI ↔ API";
}

[Collection(AppApiCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class AppComApiTests
{
    private readonly ApiFactory _f;
    public AppComApiTests(ApiFactory f) => _f = f;

    private static void Celular(HttpClient client) => DayPlannio.App.App.HttpClient = client;

    private HttpClient NovoCelular() { var c = _f.NovoClient(); Celular(c); return c; }

    private async Task<(string userId, string email, string senha)> CadastrarPeloAppAsync(
        bool teste = true, string? plano = null, bool politica = true)
    {
        var email = $"app_{Guid.NewGuid():N}@teste.local";
        const string senha = "Senha@123";
        var (userId, erro) = await UsuarioService.Cadastrar(new
        {
            nomeCompleto = $"Prestador App {Guid.NewGuid():N}"[..20],
            email,
            senha,
            confirmeSenha = senha,
            usarTesteGratuito = teste,
            aceitouPolitica = politica,
            plano
        });
        Assert.True(userId != null, $"Cadastro falhou: {erro}");
        return (userId!, email, senha);
    }

    [Fact(DisplayName = "AI-05 Cadastro pelo App: devolve o userId e o perfil volta com plano de teste ativo")]
    public async Task Cadastro_RetornaUserId_EPerfil()
    {
        NovoCelular();

        var (userId, email, _) = await CadastrarPeloAppAsync();

        Assert.True(Guid.TryParse(userId, out _));
        var perfil = await UsuarioService.GetPerfil(userId);
        Assert.NotNull(perfil);
        Assert.Equal(email, perfil!.Email);
        Assert.True(perfil.PlanoAtivo);
        Assert.Equal("trial", perfil.PlanoOrigem);
    }

    [Fact(DisplayName = "AI-06 Cadastro pelo App com e-mail repetido devolve a mensagem de erro da API")]
    public async Task Cadastro_EmailRepetido_DevolveMensagem()
    {
        NovoCelular();
        var (_, email, _) = await CadastrarPeloAppAsync();

        Celular(_f.NovoClient());
        var (userId, erro) = await UsuarioService.Cadastrar(new
        {
            nomeCompleto = "Outro Nome",
            email,
            senha = "Senha@123",
            confirmeSenha = "Senha@123",
            usarTesteGratuito = true,
            aceitouPolitica = true,
            plano = (string?)null
        });

        Assert.Null(userId);
        Assert.Contains("já está cadastrado", erro);
    }

    [Fact(DisplayName = "AI-07 Cadastro pelo App sem concordar com a política é recusado com mensagem")]
    public async Task Cadastro_SemPolitica_Recusado()
    {
        NovoCelular();

        var (userId, erro) = await UsuarioService.Cadastrar(new
        {
            nomeCompleto = "Sem Politica",
            email = $"sp_{Guid.NewGuid():N}@teste.local",
            senha = "Senha@123",
            confirmeSenha = "Senha@123",
            usarTesteGratuito = true,
            aceitouPolitica = false,
            plano = (string?)null
        });

        Assert.Null(userId);
        Assert.Contains("Política de Privacidade", erro);
    }

    [Fact(DisplayName = "AI-08 Login pelo App: credenciais certas devolvem o userId; erradas devolvem nulo")]
    public async Task Login_CertoEErrado()
    {
        NovoCelular();
        var (userId, email, senha) = await CadastrarPeloAppAsync();

        NovoCelular();
        Assert.Equal(userId, await UsuarioService.Login(email, senha));
        Assert.Null(await UsuarioService.Login(email, "Errada@999"));
        Assert.Null(await UsuarioService.Login($"nao_{Guid.NewGuid():N}@teste.local", senha));
    }

    [Fact(DisplayName = "AI-09 Recuperação de senha pelo App: enviar código → verificar → redefinir → entrar com a nova senha")]
    public async Task RecuperacaoDeSenha_FluxoDoApp()
    {
        NovoCelular();
        var (userId, email, _) = await CadastrarPeloAppAsync();
        const string nova = "NovaSenha@456";

        NovoCelular();
        var (enviou, _) = await UsuarioService.EnviarRedefinicaoSenha(email);
        Assert.True(enviou);
        var codigo = _f.Emails.UltimoCodigoPara(email);
        Assert.NotNull(codigo);

        var (codigoOk, _) = await UsuarioService.VerificarCodigo(email, codigo!);
        Assert.True(codigoOk);
        var (errado, _) = await UsuarioService.VerificarCodigo(email, codigo == "000000" ? "111111" : "000000");
        Assert.False(errado);

        var (redefiniu, msg) = await UsuarioService.RedefinirSenha(email, codigo!, nova, nova);
        Assert.True(redefiniu, msg);

        Assert.Null(await UsuarioService.Login(email, "Senha@123"));
        Assert.Equal(userId, await UsuarioService.Login(email, nova));
    }

    [Fact(DisplayName = "AI-10 Tipos de serviço pelo App: criar, listar, editar e excluir")]
    public async Task Servicos_Crud()
    {
        NovoCelular();
        var (userId, _, _) = await CadastrarPeloAppAsync();

        var (criou, erro) = await ServicoService.Create(new { usuarioId = userId, tipo = "Limpeza", descricao = "Geral", tempoEstimado = 60 });
        Assert.True(criou, erro);

        var lista = await ServicoService.GetServicos(userId);
        var servico = Assert.Single(lista!);
        Assert.Equal("Limpeza", servico.Tipo);
        Assert.Equal(60, servico.TempoEstimado);

        var (editou, erroEdit) = await ServicoService.Edit(servico.Id, new { usuarioId = userId, tipo = "Limpeza Pesada", tempoEstimado = 90 });
        Assert.True(editou, erroEdit);
        Assert.Equal("Limpeza Pesada", Assert.Single((await ServicoService.GetServicos(userId))!).Tipo);

        var (excluiu, _) = await ServicoService.Delete(servico.Id);
        Assert.True(excluiu);
        Assert.Empty((await ServicoService.GetServicos(userId))!);
    }

    [Fact(DisplayName = "AI-11 Serviço inválido (tempo 0): o App recebe a falha e não grava")]
    public async Task Servico_Invalido_NaoGrava()
    {
        NovoCelular();
        var (userId, _, _) = await CadastrarPeloAppAsync();

        var (criou, _) = await ServicoService.Create(new { usuarioId = userId, tipo = "X", tempoEstimado = 0 });

        Assert.False(criou);
        Assert.Empty((await ServicoService.GetServicos(userId))!);
    }

    [Fact(DisplayName = "AI-12 Clientes pelo App: cadastrar, listar (raio padrão 100 m), editar e excluir")]
    public async Task Clientes_Crud()
    {
        NovoCelular();
        var (userId, _, _) = await CadastrarPeloAppAsync();

        var (criou, erro) = await ClienteService.Create(new
        {
            usuarioId = userId,
            nome = "Maria Souza",
            telefone = "14999990000",
            endereco = "Rua A, 10"
        });
        Assert.True(criou, erro);

        var cliente = Assert.Single((await ClienteService.GetClientes(userId))!);
        Assert.Equal("Maria Souza", cliente.Nome);
        Assert.Equal(100, cliente.RaioGeofenceMetros);

        var (editou, erroEdit) = await ClienteService.Edit(cliente.Id, new
        {
            id = cliente.Id,
            usuarioId = userId,
            nome = "Maria Souza Lima",
            telefone = "14999990000",
            endereco = "Rua A, 10",
            raioGeofenceMetros = 20
        });
        Assert.True(editou, erroEdit);
        var editado = Assert.Single((await ClienteService.GetClientes(userId))!);
        Assert.Equal("Maria Souza Lima", editado.Nome);
        Assert.Equal(20, editado.RaioGeofenceMetros);

        var (excluiu, _) = await ClienteService.Delete(cliente.Id);
        Assert.True(excluiu);
        Assert.Empty((await ClienteService.GetClientes(userId))!);
    }

    [Fact(DisplayName = "AI-13 Gerar acesso do cliente pelo App devolve e-mail e senha provisória")]
    public async Task Clientes_GerarCredenciais()
    {
        NovoCelular();
        var (userId, _, _) = await CadastrarPeloAppAsync();
        await ClienteService.Create(new { usuarioId = userId, nome = "Cliente Portal", telefone = "14999990000", endereco = "Rua B, 20" });
        var cliente = Assert.Single((await ClienteService.GetClientes(userId))!);

        var (ok, email, senha, erro) = await ClienteService.GerarCredenciais(cliente.Id);

        Assert.True(ok, erro);
        Assert.False(string.IsNullOrWhiteSpace(email));
        Assert.Equal(10, senha.Length);
    }

    [Fact(DisplayName = "AI-14 Gerar acesso com plano que não é Full: o App recebe a mensagem de bloqueio da API")]
    public async Task Clientes_GerarCredenciais_PlanoInsuficiente()
    {
        NovoCelular();
        var (userId, _, _) = await CadastrarPeloAppAsync(teste: false, plano: "Basico");
        await _f.ClientAdmin().PostAsync($"api/Admin/planos/{userId}/aprovar", null);
        await ClienteService.Create(new { usuarioId = userId, nome = "Cliente Basico", telefone = "14999990000", endereco = "Rua C, 30" });
        var cliente = Assert.Single((await ClienteService.GetClientes(userId))!);

        var (ok, _, _, erro) = await ClienteService.GerarCredenciais(cliente.Id);

        Assert.False(ok);
        Assert.Contains("Full", erro);
    }

    private async Task<(string userId, string clienteId, string servicoId)> CenarioAgendaAsync()
    {
        NovoCelular();
        var (userId, _, _) = await CadastrarPeloAppAsync();
        await ClienteService.Create(new { usuarioId = userId, nome = "Cliente Agenda", telefone = "14999990000", endereco = "Rua D, 40" });
        await ServicoService.Create(new { usuarioId = userId, tipo = "Corte", tempoEstimado = 45 });
        var cliente = (await ClienteService.GetClientes(userId))!.Single();
        var servico = (await ServicoService.GetServicos(userId))!.Single();
        return (userId, cliente.Id, servico.Id);
    }

    private static object NovoAgendamento(string userId, string clienteId, string servicoId, DateTime quando) => new
    {
        usuarioId = userId,
        clienteId,
        tipoServicoId = servicoId,
        dataHora = quando,
        valorCobrado = 120m,
        custoMaterial = 15m,
        observacoes = "Levar material"
    };

    [Fact(DisplayName = "AI-15 Agenda pelo App: criar, recusar horário repetido, cancelar com motivo e concluir")]
    public async Task Agenda_CriarCancelarConcluir()
    {
        var (userId, clienteId, servicoId) = await CenarioAgendaAsync();
        var horario = ApiHelper.Futuro(3, 9);

        var (criou, erro) = await AgendamentoService.Create(NovoAgendamento(userId, clienteId, servicoId, horario));
        Assert.True(criou, erro);

        var (duplicou, msgDuplicado) = await AgendamentoService.Create(NovoAgendamento(userId, clienteId, servicoId, horario));
        Assert.False(duplicou);
        Assert.Contains("Já existe um agendamento", msgDuplicado);

        var historico = await AgendamentoService.GetHistorico(clienteId);
        var ag = Assert.Single(historico!);
        Assert.Equal("Agendado", ag.Status);
        Assert.Equal(120m, ag.ValorCobrado);

        var (semMotivo, _) = await AgendamentoService.Cancelar(ag.Id, "  ");
        Assert.False(semMotivo);

        var (cancelou, _) = await AgendamentoService.Cancelar(ag.Id, "Cliente viajou");
        Assert.True(cancelou);
        var cancelado = Assert.Single((await AgendamentoService.GetHistorico(clienteId))!);
        Assert.Equal("Cancelado", cancelado.Status);
        Assert.Equal("Cliente viajou", cancelado.MotivoCancelamento);

        await AgendamentoService.Create(NovoAgendamento(userId, clienteId, servicoId, horario));
        var novo = (await AgendamentoService.GetHistorico(clienteId))!.Single(a => a.Status == "Agendado");
        Assert.True(await AgendamentoService.Concluir(novo.Id));
        Assert.Contains((await AgendamentoService.GetHistorico(clienteId))!, a => a.Id == novo.Id && a.Status == "Concluido");
    }

    [Fact(DisplayName = "AI-16 Excluir agendamento pelo App")]
    public async Task Agenda_Excluir()
    {
        var (userId, clienteId, servicoId) = await CenarioAgendaAsync();
        await AgendamentoService.Create(NovoAgendamento(userId, clienteId, servicoId, ApiHelper.Futuro(4, 10)));
        var ag = (await AgendamentoService.GetHistorico(clienteId))!.Single();

        Assert.True(await AgendamentoService.Delete(ag.Id));

        Assert.Empty((await AgendamentoService.GetHistorico(clienteId))!);
    }

    [Fact(DisplayName = "AI-17 Excluir cliente pelo App remove os agendamentos futuros e mantém o histórico (nova regra)")]
    public async Task Cliente_Excluir_MantemHistorico()
    {
        var (userId, clienteId, servicoId) = await CenarioAgendaAsync();
        await AgendamentoService.Create(NovoAgendamento(userId, clienteId, servicoId, ApiHelper.Futuro(2, 8)));
        var feito = (await AgendamentoService.GetHistorico(clienteId))!.Single();
        await AgendamentoService.Concluir(feito.Id);
        await AgendamentoService.Create(NovoAgendamento(userId, clienteId, servicoId, ApiHelper.Futuro(6, 14)));

        var (excluiu, _) = await ClienteService.Delete(clienteId);

        Assert.True(excluiu);
        var restantes = (await AgendamentoService.GetHistorico(clienteId))!;
        var historico = Assert.Single(restantes);
        Assert.Equal(feito.Id, historico.Id);
        Assert.Equal("Concluido", historico.Status);
        Assert.NotNull(historico.ClienteEncerradoEm);
    }

    [Fact(DisplayName = "AI-18 Financeiro pelo App: lançar entrada e saída, listar e consultar o resumo")]
    public async Task Financeiro_LancaEConsulta()
    {
        NovoCelular();
        var (userId, _, _) = await CadastrarPeloAppAsync();
        var hoje = DateTime.UtcNow;

        var (entrada, e1) = await FinanceiroService.Create(new { usuarioId = userId, tipo = "Entrada", descricao = "Serviço", valor = 500m, data = hoje });
        var (saida, e2) = await FinanceiroService.Create(new { usuarioId = userId, tipo = "Saida", descricao = "Material", valor = 120m, data = hoje });
        Assert.True(entrada, e1);
        Assert.True(saida, e2);

        var todos = await FinanceiroService.GetAll(userId);
        Assert.Equal(2, todos!.Count);
        Assert.Contains(todos, f => f.Tipo == "Entrada" && f.Valor == 500m);

        var resumo = await FinanceiroService.GetResumoGeral(userId, "mensal");
        Assert.NotNull(resumo);
        Assert.Equal(500m, resumo!.EntradasAvulsas);
        Assert.Equal(120m, resumo.SaidasAvulsas);
    }

    [Fact(DisplayName = "AI-19 Lançamento financeiro inválido: o App recebe a falha")]
    public async Task Financeiro_Invalido()
    {
        NovoCelular();
        var (userId, _, _) = await CadastrarPeloAppAsync();

        var (ok, _) = await FinanceiroService.Create(new { usuarioId = userId, tipo = "Entrada", descricao = "Zero", valor = 0m, data = DateTime.UtcNow });

        Assert.False(ok);
    }

    [Fact(DisplayName = "AI-20 Métricas do prestador pelo App refletem um atendimento concluído")]
    public async Task Metricas_RefletemAtendimentoConcluido()
    {
        var (userId, clienteId, servicoId) = await CenarioAgendaAsync();
        await AgendamentoService.Create(NovoAgendamento(userId, clienteId, servicoId, ApiHelper.Futuro(1, 11)));
        var ag = (await AgendamentoService.GetHistorico(clienteId))!.Single();
        await AgendamentoService.Concluir(ag.Id);

        var m = await MetricasService.GetMetricas(userId, "mensal");

        Assert.NotNull(m);
        Assert.Equal(1, m!.Atendimentos);
        Assert.Equal(120m, m.Faturamento);
    }

    [Fact(DisplayName = "AI-21 Sem sessão, o App não recebe dados (listas nulas)")]
    public async Task SemSessao_NaoRecebeDados()
    {
        NovoCelular();
        var (userId, _, _) = await CadastrarPeloAppAsync();

        Celular(_f.NovoClient());

        Assert.Null(await ClienteService.GetClientes(userId));
        Assert.Null(await ServicoService.GetServicos(userId));
        Assert.Null(await FinanceiroService.GetAll(userId));
    }

    [Fact(DisplayName = "AI-22 Um prestador logado no App não enxerga os dados de outro prestador")]
    public async Task Isolamento_EntrePrestadores()
    {
        NovoCelular();
        var (idA, _, _) = await CadastrarPeloAppAsync();
        await ClienteService.Create(new { usuarioId = idA, nome = "Cliente do A", telefone = "14999990000", endereco = "Rua E, 50" });

        NovoCelular();
        await CadastrarPeloAppAsync();

        Assert.Null(await ClienteService.GetClientes(idA));
    }
}