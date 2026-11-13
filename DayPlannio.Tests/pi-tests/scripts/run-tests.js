const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');

const ROOT = path.resolve(__dirname, '..');
const REPORTS = path.join(ROOT, 'reports');

function readJson(file) {
  try {
    return JSON.parse(fs.readFileSync(file, 'utf8'));
  } catch {
    return null;
  }
}

function runProcess(label, command, args, logFile) {
  return new Promise((resolve) => {
    const output = [];
    const child = spawn(command, args, {
      cwd: ROOT,
      env: process.env,
      windowsHide: true,
      shell: false,
    });

    child.stdout.on('data', (chunk) => {
      const value = chunk.toString();
      output.push(value);
      process.stdout.write(value);
    });

    child.stderr.on('data', (chunk) => {
      const value = chunk.toString();
      output.push(value);
      process.stderr.write(value);
    });

    child.on('error', (error) => {
      output.push(`\nFalha ao iniciar ${label}: ${error.message}\n`);
      resolve({ code: 1, output: output.join(''), spawnError: error.message });
    });

    child.on('close', (code) => {
      const combined = output.join('');
      fs.writeFileSync(logFile, combined, 'utf8');
      resolve({ code: code ?? 1, output: combined });
    });
  });
}

function normalizeJest(file, tool) {
  const data = readJson(file);
  if (!data || !Array.isArray(data.testResults)) return [];

  const records = [];
  for (const suite of data.testResults) {
    for (const test of (suite.assertionResults || [])) {
      const status = test.status === 'passed'
        ? 'passed'
        : test.status === 'pending' || test.status === 'todo' || test.status === 'disabled'
          ? 'skipped'
          : 'failed';

      records.push({
        tool,
        name: test.fullName || test.title || 'Teste sem título',
        file: suite.name ? path.relative(ROOT, suite.name) : '',
        status,
        durationMs: Number.isFinite(test.duration) ? test.duration : null,
        error: (test.failureMessages || []).join('\n'),
      });
    }
  }
  return records;
}


function normalizeCypress(file) {
  const data = readJson(file);
  if (!data || !Array.isArray(data.tests)) return [];

  return data.tests.map((t) => ({
    tool: 'Cypress',
    name: t.fullTitle,
    file: t.file || '',
    status: t.state === 'passed' ? 'passed' : t.state === 'failed' ? 'failed' : 'skipped',
    durationMs: Number.isFinite(t.duration) ? t.duration : null,
    error: t.error || '',
  }));
}


function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, (char) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
  })[char]);
}

function formatDuration(ms) {
  if (!Number.isFinite(ms)) return 'N/D';
  return ms < 1000 ? `${Math.round(ms)} ms` : `${(ms / 1000).toFixed(2)} s`;
}

