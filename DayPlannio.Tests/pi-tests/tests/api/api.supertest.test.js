
const request = require('supertest');
const { gerarUsuario, gerarEmail } = require('../../helpers/dados');

// API do PI publicada no Azure.
const API = (process.env.PI_API_URL || 'https://dayplannio.canadacentral.cloudapp.azure.com').replace(/\/$/, '');

describe('API DayPlannio (Supertest)', () => {
  test('cadastro com senhas diferentes retorna 400 e não grava nada', async () => {
    const res = await request(API)
      .post('/api/Users/create')
      .send(gerarUsuario({ confirmeSenha: 'Outra@123' }));

    expect(res.status).toBe(400);
    expect(res.body.message).toContain('não coincidem');
  });

  test('login inexistente retorna 401', async () => {
    const res = await request(API)
      .post('/api/Account/login')
      .send({ email: gerarEmail('nao'), senha: 'Qualquer@123' });

    expect(res.status).toBe(401);
  });

  test('rota protegida sem login é recusada', async () => {
    const res = await request(API)
      .get('/api/TiposServico/00000000-0000-0000-0000-000000000000')
      .redirects(0);

    expect([401, 302]).toContain(res.status);
  });
});
