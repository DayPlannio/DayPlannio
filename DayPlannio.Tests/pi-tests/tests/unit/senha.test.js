const { requisitosDaSenha, senhaEhValida } = require('../../helpers/senha');
const { SENHA_VALIDA, gerarEmail, gerarUsuario } = require('../../helpers/dados');

describe('Requisitos de senha (tela Trocar Senha)', () => {
  test('"abc" atende só ao requisito de minúsculas', () => {
    const r = requisitosDaSenha('abc');
    expect(r.minusculo).toBe(true);
    expect(r.maiusculo).toBe(false);
    expect(r.numero).toBe(false);
    expect(r.especial).toBe(false);
    expect(senhaEhValida('abc')).toBe(false);
  });

  test('"Abc123!" atende a todos os requisitos', () => {
    expect(requisitosDaSenha('Abc123!')).toEqual({
      tamanho: true, maiusculo: true, minusculo: true, numero: true, especial: true,
    });
    expect(senhaEhValida('Abc123!')).toBe(true);
  });

  test('a senha padrão dos testes é válida', () => {
    expect(senhaEhValida(SENHA_VALIDA)).toBe(true);
  });
});

describe('Geração de dados dinâmicos', () => {
  test('e-mails gerados nunca se repetem', () => {
    const emails = new Set(Array.from({ length: 500 }, () => gerarEmail()));
    expect(emails.size).toBe(500);
  });

  test('usuário gerado é consistente e aceita sobrescrita', () => {
    const u = gerarUsuario();
    expect(u.email).toMatch(/^pi_\d+_\d+@teste\.local$/);
    expect(u.senha).toBe(u.confirmeSenha);
    expect(u.aceitouPolitica).toBe(true);

    const errado = gerarUsuario({ confirmeSenha: 'Outra@123' });
    expect(errado.senha).not.toBe(errado.confirmeSenha);
  });
});
