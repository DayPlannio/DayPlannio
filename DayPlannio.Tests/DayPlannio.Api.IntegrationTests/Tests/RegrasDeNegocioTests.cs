using DayPlannio.Api.Helpers;
using DayPlannio.Api.IntegrationTests.Infra;
using DayPlannio.Api.Models;
using DayPlannio.Api.Services;
using DayPlannio.Api.Settings;
using Microsoft.Extensions.Options;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Tests;

[Trait("Categoria", "Unitario")]
public class RegrasDeNegocioTests
{
    private static readonly DateTime Agora = new(2026, 3, 18, 15, 0, 0, DateTimeKind.Utc);

    private static Agendamento Novo(StatusAgendamento status) => new() { Id = Guid.NewGuid(), Status = status };

    [Theory(DisplayName = "UT-01 Raio do geofence só aceita 20, 50 ou 100 metros")]
    [InlineData(20, 20)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(0, 100)]
    [InlineData(75, 100)]
    [InlineData(-5, 100)]
    public void NormalizarRaio(int entrada, int esperado) =>
        Assert.Equal(esperado, GeofenceRegras.NormalizarRaio(entrada));

    [Fact(DisplayName = "UT-02 Precisão máxima aceita fica entre 25 e 50 metros quando não há limite configurado")]
    public void PrecisaoMaximaAceita()
    {
        if (GeofenceRegras.PrecisaoMaximaConfigurada > 0)
        {
            Assert.Equal(GeofenceRegras.PrecisaoMaximaConfigurada, GeofenceRegras.PrecisaoMaximaAceita(20));
            return;
        }

        Assert.Equal(25, GeofenceRegras.PrecisaoMaximaAceita(20));
        Assert.Equal(50, GeofenceRegras.PrecisaoMaximaAceita(50));
        Assert.Equal(50, GeofenceRegras.PrecisaoMaximaAceita(100));
    }

    [Fact(DisplayName = "UT-03 Distância entre coordenadas: zero no mesmo ponto e ~111 km por grau de latitude")]
    public void DistanciaMetros()
    {
        Assert.Equal(0, GeofenceRegras.DistanciaMetros(-22.29, -48.55, -22.29, -48.55), 3);
        Assert.InRange(GeofenceRegras.DistanciaMetros(0, 0, 1, 0), 111_000, 111_400);
        Assert.Equal(
            GeofenceRegras.DistanciaMetros(-22.29, -48.55, -22.30, -48.56),
            GeofenceRegras.DistanciaMetros(-22.30, -48.56, -22.29, -48.55), 6);
    }

    [Fact(DisplayName = "UT-04 Chegada ao raio registra a chegada; fora do raio não faz nada")]
    public void Avaliar_Agendado_Chegada()
    {
        var fora = Novo(StatusAgendamento.Agendado);
        var dentro = Novo(StatusAgendamento.Agendado);

        Assert.Equal(EventoGeofence.Nenhum, GeofenceRegras.Avaliar(fora, 500, 50, 5, Agora));
        Assert.Equal(EventoGeofence.Chegou, GeofenceRegras.Avaliar(dentro, 10, 50, 5, Agora));
        Assert.Equal(Agora, dentro.ChegadaEm);
        Assert.Null(fora.ChegadaEm);
    }

    [Fact(DisplayName = "UT-05 Permanecer no raio pelo tempo de confirmação inicia o atendimento")]
    public void Avaliar_Agendado_Confirmacao()
    {
        var ag = Novo(StatusAgendamento.Agendado);
        var chegada = Agora - GeofenceRegras.ConfirmacaoChegada - TimeSpan.FromSeconds(10);
        ag.ChegadaEm = chegada;
        ag.UltimoPingEm = Agora - TimeSpan.FromSeconds(30);

        var evento = GeofenceRegras.Avaliar(ag, 10, 50, 5, Agora);

        Assert.Equal(EventoGeofence.Iniciou, evento);
        Assert.Equal(StatusAgendamento.EmAtendimento, ag.Status);
        Assert.Equal(chegada, ag.Inicio);
        Assert.True(ag.IniciadoPorGeofence);
    }

