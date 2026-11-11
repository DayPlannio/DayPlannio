<#
.SYNOPSIS
  Roda os testes (API, Web, App e, opcionalmente, E2E) e gera um relatorio HTML legivel
  com resultado por suite e COBERTURA DE CODIGO (%), sem precisar ler arquivos .trx.

.EXEMPLO
  .\gerar-relatorio.ps1
  .\gerar-relatorio.ps1 -MongoConnectionString "mongodb://127.0.0.1:27018"   # inclui o E2E

  Ao final abra:  relatorios\resumo.html
#>
param([string]$MongoConnectionString = "")

$ErrorActionPreference = "Continue"
Set-ExecutionPolicy -Scope Process Bypass -ErrorAction SilentlyContinue

$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source }
$env:Path = (Split-Path $dotnet) + ";" + $env:USERPROFILE + "\.dotnet\tools;" + $env:Path

$raiz  = $PSScriptRoot
$saida = Join-Path $raiz "relatorios"
if (Test-Path $saida) { Remove-Item $saida -Recurse -Force }
New-Item -ItemType Directory -Force $saida | Out-Null

Write-Host ">> Ferramentas de cobertura (so baixa na 1a vez)" -ForegroundColor Cyan
& dotnet tool update -g dotnet-coverage | Out-Null
& dotnet tool update -g dotnet-reportgenerator-globaltool | Out-Null

$suites = @(
    @{ Id = "api"; Nome = "Integra&#231;&#227;o da API";        Desc = "login, cadastro, agenda, financeiro e seguran&#231;a com o banco de dados"; Proj = "DayPlannio.Api.IntegrationTests" },
    @{ Id = "web"; Nome = "Integra&#231;&#227;o do portal Web"; Desc = "telas do portal conversando com a API"; Proj = "DayPlannio.Web.IntegrationTests" },
    @{ Id = "app"; Nome = "App do prestador";         Desc = "regras do aplicativo, sem as telas"; Proj = "DayPlannio.App.Tests" }
)

$linhas = @()

function LerTrx($caminho) {
    [xml]$x = Get-Content $caminho -Encoding UTF8
    $c = $x.TestRun.ResultSummary.Counters
    return @{ Total = [int]$c.executed; Ok = [int]$c.passed; Falha = [int]$c.failed }
}

