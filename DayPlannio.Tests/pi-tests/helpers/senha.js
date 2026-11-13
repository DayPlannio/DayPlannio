// Mesmos requisitos exibidos na tela "Trocar Senha" do portal
// (li[data-req=tamanho|maiusculo|minusculo|numero|especial]).
function requisitosDaSenha(senha) {
  return {
    tamanho: senha.length >= 6,
    maiusculo: /[A-Z]/.test(senha),
    minusculo: /[a-z]/.test(senha),
    numero: /\d/.test(senha),
    especial: /[^A-Za-z0-9]/.test(senha),
  };
}

function senhaEhValida(senha) {
  return Object.values(requisitosDaSenha(senha)).every(Boolean);
}

module.exports = { requisitosDaSenha, senhaEhValida };
