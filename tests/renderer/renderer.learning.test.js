const test = require('node:test');
const assert = require('node:assert/strict');

const { createLearningStateMachine } = require('../../src/renderer');

test('learning state transitions: Learning -> Assessment -> Result', () => {
  const machine = createLearningStateMachine();

  machine.setUnit({ id: 'unit-1', topic: 'Cardiac Output' }, { completed: 0, total: 3, percent: 0 });
  assert.equal(machine.getState().phase, 'learning');

  machine.beginAssessment();
  assert.equal(machine.getState().phase, 'assessment');

  machine.recordResult(
    { isCorrect: true, feedback: 'Nice.' },
    { id: 'unit-2', topic: 'SVR' },
    { completed: 1, total: 3, percent: 33.3 }
  );
  assert.equal(machine.getState().phase, 'result');
  assert.equal(machine.getState().result.isCorrect, true);

  machine.advance();
  assert.equal(machine.getState().phase, 'learning');
  assert.equal(machine.getState().currentUnit.id, 'unit-2');
});

test('learning state does not advance when complete', () => {
  const machine = createLearningStateMachine();
  machine.setUnit({ id: 'unit-1', topic: 'Pharmacokinetics' }, { completed: 1, total: 1, percent: 100 }, null, null, true);
  machine.beginAssessment();
  assert.equal(machine.getState().phase, 'learning');

  machine.recordResult({ isCorrect: true }, null, { completed: 1, total: 1, percent: 100 }, null, null, true);
  assert.equal(machine.getState().phase, 'result');

  machine.advance();
  assert.equal(machine.getState().phase, 'result');
});