foreach ($s in $suites) {
    Write-Host ">> $($s.Nome)" -ForegroundColor Cyan
    $proj = Join-Path $raiz $s.Proj
    $res  = Join-Path $saida $s.Id
    $cov  = Join-Path $saida ("cobertura-" + $s.Id + ".cobertura.xml")
    $cmd  = "dotnet test `"$proj`" --results-directory `"$res`" --logger `"trx;LogFileName=$($s.Id).trx`" --logger `"html;LogFileName=$($s.Id).html`""
    & dotnet-coverage collect -f cobertura -o $cov $cmd
    $trx = Join-Path $res ($s.Id + ".trx")
    if (Test-Path $trx) {
        $r = LerTrx $trx
        $linhas += [pscustomobject]@{ Suite = $s.Nome; Desc = $s.Desc; Total = $r.Total; Passou = $r.Ok; Falhou = $r.Falha; Detalhe = "$($s.Id)/$($s.Id).html" }
    }
}

if ($MongoConnectionString -ne "") {
    Write-Host ">> E2E (navegador)" -ForegroundColor Cyan
    & (Join-Path $raiz "executar-e2e.ps1") -MongoConnectionString $MongoConnectionString
    $origem = Join-Path $raiz "DayPlannio.E2ETests\TestResults"
    $dest = Join-Path $saida "e2e"
    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item "$origem\e2e.*" $dest -ErrorAction SilentlyContinue
    if (Test-Path "$dest\e2e.trx") {
        $r = LerTrx "$dest\e2e.trx"
        $linhas += [pscustomobject]@{ Suite = "Jornada no navegador (E2E)"; Desc = "um cliente usando o portal de ponta a ponta"; Total = $r.Total; Passou = $r.Ok; Falhou = $r.Falha; Detalhe = "e2e/e2e.html" }
    }
}

Write-Host ">> Cobertura de codigo" -ForegroundColor Cyan
$filtros = "+DayPlannio.Api.*;+DayPlannio.Web.*;+DayPlannio.App.*;-*Tests*;-*Migrations*"
& reportgenerator "-reports:$saida\*.cobertura.xml" "-targetdir:$saida\cobertura" "-reporttypes:Html;TextSummary" "-classfilters:$filtros" | Out-Null
$resumoCob = ""
if (Test-Path "$saida\cobertura\Summary.txt") { $resumoCob = Get-Content "$saida\cobertura\Summary.txt" -Raw -Encoding UTF8 }

# ---- numeros de cobertura (lidos do index.html do ReportGenerator)
$ci  = [Globalization.CultureInfo]::GetCultureInfo("pt-BR")
$inv = [Globalization.CultureInfo]::InvariantCulture
function N0($v)  { ([double]$v).ToString("N0", $ci) }
function P1($v)  { ([double]$v).ToString("N1", $ci) }
function Css($v) { ([double]$v).ToString("0.0", $inv) }
function Piso1($v) { [math]::Floor([double]$v * 10 + 0.000001) / 10 }   # a ferramenta trunca a 1a casa decimal
function Faixa($p) { if ($p -lt 40) { "baixa" } elseif ($p -lt 70) { "m&eacute;dia" } else { "boa" } }

$idx = ""
if (Test-Path "$saida\cobertura\index.html") { $idx = Get-Content "$saida\cobertura\index.html" -Raw -Encoding UTF8 }
$asm = @{}
foreach ($m in [regex]::Matches($idx, '<tr><th>(DayPlannio\.[\w.]+)</th><th class="right">(\d+)</th><th class="right">(\d+)</th><th class="right">(\d+)</th>')) {
    $asm[$m.Groups[1].Value] = @{ Cob = [int]$m.Groups[2].Value; Exec = [int]$m.Groups[4].Value }
}
$classes = @()
foreach ($m in [regex]::Matches($idx, '<tr><td><a href="[^"]*">(DayPlannio\.[\w.]+)</a></td><td class="right">(\d+)</td><td class="right">(\d+)</td><td class="right">(\d+)</td>')) {
    $classes += [pscustomobject]@{ Nome = $m.Groups[1].Value; Cob = [int]$m.Groups[2].Value; Nao = [int]$m.Groups[3].Value; Exec = [int]$m.Groups[4].Value }
}

$camadas = @(
    @{ Chave = "DayPlannio.Api";       Nome = "API";        Prefixo = "DayPlannio.Api.";  Desc = "regras de neg&oacute;cio, login, agenda e financeiro" },
    @{ Chave = "DayPlannio.Web";       Nome = "Web";        Prefixo = "DayPlannio.Web.";  Desc = "portal do cliente e do administrador" },
    @{ Chave = "DayPlannio.App.Tests"; Nome = "App (MAUI)"; Prefixo = "DayPlannio.App.";  Desc = "aplicativo do prestador (sem as telas)" }
)

$totExec = 0; $totCob = 0
foreach ($c in $camadas) { if ($asm.ContainsKey($c.Chave)) { $totExec += $asm[$c.Chave].Exec; $totCob += $asm[$c.Chave].Cob } }
if ($totExec -eq 0) { $totExec = 1 }
$totPct = 100.0 * $totCob / $totExec

$layerHtml = ""; $maior = $null
foreach ($c in $camadas) {
    if (-not $asm.ContainsKey($c.Chave)) { continue }
    $a = $asm[$c.Chave]
    $peso = 100.0 * $a.Exec / $totExec
    $cov  = Piso1 (100.0 * $a.Cob / [math]::Max($a.Exec, 1))
    $contrib = 100.0 * $a.Cob / $totExec
    if ($null -eq $maior -or $peso -gt $maior.Peso) { $maior = @{ Nome = $c.Nome; Peso = $peso; Cov = $cov; Contrib = $contrib } }
    $layerHtml += "<tr><td><b>$($c.Nome)</b><br><span class=""sub"">$($c.Desc)</span></td><td class=""num"">$(P1 $peso)%<br><span class=""sub"">$(N0 $a.Exec) linhas</span></td><td><div class=""barrow""><div class=""bar""><span style=""width:$(Css $cov)%""></span></div><b>$(P1 $cov)%</b></div><span class=""tag"">cobertura $(Faixa $cov)</span></td><td class=""num"">$(P1 $contrib) pontos</td></tr>`n"
}
$exemplo = ""
if ($null -ne $maior) { $exemplo = "a $($maior.Nome) &eacute; $(P1 $maior.Peso)% de todo o c&oacute;digo e est&aacute; $(P1 $maior.Cov)% coberta, ent&atilde;o ela soma $(P1 $maior.Contrib) dos $(P1 $totPct) pontos da cobertura total." }

