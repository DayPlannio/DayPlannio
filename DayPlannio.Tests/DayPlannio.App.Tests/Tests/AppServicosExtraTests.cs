using DayPlannio.Api.IntegrationTests.Infra;
using DayPlannio.App.Services;
using Xunit;

namespace DayPlannio.App.Tests.Tests;

[Collection(AppApiCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class AppServicosExtraTests
{
    private readonly ApiFactory _f;
    public AppServicosExtraTests(ApiFactory f) => _f = f;

    private sealed class SemConexao : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Sem conexão");
    }

    private HttpClient NovoCelular()
    {
        var client = _f.NovoClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        DayPlannio.App.App.HttpClient = client;
        return client;
    }

    private async Task<(string userId, string email, string senha)> CadastrarPeloAppAsync()
    {
        var email = $"app_{Guid.NewGuid():N}@teste.local";
        const string senha = "Senha@123";
        var (userId, erro) = await UsuarioService.Cadastrar(new
        {
            nomeCompleto = $"Prestador App {Guid.NewGuid():N}"[..20],
            email,
            senha,
            confirmeSenha = senha,
            usarTesteGratuito = true,
            aceitouPolitica = true,
            plano = (string?)null
        });
        Assert.True(userId != null, $"Cadastro falhou: {erro}");
        return (userId!, email, senha);
    }

    private static object NovoAgendamento(string userId, string clienteId, string servicoId, DateTime quando, decimal valor = 120m) => new
    {
        usuarioId = userId,
        clienteId,
        tipoServicoId = servicoId,
        dataHora = quando,
        valorCobrado = valor,
        custoMaterial = 15m,
        observacoes = "Levar material"
    };

    private async Task<(string userId, string clienteId, string servicoId)> CenarioAgendaAsync()
    {
        NovoCelular();
        var (userId, _, _) = await CadastrarPeloAppAsync();
        await ClienteService.Create(new { usuarioId = userId, nome = "Cliente Extra", telefone = "14999990000", endereco = "Rua E, 50" });
        await ServicoService.Create(new { usuarioId = userId, tipo = "Pintura", descricao = "Geral", tempoEstimado = 45 });
        var cliente = (await ClienteService.GetClientes(userId))!.Single();
        var servico = (await ServicoService.GetServicos(userId))!.Single();
        return (userId, cliente.Id, servico.Id);
    }

    [Fact(DisplayName = "AI-23 Solicitar plano e cancelar a renovação pelo App mostram as mensagens da API")]
    public async Task Plano_SolicitarERenovar()
    {
        NovoCelular();
        var (userId, _, _) = await CadastrarPeloAppAsync();

        var solicitar = await UsuarioService.SolicitarPlano(userId, "Profissional");
        var cancelar = await UsuarioService.DefinirRenovacao(userId, false);
        var reativar = await UsuarioService.DefinirRenovacao(userId, true);

        Assert.True(solicitar.sucesso);
        Assert.Contains("Aguardando", solicitar.mensagem);
        Assert.True(cancelar.sucesso);
        Assert.Contains("cancelado", cancelar.mensagem);
        Assert.True(reativar.sucesso);
    }

    [Fact(DisplayName = "AI-24 Plano: usuário vazio devolve falha com mensagem")]
    public async Task Plano_UsuarioVazio_Falha()
    {
        NovoCelular();
        await CadastrarPeloAppAsync();

        var solicitar = await UsuarioService.SolicitarPlano("", "Full");
        var renovar = await UsuarioService.DefinirRenovacao("", true);

        Assert.False(solicitar.sucesso);
        Assert.False(string.IsNullOrWhiteSpace(solicitar.mensagem));
        Assert.False(renovar.sucesso);
        Assert.False(string.IsNullOrWhiteSpace(renovar.mensagem));
    }

    [Fact(DisplayName = "AI-25 Política: o App consulta a versão aceita e registra a concordância")]
    public async Task Politica_ConsultaERegistra()
    {
        NovoCelular();
        var (userId, _, _) = await CadastrarPeloAppAsync();

        var aceite = await UsuarioService.GetPoliticaAceite(userId);
        var registro = await UsuarioService.RegistrarConcordanciaPolitica(userId);

        Assert.NotNull(aceite.aceitaEm);
        Assert.False(string.IsNullOrWhiteSpace(aceite.versao));
        Assert.True(registro.sucesso);
        Assert.False(string.IsNullOrWhiteSpace(registro.mensagem));
    }

    [Fact(DisplayName = "AI-26 Sem internet, o App devolve mensagens amigáveis em vez de quebrar")]
    public async Task SemInternet_DevolveMensagens()
    {
        DayPlannio.App.App.HttpClient = new HttpClient(new SemConexao()) { BaseAddress = new Uri("http://localhost/") };
        var id = Guid.NewGuid().ToString();

        var aceite = await UsuarioService.GetPoliticaAceite(id);
        var registro = await UsuarioService.RegistrarConcordanciaPolitica(id);
        var encerrar = await UsuarioService.EncerrarConta(id, "politica");
        var cancelar = await AgendamentoService.Cancelar(id, "Motivo");

        Assert.Null(aceite.aceitaEm);
        Assert.Null(aceite.versao);
        Assert.False(registro.sucesso);
        Assert.Contains("conexão", registro.mensagem);
        Assert.False(encerrar.sucesso);
        Assert.Contains("conexão", encerrar.mensagem);
        Assert.False(cancelar.sucesso);
        Assert.Equal("Sem conexão", cancelar.erro);
    }

    [Fact(DisplayName = "AI-27 Encerrar a conta pelo App apaga o usuário e o login deixa de funcionar")]
    public async Task EncerrarConta_ApagaUsuario()
    {
        NovoCelular();
        var (userId, email, senha) = await CadastrarPeloAppAsync();

        var encerrar = await UsuarioService.EncerrarConta(userId, "politica");
        NovoCelular();
        var login = await UsuarioService.Login(email, senha);

        Assert.True(encerrar.sucesso);
        Assert.False(string.IsNullOrWhiteSpace(encerrar.mensagem));
        Assert.Null(login);
    }

    [Fact(DisplayName = "AI-28 Editar o perfil pelo App atualiza os dados")]
    public async Task Perfil_Editar()
    {
        NovoCelular();
        var (userId, email, _) = await CadastrarPeloAppAsync();
        var novoNome = $"Nome do App {Guid.NewGuid():N}"[..22];

        var edicao = await UsuarioService.Edit(userId, new
        {
            nomeCompleto = novoNome,
            email,
            telefone = "14966665555",
            cidade = "Bauru",
            cidadeVisivel = true,
            telefoneVisivel = false
        });
        var perfil = await UsuarioService.GetPerfil(userId);

        Assert.True(edicao.sucesso, edicao.erro);
        Assert.NotNull(perfil);
        Assert.Equal(novoNome, perfil!.NomeCompleto);
        Assert.Equal("Bauru", perfil.Cidade);
    }

    [Fact(DisplayName = "AI-29 Recuperação de senha pelo App: e-mail vazio falha e código errado é recusado")]
    public async Task RecuperacaoDeSenha_Falhas()
    {
        NovoCelular();
        var (_, email, _) = await CadastrarPeloAppAsync();
        await UsuarioService.EnviarRedefinicaoSenha(email);

        var vazio = await UsuarioService.EnviarRedefinicaoSenha("");
        var verificar = await UsuarioService.VerificarCodigo(email, "abcdef");
        var redefinir = await UsuarioService.RedefinirSenha(email, "abcdef", "Nova@12345", "Nova@12345");

        Assert.False(vazio.sucesso);
        Assert.Equal("Não foi possível enviar o e-mail. Tente novamente.", vazio.mensagem);
        Assert.False(verificar.sucesso);
        Assert.Contains("inválido", verificar.mensagem);
        Assert.False(redefinir.sucesso);
        Assert.Contains("inválido", redefinir.mensagem);
    }

    [Fact(DisplayName = "AI-30 Financeiro pelo App: editar, excluir, listar por período, resumo e relatório em PDF")]
    public async Task Financeiro_EditarExcluirEPeriodos()
    {
        NovoCelular();
        var (userId, _, _) = await CadastrarPeloAppAsync();
        var hoje = DateTime.UtcNow;
        await FinanceiroService.Create(new { usuarioId = userId, tipo = "Entrada", descricao = "Serviço", valor = 500m, data = hoje });
        await FinanceiroService.Create(new { usuarioId = userId, tipo = "Saida", descricao = "Material", valor = 120m, data = hoje });
        var registros = (await FinanceiroService.GetAll(userId))!;
        var entrada = registros.Single(r => r.Tipo == "Entrada");
        var saida = registros.Single(r => r.Tipo == "Saida");

        var edicao = await FinanceiroService.Edit(entrada.Id.ToString(),
            new { usuarioId = userId, tipo = "Entrada", descricao = "Serviço corrigido", valor = 650m, data = hoje });
        var invalida = await FinanceiroService.Edit(entrada.Id.ToString(),
            new { usuarioId = userId, tipo = "Entrada", descricao = "Zero", valor = 0m, data = hoje });
        var excluiu = await FinanceiroService.Delete(saida.Id.ToString());
        var excluiuDeNovo = await FinanceiroService.Delete(saida.Id.ToString());

        Assert.True(edicao.sucesso, edicao.erro);
        Assert.False(invalida.sucesso);
        Assert.False(string.IsNullOrWhiteSpace(invalida.erro));
        Assert.True(excluiu);
        Assert.False(excluiuDeNovo);

        var restantes = (await FinanceiroService.GetAll(userId))!;
        Assert.Single(restantes);
        Assert.Equal("Serviço corrigido", restantes[0].Descricao);
        Assert.Equal(650m, restantes[0].Valor);

        var diario = await FinanceiroService.GetByPeriodo(userId, "diario");
        var resumo = await FinanceiroService.GetResumoGeral(userId, "mensal");
        var pdf = await FinanceiroService.GetRelatorioPdf(userId, "mensal");

        Assert.Single(diario!);
        Assert.Equal(650m, resumo!.EntradasAvulsas);
        Assert.NotNull(pdf);
        Assert.Equal((byte)'%', pdf![0]);
        Assert.Equal((byte)'P', pdf[1]);
    }

    [Fact(DisplayName = "AI-31 Financeiro: dados de outro prestador não chegam ao App")]
    public async Task Financeiro_DeOutro_RetornaNulo()
    {
        NovoCelular();
        await CadastrarPeloAppAsync();
        var outro = Guid.NewGuid().ToString();

        Assert.Null(await FinanceiroService.GetAll(outro));
        Assert.Null(await FinanceiroService.GetResumoGeral(outro));
        Assert.Null(await FinanceiroService.GetByPeriodo(outro));
        Assert.Null(await FinanceiroService.GetRelatorioPdf(outro));
        Assert.False(await FinanceiroService.Delete(Guid.NewGuid().ToString()));
    }

    [Fact(DisplayName = "AI-32 Agenda pelo App: listar próximos, editar, conflito de horário e falhas")]
    public async Task Agenda_EditarEFalhas()
    {
        var (userId, clienteId, servicoId) = await CenarioAgendaAsync();
        var primeiro = ApiHelper.Futuro(2, 9);
        var segundo = ApiHelper.Futuro(2, 15);
        await AgendamentoService.Create(NovoAgendamento(userId, clienteId, servicoId, primeiro));
        await AgendamentoService.Create(NovoAgendamento(userId, clienteId, servicoId, segundo));
        var agenda = (await AgendamentoService.GetAgenda(userId, "proximos"))!;
        Assert.Equal(2, agenda.Count);
        var alvo = agenda.OrderBy(a => a.DataHora).First();

        var edicao = await AgendamentoService.Edit(alvo.Id, NovoAgendamento(userId, clienteId, servicoId, ApiHelper.Futuro(3, 10), 200m));
        var conflito = await AgendamentoService.Edit(alvo.Id, NovoAgendamento(userId, clienteId, servicoId, segundo));
        var inexistente = await AgendamentoService.Edit(Guid.NewGuid().ToString(), NovoAgendamento(userId, clienteId, servicoId, ApiHelper.Futuro(5)));
        var duplicado = await AgendamentoService.Create(NovoAgendamento(userId, clienteId, servicoId, segundo));
        var cancelar = await AgendamentoService.Cancelar(Guid.NewGuid().ToString(), "Motivo qualquer");

        Assert.True(edicao.sucesso, edicao.erro);
        Assert.False(conflito.sucesso);
        Assert.False(string.IsNullOrWhiteSpace(conflito.erro));
        Assert.False(inexistente.sucesso);
        Assert.False(duplicado.sucesso);
        Assert.Contains("horário", duplicado.erro);
        Assert.False(cancelar.sucesso);
        Assert.False(await AgendamentoService.Concluir(Guid.NewGuid().ToString()));
        Assert.False(await AgendamentoService.Delete(Guid.NewGuid().ToString()));
        Assert.NotNull(await AgendamentoService.GetAgenda(userId, "mensal"));
    }
}
