
// Gera dados dinâmicos para os testes não duplicarem cadastros no banco real.
const SENHA_VALIDA = 'Senha@123';

let contadorEmail = 0;

function gerarEmail(prefixo = 'pi') {
  contadorEmail++;

  return `${prefixo}_${Date.now()}_${contadorEmail}@teste.local`;
}

function gerarUsuario(sobrescrever = {}) {
  const senha = sobrescrever.senha ?? SENHA_VALIDA;

  return {
    nomeCompleto: `Teste PI ${Date.now()}`.slice(0, 20),
    email: gerarEmail(),
    senha,
    confirmeSenha: senha,
    aceitouPolitica: true,
    usarTesteGratuito: true,
    ...sobrescrever,
  };
}

module.exports = { SENHA_VALIDA, gerarEmail, gerarUsuario };
