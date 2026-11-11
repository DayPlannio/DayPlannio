<#
.SYNOPSIS
  Sobe API + Web + Azurite LOCALMENTE (banco de teste) e roda os testes E2E (Playwright).

.EXEMPLO
  .\executar-e2e.ps1 -MongoConnectionString "mongodb://localhost:27017"
  .\executar-e2e.ps1 -MongoConnectionString "mongodb://localhost:27017" -Visivel

  Use um MongoDB LOCAL (ou um banco de teste no Atlas). Os testes E2E criam dados:
  NÃO aponte para o banco de produção.
#>
param(
    [Parameter(Mandatory = $true)][string]$MongoConnectionString,
    [string]$Database = "dayplannio_e2e",
    [switch]$Visivel
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
$chave = "chave-e2e-local"
$procs = @()

# Caminho explícito do dotnet.exe (evita pegar outro "dotnet" que não é executável do Windows)
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source }
$env:Path = (Split-Path $dotnet) + ";" + $env:Path
$logs = "$PSScriptRoot\logs-e2e"
New-Item -ItemType Directory -Force $logs | Out-Null
Remove-Item "$logs\*" -Force -ErrorAction SilentlyContinue

# Falha rápido se o MongoDB não está de pé (sem isso cada teste espera ~30 s e dá 500)
$uriMongo = [Uri]($MongoConnectionString -replace '^mongodb(\+srv)?://', 'http://')
$porta = if ($uriMongo.Port -gt 0) { $uriMongo.Port } else { 27017 }
$tcp = New-Object Net.Sockets.TcpClient
try { $tcp.Connect($uriMongo.Host, $porta) }
catch { throw "MongoDB não respondeu em $($uriMongo.Host):$porta. Suba o Mongo antes de rodar o E2E." }
finally { $tcp.Dispose() }

# Chave pública e conhecida do emulador Azurite (não é segredo)
$azurite = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;"

function Esperar($url) {
    for ($i = 0; $i -lt 60; $i++) {
        try { Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 2 | Out-Null; return } catch {
            if ($_.Exception.Response) { return }   # respondeu (mesmo 401/404): está no ar
            Start-Sleep -Seconds 1
        }
    }
    throw "Timeout esperando $url"
}

try {
    # ---- variáveis de ambiente lidas pelo ASP.NET (sobrescrevem appsettings)
    $env:MongoConnection__ConnectionString = $MongoConnectionString
    $env:MongoConnection__Database         = $Database
    $env:MongoConnection__IsSSL            = "false"
    $env:WebApiKey                         = $chave
    $env:AzureBlobStorage__ConnectionString = $azurite
    $env:AdminSettings__Email              = "admin@e2e.local"
    $env:AdminSettings__Senha              = "Admin@12345"
    $env:Api__BaseUrl                      = "http://localhost:5143/"
    $env:Api__Key                          = $chave

    Write-Host ">> Azurite (emulador do Azure Blob)" -ForegroundColor Cyan
    if (-not (Get-Command npx.cmd -ErrorAction SilentlyContinue)) { throw "Node.js/npx não encontrado. Instale o Node.js (https://nodejs.org) para o Azurite." }
    $procs += Start-Process npx.cmd -ArgumentList "--yes azurite --silent --skipApiVersionCheck --blobHost 127.0.0.1 --location $env:TEMP\azurite-e2e" -PassThru -WindowStyle Hidden -RedirectStandardOutput "$logs\azurite.out.log" -RedirectStandardError "$logs\azurite.err.log"
    # Espera a porta do Blob (10000) abrir; na 1ª vez o npx baixa o Azurite e pode demorar
    $azOk = $false
    for ($i = 0; $i -lt 90; $i++) {
        $t = New-Object Net.Sockets.TcpClient
        try { $t.Connect("127.0.0.1", 10000); $azOk = $true; break } catch { Start-Sleep -Seconds 1 } finally { $t.Dispose() }
    }
    if (-not $azOk) {
        Write-Host "Azurite não subiu na porta 10000. Log:" -ForegroundColor Red
        Get-Content "$logs\azurite.err.log","$logs\azurite.out.log" -ErrorAction SilentlyContinue | Select-Object -Last 30
        throw "Azurite indisponível (a API precisa dele: AzureBlobStorageService cria o container no construtor)."
    }

    Write-Host ">> API (5143)" -ForegroundColor Cyan
    $procs += Start-Process $dotnet -ArgumentList "run --project `"$raiz\DayPlannio.Api\DayPlannio.Api\DayPlannio.Api.csproj`" --no-launch-profile --urls http://localhost:5143" -PassThru -WindowStyle Hidden -RedirectStandardOutput "$logs\api.out.log" -RedirectStandardError "$logs\api.err.log"
    Esperar "http://localhost:5143/api/Users"

    Write-Host ">> Web (5140)" -ForegroundColor Cyan
    $procs += Start-Process $dotnet -ArgumentList "run --project `"$raiz\DayPlannio.Web\DayPlannio.Web.csproj`" --no-launch-profile --urls http://localhost:5140" -PassThru -WindowStyle Hidden -RedirectStandardOutput "$logs\web.out.log" -RedirectStandardError "$logs\web.err.log"
    Esperar "http://localhost:5140/Account/Login"

    Write-Host ">> Instalando o Chromium do Playwright (só na 1ª vez)" -ForegroundColor Cyan
    $proj = "$PSScriptRoot\DayPlannio.E2ETests"
    dotnet build $proj | Out-Null
    $pw = Get-ChildItem "$proj\bin" -Recurse -Filter playwright.ps1 | Select-Object -First 1
    & powershell -File $pw.FullName install chromium

    $env:DP_WEB_URL = "http://localhost:5140"
    $env:DP_API_URL = "http://localhost:5143"
    $env:DP_ADMIN_EMAIL = "admin@e2e.local"
    $env:DP_ADMIN_SENHA = "Admin@12345"
    if ($Visivel) { $env:HEADED = "1"; $env:SLOWMO = "300" }

    Write-Host ">> Rodando os testes E2E" -ForegroundColor Cyan
    dotnet test $proj --no-build --logger "trx;LogFileName=e2e.trx" --logger "html;LogFileName=e2e.html" --logger "console;verbosity=normal"
    if ($LASTEXITCODE -ne 0) {
        foreach ($f in "azurite.err.log","api.err.log","api.out.log","web.err.log","web.out.log") {
            $p = "$logs\$f"
            if ((Test-Path $p) -and (Get-Item $p).Length -gt 0) {
                Write-Host "`n===== $f (últimas 60 linhas) =====" -ForegroundColor Yellow
                Get-Content $p -Tail 60
            }
        }
        Write-Host "`nLogs completos em: $logs" -ForegroundColor Yellow
    }
}
finally {
    Write-Host ">> Encerrando processos" -ForegroundColor Cyan
    foreach ($p in $procs) { if ($p -and -not $p.HasExited) { & taskkill /PID $p.Id /T /F 2>$null | Out-Null } }
}