    [Fact(DisplayName = "UT-06 Sair do raio antes de confirmar descarta a chegada")]
    public void Avaliar_Agendado_SaidaDescartaChegada()
    {
        var ag = Novo(StatusAgendamento.Agendado);
        ag.ChegadaEm = Agora.AddMinutes(-1);
        ag.UltimoPingEm = Agora.AddSeconds(-20);

        var evento = GeofenceRegras.Avaliar(ag, 5000, 50, 5, Agora);

        Assert.Equal(EventoGeofence.ChegadaDescartada, evento);
        Assert.Null(ag.ChegadaEm);
        Assert.Null(ag.UltimoPingEm);
    }

    [Fact(DisplayName = "UT-07 Lacuna grande entre leituras reinicia a contagem de chegada")]
    public void Avaliar_Agendado_LacunaDePing()
    {
        var ag = Novo(StatusAgendamento.Agendado);
        ag.ChegadaEm = Agora.AddMinutes(-10);
        ag.UltimoPingEm = Agora - GeofenceRegras.LacunaDePing - TimeSpan.FromMinutes(1);

        var evento = GeofenceRegras.Avaliar(ag, 5, 50, 5, Agora);

        Assert.Equal(EventoGeofence.Nenhum, evento);
        Assert.Equal(Agora, ag.ChegadaEm);
        Assert.Equal(StatusAgendamento.Agendado, ag.Status);
    }

    [Fact(DisplayName = "UT-08 Leitura com precisão pior que o máximo é ignorada")]
    public void Avaliar_PrecisaoRuim()
    {
        var ag = Novo(StatusAgendamento.Agendado);

        var evento = GeofenceRegras.Avaliar(ag, 1, 50, 100000, Agora);

        Assert.Equal(EventoGeofence.Nenhum, evento);
        Assert.Null(ag.ChegadaEm);
    }

    [Fact(DisplayName = "UT-09 Em atendimento: sair do raio pausa; ficar dentro não muda nada")]
    public void Avaliar_EmAtendimento()
    {
        var ag = Novo(StatusAgendamento.EmAtendimento);

        Assert.Equal(EventoGeofence.Nenhum, GeofenceRegras.Avaliar(ag, 10, 50, 5, Agora));
        Assert.Equal(EventoGeofence.Pausou, GeofenceRegras.Avaliar(ag, 5000, 50, 5, Agora));
        Assert.Equal(StatusAgendamento.EmPausa, ag.Status);
        Assert.Equal(Agora, ag.InicioPausa);
    }

    [Fact(DisplayName = "UT-10 Em pausa: voltar ao raio retoma e acumula o tempo parado")]
    public void Avaliar_EmPausa_Retoma()
    {
        var ag = Novo(StatusAgendamento.EmPausa);
        ag.InicioPausa = Agora.AddMinutes(-10);

        var evento = GeofenceRegras.Avaliar(ag, 5, 50, 5, Agora);

        Assert.Equal(EventoGeofence.Retomou, evento);
        Assert.Equal(StatusAgendamento.EmAtendimento, ag.Status);
        Assert.Equal(10, ag.PausaAcumuladaMinutos, 3);
        Assert.Null(ag.InicioPausa);
    }

