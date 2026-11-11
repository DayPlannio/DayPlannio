using DayPlannio.App.Helpers;
using DayPlannio.App.Models;
using Xunit;

namespace DayPlannio.App.Tests.Tests;

[Trait("Categoria", "Integracao")]
public class SQLiteDatabaseHelperTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "dayplannio_testes_" + Guid.NewGuid().ToString("N"));
    private string Caminho => Path.Combine(_pasta, "sessao.db3");

    public SQLiteDatabaseHelperTests() => Directory.CreateDirectory(_pasta);

    public void Dispose()
    {
        try { Directory.Delete(_pasta, recursive: true); } catch { }
    }

    [Fact(DisplayName = "AI-01 Banco novo começa vazio (a tabela é criada no construtor)")]
    public async Task BancoNovo_ComecaVazio()
    {
        var db = new SQLiteDatabaseHelper(Caminho);

        Assert.Empty(await db.GetAll());
        Assert.True(File.Exists(Caminho));
    }

    [Fact(DisplayName = "AI-02 Inserir sessão gera Id automático e preserva os dados")]
    public async Task Insert_GeraIdEPreservaDados()
    {
        var db = new SQLiteDatabaseHelper(Caminho);
        var acesso = new DateTime(2026, 10, 28, 9, 30, 0);

        await db.Insert(new SessaoLocal { UserId = "user-1", UltimoAcesso = acesso });
        var todas = await db.GetAll();

        var s = Assert.Single(todas);
        Assert.True(s.Id > 0);
        Assert.Equal("user-1", s.UserId);
        Assert.Equal(acesso, s.UltimoAcesso);
    }

    [Fact(DisplayName = "AI-03 Excluir remove só a sessão informada")]
    public async Task Delete_RemoveSomenteAInformada()
    {
        var db = new SQLiteDatabaseHelper(Caminho);
        await db.Insert(new SessaoLocal { UserId = "a", UltimoAcesso = DateTime.Now });
        await db.Insert(new SessaoLocal { UserId = "b", UltimoAcesso = DateTime.Now });
        var primeira = (await db.GetAll()).Single(s => s.UserId == "a");

        await db.Delete(primeira.Id);

        Assert.Equal("b", Assert.Single(await db.GetAll()).UserId);
    }

    [Fact(DisplayName = "AI-04 Os dados persistem: um novo helper no mesmo arquivo enxerga a sessão (simula reabrir o app)")]
    public async Task Persistencia_ReabrindoOArquivo()
    {
        await new SQLiteDatabaseHelper(Caminho).Insert(new SessaoLocal { UserId = "persistente", UltimoAcesso = DateTime.Now });

        var reaberto = new SQLiteDatabaseHelper(Caminho);

        Assert.Equal("persistente", Assert.Single(await reaberto.GetAll()).UserId);
    }
}
