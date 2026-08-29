// Tests for scripts/check-doc-links.mjs. Run from the repository root:
//
//   node --test scripts/tests/
//
// The module under test only walks docs/ and exits when it is process.argv[1], so importing it
// here is inert.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { stripFencedCode, brokenLinksIn } from '../check-doc-links.mjs';

test('a link outside any fence is still seen', () => {
  assert.deepEqual(
    brokenLinksIn('docs/x.md', 'see [that](no-such-file.md) please'),
    ['no-such-file.md']);
});

test('a link inside a backtick fence is ignored', () => {
  const text = '```markdown\n![Description](../assets/screenshots/example.png)\n```\n';
  assert.deepEqual(brokenLinksIn('docs/x.md', text), []);
});

test('a link inside a tilde fence is ignored', () => {
  const text = '~~~\n[a](nope.md)\n~~~\n';
  assert.deepEqual(brokenLinksIn('docs/x.md', text), []);
});

test('a fence does not swallow the rest of the file', () => {
  const text = '```\n[a](inside.md)\n```\n[b](outside.md)\n';
  assert.deepEqual(brokenLinksIn('docs/x.md', text), ['outside.md']);
});

// CommonMark: a closing fence has to be at least as long as the opener, so a longer run closes.
test('a longer run closes a shorter fence', () => {
  const text = '```\n````\n[a](after.md)\n';
  assert.deepEqual(brokenLinksIn('docs/x.md', text), ['after.md']);
});

test('a shorter run inside a longer fence does not close it', () => {
  const text = '````\n```\n[a](inside.md)\n````\n[b](outside.md)\n';
  assert.deepEqual(brokenLinksIn('docs/x.md', text), ['outside.md']);
});

test('a tilde run does not close a backtick fence', () => {
  const text = '```\n~~~\n[a](inside.md)\n```\n[b](outside.md)\n';
  assert.deepEqual(brokenLinksIn('docs/x.md', text), ['outside.md']);
});

test('an unterminated fence blanks to end of file', () => {
  assert.deepEqual(brokenLinksIn('docs/x.md', '```\n[a](nope.md)\n'), []);
});

test('an existing target is not reported', () => {
  assert.deepEqual(brokenLinksIn('README.md', '[c](CHANGELOG.md)'), []);
});

test('external schemes are not checked', () => {
  const text = '[a](https://example.com/x.md) [b](mailto:x@example.com) [c](tel:+100)';
  assert.deepEqual(brokenLinksIn('docs/x.md', text), []);
});

test('an anchor fragment is stripped before resolving', () => {
  assert.deepEqual(brokenLinksIn('README.md', '[c](CHANGELOG.md#top)'), []);
});

test('stripFencedCode preserves line count', () => {
  const text = 'a\n```\nb\nc\n```\nd\n';
  assert.equal(stripFencedCode(text).split('\n').length, text.split('\n').length);
});
