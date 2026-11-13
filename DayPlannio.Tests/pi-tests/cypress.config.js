const { defineConfig } = require('cypress');

module.exports = defineConfig({
  e2e: {
    // Pode sobrescrever pelo terminal com CYPRESS_baseUrl.
    baseUrl: 'https://dayplannio.canadacentral.cloudapp.azure.com',
    specPattern: 'cypress/e2e/**/*.cy.js',
    supportFile: false,
    viewportWidth: 1280,
    viewportHeight: 800,
    video: false,
  },
});