    [Fact(DisplayName = "UT-11 Em pausa: fora do raio por tempo demais conclui; antes do limite continua pausado")]
    public void Avaliar_EmPausa_ConcluiPorTempo()
    {
        var curta = Novo(StatusAgendamento.EmPausa);
        curta.InicioPausa = Agora.AddMinutes(-1);
        var longa = Novo(StatusAgendamento.EmPausa);
        longa.Inicio = Agora.AddHours(-2);
        longa.InicioPausa = Agora - GeofenceRegras.LimiteForaDoRaio - TimeSpan.FromMinutes(1);

        Assert.Equal(EventoGeofence.Nenhum, GeofenceRegras.Avaliar(curta, 5000, 50, 5, Agora));
        Assert.Equal(EventoGeofence.Concluiu, GeofenceRegras.Avaliar(longa, 5000, 50, 5, Agora));
        Assert.Equal(StatusAgendamento.Concluido, longa.Status);
    }

    [Theory(DisplayName = "UT-12 Status sem regra de geofence não geram evento")]
    [InlineData(StatusAgendamento.Cancelado)]
    [InlineData(StatusAgendamento.Concluido)]
    public void Avaliar_StatusFinais(StatusAgendamento status) =>
        Assert.Equal(EventoGeofence.Nenhum, GeofenceRegras.Avaliar(Novo(status), 5, 50, 5, Agora));

    [Fact(DisplayName = "UT-13 Concluir calcula a duração líquida descontando a pausa")]
    public void Concluir_DescontaPausa()
    {
        var ag = Novo(StatusAgendamento.EmAtendimento);
        ag.Inicio = Agora.AddMinutes(-60);
        ag.PausaAcumuladaMinutos = 15;

        GeofenceRegras.Concluir(ag, Agora);

        Assert.Equal(StatusAgendamento.Concluido, ag.Status);
        Assert.Equal(45, ag.DuracaoMinutos);
        Assert.Equal(Agora, ag.Fim);
        Assert.Equal(Agora, ag.DataConclusao);
    }

    [Fact(DisplayName = "UT-14 Concluir com fim anterior ao início usa o início e nunca dá duração negativa")]
    public void Concluir_FimAnteriorAoInicio()
    {
        var ag = Novo(StatusAgendamento.EmAtendimento);
        ag.Inicio = Agora;
        ag.PausaAcumuladaMinutos = 30;

        GeofenceRegras.Concluir(ag, Agora.AddMinutes(-5));

        Assert.Equal(Agora, ag.Fim);
        Assert.Equal(0, ag.DuracaoMinutos);
    }

    [Fact(DisplayName = "UT-15 Configurar aplica apenas valores válidos e mantém o restante")]
    public void Configurar_AplicaValoresPadrao()
    {
        var antes = (GeofenceRegras.ConfirmacaoChegada, GeofenceRegras.ToleranciaSaidaMetros, GeofenceRegras.LimiteForaDoRaio);

        GeofenceRegras.Configurar(new GeofenceSettings());

        Assert.Equal(antes.ConfirmacaoChegada, GeofenceRegras.ConfirmacaoChegada);
        Assert.Equal(antes.ToleranciaSaidaMetros, GeofenceRegras.ToleranciaSaidaMetros);
        Assert.Equal(antes.LimiteForaDoRaio, GeofenceRegras.LimiteForaDoRaio);
    }

    [Fact(DisplayName = "UT-16 Propriedades calculadas do agendamento refletem o estado do geofence")]
    public void Agendamento_PropriedadesCalculadas()
    {
        var aguardando = Novo(StatusAgendamento.Agendado);
        aguardando.ChegadaEm = DateTime.UtcNow.AddMinutes(-1);
        var emAtendimento = Novo(StatusAgendamento.EmAtendimento);
        emAtendimento.Inicio = DateTime.UtcNow.AddMinutes(-30);
        emAtendimento.PausaAcumuladaMinutos = 10;
        var emPausa = Novo(StatusAgendamento.EmPausa);
        emPausa.Inicio = DateTime.UtcNow.AddMinutes(-60);
        emPausa.InicioPausa = DateTime.UtcNow.AddMinutes(-20);

        Assert.NotNull(aguardando.ConfirmacaoChegadaEm);
        Assert.InRange(aguardando.NoLocalSegundos!.Value, 50, 120);
        Assert.NotNull(aguardando.ConfirmaEmSegundos);
        Assert.InRange(emAtendimento.TrabalhadoSegundos!.Value, 19 * 60, 21 * 60);
        Assert.InRange(emPausa.TrabalhadoSegundos!.Value, 39 * 60, 41 * 60);
        Assert.NotNull(emPausa.PausaConcluiEm);
        Assert.Null(Novo(StatusAgendamento.Concluido).TrabalhadoSegundos);
        Assert.Null(Novo(StatusAgendamento.Agendado).NoLocalSegundos);
    }

