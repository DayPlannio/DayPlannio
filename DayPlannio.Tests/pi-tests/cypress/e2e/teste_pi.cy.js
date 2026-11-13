describe('Teste de Fluxo Crítico - Projeto Integrador (DayPlannio)', () => {
  const urlLogin = /\/(Account\/)?Login(\?|$)/i;

  it('Login com credenciais inválidas mostra erro e permanece no login', () => {
    cy.visit('/Account/Login');

    cy.get('#Email').type(`ninguem${Date.now()}@teste.local`);
    cy.get('input[name=Senha]').type('Errada@123');
    cy.contains('button', 'Entrar').click();

    cy.get('.alert-danger').should('contain', 'Credenciais inválidas');
    cy.url().should('match', urlLogin);
  });

  it('Áreas restritas redirecionam para o login quando não há sessão', () => {
    ['/Metricas', '/Account/Perfil', '/Admin/Painel'].forEach((rota) => {
      cy.visit(rota);
      cy.url().should('match', urlLogin);
    });
  });
});
