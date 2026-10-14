## Como rodar

O DayPlannio tem três partes: **API**, **Web** e **App**. A API e o Web já estão publicados em uma VM no Azure, então para testar basta rodar o App.

### Portal Web (já no ar)

Acesse: **http://dayplannio.canadacentral.cloudapp.azure.com/**

- A API fica em `http://dayplannio.canadacentral.cloudapp.azure.com/api/`.
- O endereço usa HTTP (sem certificado). Use apenas senhas de teste.

### Rodar o App

**Pré-requisitos**

- Windows com **Visual Studio 2026** e a carga de trabalho **Desenvolvimento de aplicativos móveis com .NET** (MAUI) instalada
- **.NET 10 SDK**

**Passos**

1. Clone o repositório:
```bash
   git clone https://github.com/DayPlannio/DayPlannio.git
```
2. Abra o arquivo da solução da App (`.slnx`) no Visual Studio:
   - Pasta: `DayPlannio.App/`
   - Arquivo: `DayPlannio.App.slnx`
3. Aguarde a restauração dos pacotes NuGet.
4. Selecione o projeto do App como projeto de inicialização e escolha o destino (emulador Android ou dispositivo).
5. Clique em **Iniciar (▶)** ou pressione **F5**.

> Na primeira execução o build pode demorar alguns minutos.
