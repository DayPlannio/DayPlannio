using System.ComponentModel;
using System.Text.RegularExpressions;
using DayPlannio.App.ViewModel;
using Xunit;

namespace DayPlannio.App.Tests.Tests;

[Trait("Categoria", "Unitario")]
public class AgendamentoItemViewModelTests
{
    private static AgendamentoItemViewModel Vm(string status = "AGENDADO") => new() { StatusTexto = status };

    private static string Hora(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm");

    [Fact(DisplayName = "AU-15 Atendimento concluído mostra início, término e duração")]
    public void Concluido_MostraInicioTerminoEDuracao()
    {
        var inicio = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
        var fim = inicio.AddMinutes(45);
        var vm = Vm("CONCLUÍDO");
        vm.Inicio = inicio; vm.Fim = fim; vm.DuracaoMinutos = 45;

        Assert.Equal($"Início {Hora(inicio)} • Término {Hora(fim)} • Duração 45min", vm.TempoTexto);
        Assert.True(vm.TemTempo);
    }

    [Fact(DisplayName = "AU-16 Sem horário de término, o texto omite 'Término'")]
    public void SemFim_OmiteTermino()
    {
        var vm = Vm("CONCLUÍDO");
        vm.Inicio = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc); vm.DuracaoMinutos = 30;

        Assert.DoesNotContain("Término", vm.TempoTexto);
        Assert.EndsWith("Duração 30min", vm.TempoTexto);
    }

    [Theory(DisplayName = "AU-17 Duração é formatada em minutos, horas e 'menos de 1 min'")]
    [InlineData(0.2, "menos de 1 min")]
    [InlineData(1, "1min")]
    [InlineData(45, "45min")]
    [InlineData(60, "1h")]
    [InlineData(90, "1h 30min")]
    [InlineData(125, "2h 5min")]
    public void Duracao_Formatacao(double minutos, string esperado)
    {
        var vm = Vm("CONCLUÍDO");
        vm.Inicio = DateTime.UtcNow.AddHours(-3); vm.DuracaoMinutos = minutos;

        Assert.EndsWith($"Duração {esperado}", vm.TempoTexto);
    }

    [Fact(DisplayName = "AU-18 Em atendimento usa o tempo trabalhado enviado pela API (hh:mm:ss)")]
    public void EmAtendimento_UsaTrabalhadoSegundos()
    {
        var vm = Vm("EM ATENDIMENTO");
        vm.Inicio = DateTime.UtcNow.AddHours(-2); vm.TrabalhadoSegundos = 3661;

        Assert.Contains("Em andamento 01:01:01", vm.TempoTexto);
    }

    [Fact(DisplayName = "AU-19 Em atendimento sem tempo da API calcula a partir do início")]
    public void EmAtendimento_CalculaDoInicio()
    {
        var vm = Vm("EM ATENDIMENTO");
        vm.Inicio = DateTime.UtcNow.AddMinutes(-10);

        Assert.Matches(new Regex(@"Em andamento 00:10:0[0-3]"), vm.TempoTexto);
    }

    [Fact(DisplayName = "AU-20 O tempo de pausa acumulada é descontado do tempo trabalhado")]
    public void EmAtendimento_DescontaPausaAcumulada()
    {
        var vm = Vm("EM ATENDIMENTO");
        vm.Inicio = DateTime.UtcNow.AddMinutes(-10); vm.PausaAcumuladaMinutos = 4;

        Assert.Matches(new Regex(@"Em andamento 00:06:0[0-3]"), vm.TempoTexto);
    }

    [Fact(DisplayName = "AU-21 O tempo trabalhado nunca fica negativo")]
    public void TempoTrabalhado_NaoFicaNegativo()
    {
        var vm = Vm("EM ATENDIMENTO");
        vm.Inicio = DateTime.UtcNow.AddHours(1);

        Assert.Contains("Em andamento 00:00:00", vm.TempoTexto);
    }

    [Fact(DisplayName = "AU-22 Em pausa mostra o tempo trabalhado congelado")]
    public void EmPausa_MostraTempoTrabalhado()
    {
        var vm = Vm("EM PAUSA");
        vm.Inicio = DateTime.UtcNow.AddHours(-1); vm.TrabalhadoSegundos = 600;

        Assert.Contains("Em pausa", vm.TempoTexto);
        Assert.Contains("Tempo trabalhado 00:10:00", vm.TempoTexto);
        Assert.DoesNotContain("conclui em", vm.TempoTexto);
    }

    [Fact(DisplayName = "AU-23 Em pausa com prazo futuro mostra a contagem regressiva para concluir sozinho")]
    public void EmPausa_ComPrazoFuturo_MostraContagem()
    {
        var vm = Vm("EM PAUSA");
        vm.Inicio = DateTime.UtcNow.AddHours(-1); vm.TrabalhadoSegundos = 600;
        vm.PausaConcluiEm = DateTime.UtcNow.AddMinutes(5);

        Assert.Matches(new Regex(@"conclui em 0[45]:\d\d se não voltar"), vm.TempoTexto);
    }