    [Fact(DisplayName = "UT-17 Períodos: conversão entre UTC e horário de Brasília")]
    public void PeriodoHelper_Conversoes()
    {
        var utc = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        var brasilia = PeriodoHelper.ParaBrasilia(utc);

        Assert.Equal(9, brasilia.Hour);
        Assert.Equal(utc, PeriodoHelper.ParaUtc(brasilia));
        Assert.Equal(9, PeriodoHelper.ParaBrasilia(utc.ToLocalTime()).Hour);
        Assert.Equal(9, PeriodoHelper.ParaBrasilia(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified)).Hour);
    }

    [Fact(DisplayName = "UT-18 Períodos: limites diário, semanal e mensal")]
    public void PeriodoHelper_Limites()
    {
        var diario = PeriodoHelper.Limites("diario", Agora);
        var semanal = PeriodoHelper.Limites("semanal", Agora);
        var mensal = PeriodoHelper.Limites("mensal", Agora);
        var padrao = PeriodoHelper.Limites(null, Agora);

        Assert.Equal(new DateTime(2026, 3, 18, 3, 0, 0), diario.Inicio);
        Assert.Equal(new DateTime(2026, 3, 19, 3, 0, 0), diario.Fim);
        Assert.Equal(new DateTime(2026, 3, 17, 3, 0, 0), diario.InicioAnterior);

        Assert.Equal(new DateTime(2026, 3, 15, 3, 0, 0), semanal.Inicio);
        Assert.Equal(new DateTime(2026, 3, 22, 3, 0, 0), semanal.Fim);
        Assert.Equal(new DateTime(2026, 3, 8, 3, 0, 0), semanal.InicioAnterior);

        Assert.Equal(new DateTime(2026, 3, 1, 3, 0, 0), mensal.Inicio);
        Assert.Equal(new DateTime(2026, 4, 1, 3, 0, 0), mensal.Fim);
        Assert.Equal(new DateTime(2026, 2, 1, 3, 0, 0), mensal.InicioAnterior);
        Assert.Equal(mensal, padrao);
    }

    [Fact(DisplayName = "UT-19 Janela de próximos dias vai de ontem até daqui a nove dias")]
    public void PeriodoHelper_JanelaProximos()
    {
        var (inicio, fim) = PeriodoHelper.JanelaProximos(Agora);

        Assert.Equal(new DateTime(2026, 3, 17, 3, 0, 0), inicio);
        Assert.Equal(new DateTime(2026, 3, 27, 3, 0, 0), fim);
    }

    [Theory(DisplayName = "UT-20 Plano: normalização e nome de exibição")]
    [InlineData("full", "Full", "Full")]
    [InlineData(" PROFISSIONAL ", "Profissional", "Profissional")]
    [InlineData("semplano", "SemPlano", "Sem assinatura")]
    [InlineData("qualquer coisa", "Basico", "Básico")]
    public void PlanoService_NormalizarENome(string entrada, string normalizado, string exibicao)
    {
        Assert.Equal(normalizado, PlanoService.Normalizar(entrada));
        Assert.Equal(exibicao, PlanoService.NomeExibicao(entrada));
    }

    [Fact(DisplayName = "UT-21 Plano: nome de exibição de vazio e nível de cada plano")]
    public void PlanoService_NivelEVazio()
    {
        Assert.Equal("Sem assinatura", PlanoService.NomeExibicao(null));
        Assert.Equal("Sem assinatura", PlanoService.NomeExibicao("  "));
        Assert.Equal(3, PlanoService.Nivel(PlanoService.Full));
        Assert.Equal(2, PlanoService.Nivel(PlanoService.Profissional));
        Assert.Equal(1, PlanoService.Nivel(PlanoService.Basico));
        Assert.Equal(0, PlanoService.Nivel(PlanoService.SemPlano));
    }

    [Fact(DisplayName = "UT-22 Plano efetivo considera plano, ativação e validade")]
    public void PlanoService_PlanoEfetivo()
    {
        Assert.Equal("SemPlano", PlanoService.ObterPlanoEfetivo(null));
        Assert.Equal("SemPlano", PlanoService.ObterPlanoEfetivo(new ApplicationUser { Plano = null, PlanoAtivo = true }));
        Assert.Equal("SemPlano", PlanoService.ObterPlanoEfetivo(new ApplicationUser { Plano = "Full", PlanoAtivo = false }));
        Assert.Equal("SemPlano", PlanoService.ObterPlanoEfetivo(
            new ApplicationUser { Plano = "Full", PlanoAtivo = true, PlanoExpiraEm = DateTime.UtcNow.AddDays(-1) }));
        Assert.Equal("Full", PlanoService.ObterPlanoEfetivo(
            new ApplicationUser { Plano = "Full", PlanoAtivo = true, PlanoExpiraEm = DateTime.UtcNow.AddDays(1) }));
        Assert.Equal("Profissional", PlanoService.ObterPlanoEfetivo(
            new ApplicationUser { Plano = "profissional", PlanoAtivo = true }));
    }

    [Fact(DisplayName = "UT-23 Senha provisória e hash: formato, verificação e slug")]
    public void SenhaHelper_Basico()
    {
        var senha = SenhaHelper.GerarSenhaProvisoria();
        var hash = SenhaHelper.GerarHash(senha);

        Assert.True(senha.Length >= 6);
        Assert.NotEqual(senha, hash);
        Assert.True(SenhaHelper.Verificar(senha, hash));
        Assert.False(SenhaHelper.Verificar(senha + "x", hash));
        Assert.False(SenhaHelper.Verificar(senha, null));
        Assert.Equal("joaodasilva", SenhaHelper.Slug("João da Silva"));
    }

    [Fact(DisplayName = "UT-24 Acesso: dono, admin e usuário sem identidade")]
    public void AcessoHelper_Regras()
    {
        var id = Guid.NewGuid();
        var dono = Principal((System.Security.Claims.ClaimTypes.NameIdentifier, id.ToString()));
        var admin = Principal((System.Security.Claims.ClaimTypes.Role, "Admin"));
        var anonimo = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());

        Assert.True(dono.EhDonoOuAdmin(id));
        Assert.True(dono.EhDonoOuAdmin(id.ToString()));
        Assert.False(dono.EhDonoOuAdmin(Guid.NewGuid()));
        Assert.False(dono.EhDonoOuAdmin("nao-e-guid"));
        Assert.False(dono.EhDonoOuAdmin(Guid.Empty));
        Assert.True(admin.EhDonoOuAdmin(Guid.NewGuid()));
        Assert.True(admin.EhAdmin());
        Assert.False(anonimo.EhDonoOuAdmin(id));
        Assert.Null(anonimo.ObterUsuarioId());
        Assert.Equal(403, ((Microsoft.AspNetCore.Mvc.ObjectResult)AcessoHelper.Negado()).StatusCode);
    }

    private static System.Security.Claims.ClaimsPrincipal Principal(params (string tipo, string valor)[] claims) =>
        new(new System.Security.Claims.ClaimsIdentity(
            claims.Select(c => new System.Security.Claims.Claim(c.tipo, c.valor)), "teste"));

    [Fact(DisplayName = "UT-25 Aviso por e-mail aos clientes só vai para quem tem e-mail secundário")]
    public async Task ContaPrestadorHelper_AvisaSoQuemTemEmail()
    {
        var fake = new FakeEmailService();
        var clientes = new List<Cliente>
        {
            new() { Nome = "Com Email", EmailSecundario = "  com@teste.local " },
            new() { Nome = "Sem Email", EmailSecundario = null },
            new() { Nome = "Vazio", EmailSecundario = "   " }
        };

        ContaPrestadorHelper.AvisarClientesPorEmail(fake, clientes, "Ana <b>Souza</b>");

        await AdminHelper.EsperarAsync(() => fake.Enviados.Count >= 1);
        var email = Assert.Single(fake.Enviados);
        Assert.Equal("com@teste.local", email.Para);
        Assert.Contains("Ana &lt;b&gt;Souza&lt;/b&gt;", email.CorpoHtml);
    }

    [Fact(DisplayName = "UT-26 Aviso por e-mail sem destinatários ou sem nome do prestador")]
    public async Task ContaPrestadorHelper_SemDestinatariosOuSemNome()
    {
        var fake = new FakeEmailService();

        ContaPrestadorHelper.AvisarClientesPorEmail(fake, new List<Cliente> { new() { Nome = "X" } }, "Ana");
        ContaPrestadorHelper.AvisarClientesPorEmail(fake,
            new List<Cliente> { new() { Nome = "Y", EmailSecundario = "y@teste.local" } }, null);

        await AdminHelper.EsperarAsync(() => fake.Enviados.Count >= 1);
        Assert.Contains("O profissional que atendia você", Assert.Single(fake.Enviados).CorpoHtml);
    }

    [Fact(DisplayName = "UT-27 Envio de e-mail com servidor inválido propaga o erro")]
    public async Task EmailService_ServidorInvalido_LancaExcecao()
    {
        var servico = new EmailService(Options.Create(new EmailSettings
        {
            SmtpServer = "127.0.0.1",
            SmtpPort = 1,
            SenderName = "DayPlannio",
            SenderEmail = "no-reply@teste.local",
            Username = "x",
            Password = "y"
        }));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            servico.SendEmailAsync("destino@teste.local", "Assunto", "<p>Olá</p>"));
    }

    [Fact(DisplayName = "UT-28 Relatório em PDF é gerado mesmo com cliente removido e serviço desconhecido")]
    public void RelatorioPdf_GeraArquivo()
    {
        var cliente = new Cliente { Id = Guid.NewGuid(), Nome = "Maria" };
        var servico = new TipoServico { Id = Guid.NewGuid(), Tipo = "Limpeza" };
        var agendamentos = new List<Agendamento>
        {
            new() { ClienteId = cliente.Id, TipoServicoId = servico.Id, DataHora = Agora, ValorCobrado = 100, CustoMaterial = 10 },
            new() { ClienteId = Guid.NewGuid(), TipoServicoId = Guid.NewGuid(), DataHora = Agora, ValorCobrado = 50,
                    ClienteEncerradoEm = Agora },
            new() { ClienteId = Guid.NewGuid(), TipoServicoId = Guid.NewGuid(), DataHora = Agora, ValorCobrado = 20 }
        };
        var registros = new List<Financeiro>
        {
            new() { Tipo = TipoFinanceiro.Entrada, Descricao = "Venda", Valor = 30, Data = Agora },
            new() { Tipo = TipoFinanceiro.Saida, Descricao = "Compra", Valor = 15, Data = Agora }
        };

        var pdf = new RelatorioPdfService().GerarRelatorioFinanceiro(
            "mensal", Agora.AddDays(-10), Agora, 170, 10, 30, 15, 200, 175, 3,
            agendamentos, registros, new List<Cliente> { cliente }, new List<TipoServico> { servico });

        Assert.True(pdf.Length > 500);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
