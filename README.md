## Tecnologias

O DayPlannio é um sistema de agendamento e gestão para prestadores de serviço, dividido em três projetos em **.NET 10** (C#).

### API
- ASP.NET Core Web API
- MongoDB (MongoDB Atlas)
- ASP.NET Core Identity para autenticação
- Azure Blob Storage para armazenar as fotos
- Envio de e-mails via SMTP (Gmail)
- Geração de relatórios em PDF

### Web
- ASP.NET Core MVC (Razor)
- Bootstrap e jQuery
- Chart.js para os gráficos

### App
- .NET MAUI (XAML), padrão MVVM
- SQLite para armazenamento local

### Infraestrutura e nuvem
- **Microsoft Azure** (região Canada Central)
  - Máquina virtual Linux (Ubuntu Server 24.04 LTS) hospedando a API e o Web
  - Azure Blob Storage
  - IP público estático e nome DNS
- **MongoDB Atlas** (banco de dados na nuvem)
- **Nginx** como proxy reverso
- **systemd** para manter a API e o Web em execução como serviços
- Script de deploy (`deploy/vm-setup.sh`)
- Git e GitHub para versionamento

## Como rodar

O DayPlannio tem três partes: **API**, **Web** e **App**. A API e o Web já estão publicados em uma VM no Azure, então para testar basta rodar o App.

### Portal Web (já no ar)

Acesse: **https://dayplannio.canadacentral.cloudapp.azure.com/**

- A API fica em `https://dayplannio.canadacentral.cloudapp.azure.com/api/`.
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
2. Abra o arquivo da solução da App (`.sln`) no Visual Studio:
   - Pasta: `DayPlannio.App/`
   - Arquivo: `DayPlannio.App.sln`
3. Aguarde a restauração dos pacotes NuGet.
4. Selecione o projeto do App como projeto de inicialização e escolha o destino (windows machine).
5. Clique em **Iniciar (▶)** ou pressione **F5**.

> Na primeira execução o build pode demorar alguns minutos.
