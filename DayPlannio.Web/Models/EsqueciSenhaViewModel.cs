namespace DayPlannio.Web.Models;

public class EsqueciSenhaViewModel
{
    public int Passo { get; set; } = 1;
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string SenhaNova { get; set; } = string.Empty;
    public string SenhaNovaConfirmacao { get; set; } = string.Empty;
    public string? Mensagem { get; set; }
    public string? Erro { get; set; }

    public string EmailMascarado
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Email))
                return Email;

            int arroba = Email.IndexOf('@');
            if (arroba <= 1)
                return Email;

            var nome = Email.Substring(0, arroba);
            var dominio = Email.Substring(arroba + 1);

            if (nome.Length <= 2)
                return nome + "@" + dominio;

            var primeiro = nome.Substring(0, 2);
            var ultimo = nome.Substring(nome.Length - 1);
            return primeiro + new string('*', nome.Length - 3) + ultimo + "@" + dominio;
        }
    }
}
