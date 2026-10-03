namespace DayPlannio.App.Services;

public interface IExibeToast
{
    Task MostrarToastAsync(string titulo, string mensagem, bool sucesso);
}
