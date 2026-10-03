using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DayPlannio.Api.Helpers
{
    public static class SenhaHelper
    {
        public static string GerarHash(string senha)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            using var pbkdf = new Rfc2898DeriveBytes(senha, salt, 100_000, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf.GetBytes(32);
            return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }

        public static bool Verificar(string senha, string? hashArmazenado)
        {
            if (string.IsNullOrWhiteSpace(hashArmazenado))
                return false;

            var parts = hashArmazenado.Split(':');
            if (parts.Length != 2)
                return false;

            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] hashOriginal = Convert.FromBase64String(parts[1]);

            using var pbkdf = new Rfc2898DeriveBytes(senha, salt, 100_000, HashAlgorithmName.SHA256);
            byte[] hashTeste = pbkdf.GetBytes(32);

            return CryptographicOperations.FixedTimeEquals(hashOriginal, hashTeste);
        }

        public static string GerarSenhaProvisoria(int tamanho = 10)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            using var rng = RandomNumberGenerator.Create();
            var buf = new byte[tamanho];
            rng.GetBytes(buf);
            return new string(buf.Select(b => chars[b % chars.Length]).ToArray());
        }

        public static string Slug(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return "cliente";

            var normalizado = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalizado)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    if (char.IsLetterOrDigit(c))
                        sb.Append(char.ToLowerInvariant(c));
                }
            }

            var resultado = sb.ToString();
            return string.IsNullOrWhiteSpace(resultado) ? "cliente" : resultado;
        }
    }
}
