using DayPlannio.App.Models;
using SQLite;
using System.Diagnostics;

namespace DayPlannio.App.Helpers;

public class SQLiteDatabaseHelper
{
    readonly SQLiteAsyncConnection conexao;

    public SQLiteDatabaseHelper(string caminho_pro_db3)
    {
        conexao = new SQLiteAsyncConnection(caminho_pro_db3);
        conexao.CreateTableAsync<SessaoLocal>().Wait();
    }

    public async Task<int> Insert(SessaoLocal s)
    {
        var resultado = await conexao.InsertAsync(s);
        Debug.WriteLine($"[DB] Sessão salva — UserId: {s.UserId}, Acesso: {s.UltimoAcesso}");
        return resultado;
    }

    public Task<List<SessaoLocal>> GetAll()
    {
        return conexao.Table<SessaoLocal>().ToListAsync();
    }

    public Task<int> Delete(int id)
    {
        return conexao.Table<SessaoLocal>().DeleteAsync(s => s.Id == id);
    }
}