$faltaHtml = ""
foreach ($k in ($classes | Sort-Object Nao -Descending | Select-Object -First 8)) {
    $nomeCamada = ""
    foreach ($c in $camadas) { if ($k.Nome.StartsWith($c.Prefixo)) { $nomeCamada = $c.Nome } }
    $faltaHtml += "<tr><td>$($k.Nome.Substring(11))</td><td>$nomeCamada</td><td class=""num"">$(N0 $k.Nao)</td><td class=""num"">$(P1 (Piso1 (100.0 * $k.Cob / [math]::Max($k.Exec, 1))))%</td></tr>`n"
}

# ---- bloco "tudo certo / falhou" e tabela de suites
$totTestes = ($linhas | Measure-Object -Property Total  -Sum).Sum
$totOk     = ($linhas | Measure-Object -Property Passou -Sum).Sum
if ($totOk -eq $totTestes) {
    $banner = "<div class=""banner ok""><span class=""ic"">&#10004;</span><div>Todos os testes passaram<small>$totTestes de $totTestes testes aprovados</small></div></div>"
} else {
    $banner = "<div class=""banner fail""><span class=""ic"">&#10008;</span><div>$($totTestes - $totOk) teste(s) falharam<small>$totOk de $totTestes testes aprovados</small></div></div>"
}
$tab = ($linhas | ForEach-Object {
    $selo = if ($_.Falhou -eq 0) { "<span class=""badge ok"">Aprovado</span>" } else { "<span class=""badge fail"">$($_.Falhou) falharam</span>" }
    "<tr><td><b>$($_.Suite)</b><br><span class=""sub"">$($_.Desc)</span></td><td class=""num"">$($_.Total)</td><td>$selo</td><td><a href=""$($_.Detalhe)"">ver</a></td></tr>"
}) -join "`n"

