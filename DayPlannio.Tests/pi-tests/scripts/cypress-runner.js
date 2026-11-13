const cypress = require('cypress');
const fs = require('fs');

const out = process.argv[2];

function firstNumber(...values) {
  for (const v of values) {
    if (typeof v === 'number' && Number.isFinite(v)) return v;
  }
  return null;
}

function sumTimings(timings) {
  if (!timings || typeof timings !== 'object') return null;
  let total = 0;
  let found = false;
  for (const v of Object.values(timings)) {
    const n = Array.isArray(v)
      ? v.reduce((a, b) => a + (Number(b?.wallClockDuration ?? b?.duration) || 0), 0)
      : Number(v?.wallClockDuration ?? v?.duration ?? v);
    if (Number.isFinite(n) && n > 0) { total += n; found = true; }
  }
  return found ? total : null;
}

cypress.run({ browser: 'electron', headless: true })
  .then((res) => {
    if (res.status === 'failed') {
      console.error(res.message);
      process.exit(1);
    }

    const tests = [];
    const rawTests = [];

    for (const run of res.runs) {
      for (const t of run.tests) {
        const attempts = t.attempts || [];
        const last = attempts[attempts.length - 1] || {};
        rawTests.push(t);

        tests.push({
          fullTitle: Array.isArray(t.title) ? t.title.join(' ') : String(t.title),
          file: run.spec.relative,
          state: t.state,
          duration: firstNumber(
            last.wallClockDuration,
            last.duration,
            t.wallClockDuration,
            t.duration,
            sumTimings(last.timings)
          ),
          error: last.error?.message || t.displayError || '',
        });
      }
    }

    fs.writeFileSync(out, JSON.stringify({ tests }, null, 2));
    // Estrutura bruta devolvida pelo Cypress, para depuração
    fs.writeFileSync(out.replace(/\.json$/, '-debug.json'), JSON.stringify(rawTests, null, 2));

    process.exit(res.totalFailed > 0 ? 1 : 0);
  })
  .catch((err) => {
    console.error(err);
    process.exit(1);
  });