function createHtml(records, suiteResults, startedAt, finishedAt) {
  const passed = records.filter((r) => r.status === 'passed').length;
  const failed = records.filter((r) => r.status === 'failed').length;
  const skipped = records.filter((r) => r.status === 'skipped').length;
  const total = records.length;
  const percent = total ? ((passed / total) * 100).toFixed(1) : '0.0';
  const totalDuration = finishedAt - startedAt;

  const rows = records.map((r) => `
    <tr>
      <td><span class="tool">${escapeHtml(r.tool)}</span></td>
      <td><strong>${escapeHtml(r.name)}</strong>${r.file ? `<small>${escapeHtml(r.file)}</small>` : ''}${r.error ? `<details><summary>Detalhes do erro</summary><pre>${escapeHtml(r.error)}</pre></details>` : ''}</td>
      <td>${formatDuration(r.durationMs)}</td>
      <td><span class="status ${r.status}">${r.status === 'passed' ? 'Aprovado' : r.status === 'failed' ? 'Falhou' : 'Ignorado'}</span></td>
    </tr>`).join('');

  const suites = suiteResults.map((s) => `
    <li><span>${escapeHtml(s.name)}</span><span class="${s.code === 0 ? 'ok' : 'bad'}">${s.code === 0 ? 'Concluída' : `Finalizada com código ${s.code}`}</span></li>
  `).join('');

  return `<!doctype html>
<html lang="pt-BR">
<head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Relatório de testes - DayPlannio</title>
<style>
:root{font-family:Inter,Segoe UI,Arial,sans-serif;color:#20252b;background:#f4f6f8}
*{box-sizing:border-box}body{margin:0;padding:32px}.wrap{max-width:1180px;margin:auto}
header{display:flex;justify-content:space-between;align-items:flex-start;gap:20px;margin-bottom:24px}
h1{font-size:27px;margin:0 0 7px}p,.muted{color:#68737d;margin:0}
.cards{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:12px;margin:22px 0}
.card,section{background:#fff;border:1px solid #e5e9ed;border-radius:13px;padding:18px}
.card span{display:block;color:#68737d;font-size:13px}.card strong{display:block;font-size:25px;margin-top:8px}
.progress{height:9px;background:#e9edef;border-radius:20px;overflow:hidden;margin-top:12px}.progress div{height:100%;background:#087a78;width:${percent}%}
section{margin-top:18px}h2{font-size:18px;margin:0 0 15px}
table{width:100%;border-collapse:collapse}th{text-align:left;color:#68737d;font-size:12px;text-transform:uppercase;letter-spacing:.04em}
th,td{padding:13px 10px;border-bottom:1px solid #edf0f2;vertical-align:top}td small{display:block;color:#77818a;margin-top:5px}
.tool{background:#eef5f5;color:#087a78;padding:5px 8px;border-radius:6px;font-size:12px;font-weight:600;white-space:nowrap}
.status{font-size:12px;font-weight:700;padding:5px 8px;border-radius:20px;white-space:nowrap}
.passed{background:#e8f6ee;color:#187343}.failed{background:#fff0ef;color:#b42318}.skipped{background:#f1f2f4;color:#626b73}
details{margin-top:8px;color:#b42318}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#fff7f6;padding:10px;border-radius:7px;font-size:12px}
ul{list-style:none;padding:0;margin:0}li{display:flex;justify-content:space-between;padding:9px 0;border-bottom:1px solid #edf0f2}.ok{color:#187343}.bad{color:#b42318}
footer{color:#77818a;font-size:12px;margin:18px 2px}
@media(max-width:800px){body{padding:16px}.cards{grid-template-columns:repeat(2,minmax(0,1fr))}header{display:block}table{font-size:13px}th,td{padding:10px 6px}}
</style></head>
<body><main class="wrap">
<header><div><h1>Relatório de testes — DayPlannio</h1><p>Execução unificada de Jest, Supertest e Cypress</p></div><div class="muted">Gerado em ${new Date(finishedAt).toLocaleString('pt-BR')}</div></header>
<div class="cards">
<div class="card"><span>Total de testes</span><strong>${total}</strong></div>
<div class="card"><span>Aprovados</span><strong>${passed}</strong></div>
<div class="card"><span>Falharam</span><strong>${failed}</strong></div>
<div class="card"><span>Ignorados</span><strong>${skipped}</strong></div>
<div class="card"><span>Sucesso</span><strong>${percent}%</strong><div class="progress"><div></div></div></div>
</div>
<section><h2>Resultado por suíte</h2><ul>${suites}</ul></section>
<section><h2>Detalhamento individual</h2>
${records.length ? `<table><thead><tr><th>Ferramenta</th><th>Teste / funcionalidade</th><th>Duração</th><th>Resultado</th></tr></thead><tbody>${rows}</tbody></table>` : '<p>Nenhum resultado individual foi encontrado nos arquivos de saída. Consulte os logs em reports/.</p>'}
</section>
<footer>Tempo total de execução: ${formatDuration(totalDuration)}. A porcentagem considera testes aprovados sobre o total de testes registrados.</footer>
</main></body></html>`;
}