# ---- resumo.html (modelo fixo + valores calculados acima)
$modelo = @'
<!doctype html>
<html lang="pt-BR"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>DayPlannio - Resultado dos testes</title>
<style>
:root{--teal:#006260;--dark:#00504E;--soft:#C8E9E7;--track:#E0E3E2;--txt:#1d2524;--mut:#5b6766;--ok:#1b7f3b;--okbg:#e3f4e8;--bad:#BA1A1A;--badbg:#FFDAD6}
*{box-sizing:border-box}
body{font-family:"Segoe UI",Arial,sans-serif;margin:0;background:#f4f7f6;color:var(--txt);line-height:1.5}
.wrap{max-width:920px;margin:0 auto;padding:28px 20px 48px}
header h1{margin:0;color:var(--dark);font-size:28px}
header p{margin:4px 0 0;color:var(--mut)}
section{background:#fff;border-radius:12px;padding:22px 24px;margin-top:20px;box-shadow:0 1px 3px rgba(0,0,0,.08)}
h2{margin:0 0 4px;font-size:20px;color:var(--dark)}
.lead{margin:0 0 16px;color:var(--mut)}
.banner{border-radius:12px;padding:18px 24px;margin-top:20px;font-size:20px;font-weight:600;display:flex;gap:14px;align-items:center}
.banner small{display:block;font-weight:400;font-size:15px;margin-top:2px}
.banner.ok{background:var(--okbg);color:var(--ok);border:1px solid #b8dfc4}
.banner.fail{background:var(--badbg);color:var(--bad);border:1px solid #f2b3ac}
.banner .ic{font-size:30px}
table{border-collapse:collapse;width:100%}
th{font-size:13px;text-transform:uppercase;letter-spacing:.03em;color:var(--mut);text-align:left;border-bottom:2px solid var(--track);padding:8px 10px}
td{padding:12px 10px;border-bottom:1px solid var(--track);vertical-align:middle}
tr:last-child td{border-bottom:0}
.sub{color:var(--mut);font-size:13px}
.num{text-align:right;font-variant-numeric:tabular-nums;white-space:nowrap}
th.num{text-align:right}
.badge{display:inline-block;padding:2px 12px;border-radius:99px;font-size:13px;font-weight:600}
.badge.ok{background:var(--okbg);color:var(--ok)}
.badge.fail{background:var(--badbg);color:var(--bad)}
.big{display:flex;align-items:baseline;gap:14px;flex-wrap:wrap;margin:6px 0 8px}
.big .n{font-size:54px;font-weight:700;color:var(--teal);line-height:1}
.big .d{color:var(--mut)}
.bar{background:var(--track);border-radius:99px;height:14px;overflow:hidden;min-width:120px}
.bar span{display:block;height:100%;background:var(--teal);border-radius:99px}
.bar.big-bar{height:22px}
.barrow{display:flex;align-items:center;gap:10px}
.barrow .bar{flex:1}
.barrow b{min-width:52px;text-align:right}
.tag{font-size:12px;color:var(--mut)}
.note{background:#eef6f5;border-left:4px solid var(--teal);border-radius:6px;padding:10px 14px;margin:14px 0 0;font-size:14px}
details{margin-top:10px}
summary{cursor:pointer;color:var(--dark);font-weight:600}
dl{margin:8px 0 0}dt{font-weight:600;margin-top:8px}dd{margin:0;color:var(--mut)}
a{color:var(--teal)}
footer{margin-top:20px;color:var(--mut);font-size:13px}
@media(max-width:600px){.big .n{font-size:42px}th,td{padding:8px 6px}}
</style></head><body><div class="wrap">
<header><h1>DayPlannio &ndash; Resultado dos testes</h1><p>Gerado em @@DATA@@</p></header>

@@BANNER@@

<section>
<h2>1. O que foi testado</h2>
<p class="lead">Cada linha &eacute; um grupo de testes autom&aacute;ticos. &ldquo;Aprovado&rdquo; significa que todos os testes do grupo deram o resultado esperado.</p>
<table>
<tr><th>Grupo de testes</th><th class="num">Testes</th><th>Resultado</th><th>Detalhes</th></tr>
@@SUITES@@
</table>
</section>

<section>
<h2>2. Quanto do c&oacute;digo os testes cobrem</h2>
<p class="lead">&ldquo;Cobertura&rdquo; &eacute; a parte das linhas de c&oacute;digo que foram executadas por pelo menos um teste. Quanto maior, menos c&oacute;digo ficou sem ser verificado.</p>
<div class="big"><span class="n">@@TOTALPCT@@</span><span class="d">de 100% do c&oacute;digo do projeto<br>(@@TOTALDETAIL@@)</span></div>
<div class="bar big-bar"><span style="width:@@TOTALCSS@@%"></span></div>

<h3 style="margin:26px 0 4px;font-size:16px;color:var(--dark)">Por camada do sistema</h3>
<p class="lead" style="margin-bottom:8px">O <b>tamanho</b> mostra quanto do c&oacute;digo total a camada representa. A <b>cobertura</b> mostra quanto dela os testes executam. A <b>contribui&ccedil;&atilde;o</b> &eacute; quanto ela soma na cobertura total.</p>
<table>
<tr><th>Camada</th><th class="num">Tamanho no projeto</th><th>Cobertura da camada</th><th class="num">Contribui&ccedil;&atilde;o no total</th></tr>
@@LAYERS@@
<tr><td><b>Projeto inteiro</b></td><td class="num"><b>100%</b><br><span class="sub">@@TOTALEXEC@@ linhas</span></td><td><div class="barrow"><div class="bar"><span style="width:@@TOTALCSS@@%"></span></div><b>@@TOTALPCT@@</b></div></td><td class="num"><b>@@TOTALPCT@@</b></td></tr>
</table>
<p class="tag" style="margin-top:8px">Refer&ecirc;ncia de leitura: abaixo de 40% = baixa &middot; de 40% a 69% = m&eacute;dia &middot; 70% ou mais = boa.</p>
<div class="note"><b>Exemplo de leitura:</b> @@EXEMPLO@@</div>
<p class="sub" style="margin-top:12px">A cobertura vem dos testes da API, do Web e do App. Os testes de navegador (E2E) rodam em processos separados e n&atilde;o entram nesta conta, ent&atilde;o a cobertura real do portal &eacute; maior do que a mostrada.</p>
</section>

<section>
<h2>3. Onde mais faltam testes</h2>
<p class="lead">As 8 classes com mais linhas sem nenhum teste. &Eacute; por elas que vale come&ccedil;ar para aumentar a cobertura.</p>
<table>
<tr><th>Classe</th><th>Camada</th><th class="num">Linhas sem teste</th><th class="num">Cobertura</th></tr>
@@FALTA@@
</table>
</section>

<section>
<h2>4. Relat&oacute;rios completos</h2>
<ul>
<li><a href="cobertura/index.html">Cobertura por classe e por arquivo</a>, com as linhas n&atilde;o testadas marcadas em vermelho.</li>
<li>Detalhe de cada teste: use os links &ldquo;ver&rdquo; da tabela da se&ccedil;&atilde;o 1.</li>
</ul>
<details><summary>Gloss&aacute;rio r&aacute;pido</summary><dl>
<dt>Integra&ccedil;&atilde;o</dt><dd>Testa partes do sistema trabalhando juntas (por exemplo, a API com o banco de dados).</dd>
<dt>E2E (ponta a ponta)</dt><dd>Um navegador de verdade usa o portal como um cliente usaria.</dd>
<dt>Linha coberta</dt><dd>Linha de c&oacute;digo que rodou durante algum teste.</dd>
<dt>Camada</dt><dd>API (regras e banco), Web (portal) e App (aplicativo do prestador).</dd>
</dl></details>
</section>
<footer>Gerado automaticamente por gerar-relatorio.ps1.</footer>
</div></body></html>

'@
$html = $modelo.Replace("@@DATA@@", (Get-Date -Format "dd/MM/yyyy HH:mm")).Replace("@@BANNER@@", $banner).Replace("@@SUITES@@", $tab)
$html = $html.Replace("@@TOTALPCT@@", (([math]::Round($totPct)).ToString() + "%")).Replace("@@TOTALDETAIL@@", "$(N0 $totCob) de $(N0 $totExec) linhas executadas pelos testes")
$html = $html.Replace("@@TOTALCSS@@", (Css $totPct)).Replace("@@TOTALEXEC@@", (N0 $totExec))
$html = $html.Replace("@@LAYERS@@", $layerHtml).Replace("@@FALTA@@", $faltaHtml).Replace("@@EXEMPLO@@", $exemplo)
Set-Content "$saida\resumo.html" $html -Encoding UTF8

Write-Host ""
$linhas | Format-Table Suite, Total, Passou, Falhou -AutoSize
if ($resumoCob) { Write-Host $resumoCob }
Write-Host "Relatorio: $saida\resumo.html" -ForegroundColor Green
Start-Process "$saida\resumo.html"