    [Fact(DisplayName = "AU-24 Em pausa com prazo vencido mostra 'concluindo...'")]
    public void EmPausa_PrazoVencido_MostraConcluindo()
    {
        var vm = Vm("EM PAUSA");
        vm.Inicio = DateTime.UtcNow.AddHours(-1); vm.TrabalhadoSegundos = 600;
        vm.PausaConcluiEm = DateTime.UtcNow.AddMinutes(-1);

        Assert.Contains("concluindo...", vm.TempoTexto);
    }

    [Fact(DisplayName = "AU-25 Agendado com chegada detectada mostra há quanto tempo está no local e quando confirma")]
    public void Agendado_ChegadaDetectada()
    {
        var vm = Vm("AGENDADO");
        vm.NoLocalSegundos = 60; vm.ConfirmaEmSegundos = 120;

        Assert.Equal("No local há 00:01:00 • confirma em 02:00", vm.TempoTexto);
    }

    [Fact(DisplayName = "AU-26 Chegada confirmada mostra 'iniciando...'")]
    public void Agendado_ChegadaConfirmada()
    {
        var vm = Vm("AGENDADO");
        vm.NoLocalSegundos = 400; vm.ConfirmaEmSegundos = 0;

        Assert.Equal("Chegada confirmada • iniciando...", vm.TempoTexto);
    }

    [Fact(DisplayName = "AU-27 Sem dados de tempo, o texto é vazio e 'TemTempo' é falso")]
    public void SemDados_TextoVazio()
    {
        var vm = Vm("AGENDADO");

        Assert.Equal(string.Empty, vm.TempoTexto);
        Assert.False(vm.TemTempo);
    }

    [Theory(DisplayName = "AU-28 Só agendamentos 'AGENDADO' podem ser editados")]
    [InlineData("AGENDADO", true)]
    [InlineData("EM ATENDIMENTO", false)]
    [InlineData("EM PAUSA", false)]
    [InlineData("CONCLUÍDO", false)]
    [InlineData("CANCELADO", false)]
    public void PodeEditar(string status, bool esperado) => Assert.Equal(esperado, Vm(status).PodeEditar);

    [Fact(DisplayName = "AU-29 Card em atendimento tem fundo destacado e borda; os demais não")]
    public void AparenciaDoCard()
    {
        var ativo = Vm("EM ATENDIMENTO");
        var normal = Vm("AGENDADO");

        Assert.True(ativo.EmAtendimento);
        Assert.Equal(Color.FromArgb("#FFF6E8"), ativo.CorFundoCard);
        Assert.Equal(1.5, ativo.EspessuraBorda);
        Assert.Equal(Color.FromArgb("#FAFAFA"), normal.CorFundoCard);
        Assert.Equal(0, normal.EspessuraBorda);
    }

    [Theory(DisplayName = "AU-30 O relógio só precisa rodar quando há algo se movendo na tela")]
    [InlineData("EM ATENDIMENTO", false, false, true)]
    [InlineData("EM PAUSA", true, false, true)]
    [InlineData("EM PAUSA", false, false, false)]
    [InlineData("AGENDADO", false, true, true)]
    [InlineData("AGENDADO", false, false, false)]
    [InlineData("CONCLUÍDO", false, false, false)]
    public void PrecisaRelogio(string status, bool temPrazoPausa, bool temConfirma, bool esperado)
    {
        var vm = Vm(status);
        if (temPrazoPausa) vm.PausaConcluiEm = DateTime.UtcNow.AddMinutes(5);
        if (temConfirma) vm.ConfirmaEmSegundos = 90;

        Assert.Equal(esperado, vm.PrecisaRelogio);
    }

    [Fact(DisplayName = "AU-31 Observações: vazio/espaços = sem observação")]
    public void TemObservacoes()
    {
        var vm = Vm();
        vm.Observacoes = "  ";
        Assert.False(vm.TemObservacoes);

        vm.Observacoes = "Levar escada";
        Assert.True(vm.TemObservacoes);
    }

    [Fact(DisplayName = "AU-32 AtualizarRelogio notifica a tela sobre TempoTexto e TemTempo")]
    public void AtualizarRelogio_DisparaNotificacoes()
    {
        var vm = Vm("EM ATENDIMENTO");
        var propriedades = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => propriedades.Add(e.PropertyName);

        vm.AtualizarRelogio();

        Assert.Equal(new[] { nameof(vm.TempoTexto), nameof(vm.TemTempo) }, propriedades);
    }
}