async function openReport(file) {
  let command;
  let args;
  if (process.platform === 'win32') {
    command = 'cmd';
    args = ['/c', 'start', '', file];
  } else if (process.platform === 'darwin') {
    command = 'open';
    args = [file];
  } else {
    command = 'xdg-open';
    args = [file];
  }

  try {
    const opener = spawn(command, args, { detached: true, stdio: 'ignore' });
    opener.on('error', () => {});
    opener.unref();
  } catch {
    console.log(`Não foi possível abrir automaticamente. Abra: ${file}`);
  }
}

async function main() {
  fs.mkdirSync(REPORTS, { recursive: true });
  const startedAt = Date.now();

  const jestJson = path.join(REPORTS, 'jest-unit.json');
  const apiJson = path.join(REPORTS, 'jest-api.json');
  const cypressJson = path.join(REPORTS, 'cypress.json');

  for (const file of [jestJson, apiJson, cypressJson]) {
    try { fs.rmSync(file); } catch {}
  }

  const jestCli = path.join(ROOT, 'node_modules', 'jest', 'bin', 'jest.js');

  const suites = [
    {
      name: 'Jest — testes unitários',
      json: jestJson,
      log: path.join(REPORTS, 'jest-unit.log'),
      command: process.execPath,
      args: [jestCli, 'tests/unit', '--runInBand', '--json', `--outputFile=${jestJson}`],
      parse: () => normalizeJest(jestJson, 'Jest'),
    },
    {
      name: 'Supertest — testes de API',
      json: apiJson,
      log: path.join(REPORTS, 'supertest.log'),
      command: process.execPath,
      args: [jestCli, 'tests/api', '--runInBand', '--json', `--outputFile=${apiJson}`],
      parse: () => normalizeJest(apiJson, 'Supertest'),
    },
    {
      name: 'Cypress — testes de interface',
      json: cypressJson,
      log: path.join(REPORTS, 'cypress.log'),
      command: process.execPath,
      args: [path.join(__dirname, 'cypress-runner.js'), cypressJson],
      parse: () => normalizeCypress(cypressJson),
    },
  ];

  const records = [];
  const suiteResults = [];

  for (const suite of suites) {
    console.log(`\n\n========== ${suite.name} ==========`);
    const result = await runProcess(suite.name, suite.command, suite.args, suite.log);
    suiteResults.push({ name: suite.name, code: result.code });
    const parsed = suite.parse();
    records.push(...parsed);

    if (parsed.length === 0) {
      records.push({
        tool: suite.name.split(' ')[0],
        name: `Não foi possível obter os resultados individuais da suíte`,
        file: '',
        status: 'failed',
        durationMs: null,
        error: result.spawnError || `A suíte terminou com código ${result.code} e não gerou resultados individuais. Consulte ${path.relative(ROOT, suite.log)}.`,
      });
    }
  }

  const finishedAt = Date.now();
  const html = createHtml(records, suiteResults, startedAt, finishedAt);
  const reportPath = path.join(REPORTS, 'relatorio.html');
  fs.writeFileSync(reportPath, html, 'utf8');

  const passed = records.filter((r) => r.status === 'passed').length;
  const failed = records.filter((r) => r.status === 'failed').length;
  const skipped = records.filter((r) => r.status === 'skipped').length;
  const total = records.length;
  const percent = total ? ((passed / total) * 100).toFixed(1) : '0.0';

  console.log('\n========== RESUMO ==========');
  console.log(`Testes registrados: ${total}`);
  console.log(`Aprovados: ${passed} | Falharam: ${failed} | Ignorados: ${skipped}`);
  console.log(`Percentual de sucesso: ${percent}%`);
  console.log(`Relatório: ${reportPath}`);
  console.log('As três suítes foram tentadas, mesmo que alguma tenha falhado.');

  await openReport(reportPath);
  process.exitCode = suiteResults.some((s) => s.code !== 0) || failed > 0 ? 1 : 0;
}

main().catch((error) => {
  console.error('Erro no executor de testes:', error);
  process.exitCode = 1;
});