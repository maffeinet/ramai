export function parseTestResult(run) {
  if (run.error || run.status !== 0) throw new Error(`Conformance failed: ${run.error ?? run.status}`);
  const count = name => {
    const match = new RegExp(`^# ${name} (\\d+)$`, 'm').exec(run.stdout);
    if (!match) throw new Error(`Missing test runner count: ${name}`);
    return Number(match[1]);
  };
  const result = Object.fromEntries(['tests', 'pass', 'fail', 'skipped', 'cancelled', 'todo'].map(name => [name, count(name)]));
  if (!result.tests || result.tests !== result.pass || result.fail || result.skipped || result.cancelled || result.todo)
    throw new Error('Incomplete or non-green conformance test run');
  return { tests: result.tests, passed: result.pass, failed: result.fail, skipped: result.skipped };
}
