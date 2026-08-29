// Reports relative markdown links in docs/, README.md and CHANGELOG.md that do not resolve on disk.
// Run from the repository root: node scripts/check-doc-links.mjs
// Exits 1 when anything is broken, so it can gate a docs build.
//
// External links (http, mailto, tel) and anchor fragments are not checked.
//
// Fenced code blocks are skipped. They used to be scanned, which made an illustrative path in a
// ```markdown fence report as broken - docs/_assets/README.md carries one on purpose, showing a
// reader how to embed an image. One permanent false positive means the exit code can never be
// zero, and a gate that is always red is not a gate.

import { readFileSync, existsSync, readdirSync, statSync } from 'fs';
import { join, dirname, resolve } from 'path';

const SKIP_DIRS = ['node_modules', 'bin', 'obj', '.git', '99-archive'];

// Blanks the body of every fenced block (``` or ~~~, any info string, any fence length >= 3),
// keeping line and character positions intact so nothing else has to change. An unterminated
// fence blanks to end of file, which is the safe direction: a stray link is missed rather than
// a code sample reported.
export function stripFencedCode(text) {
  const lines = text.split('\n');
  let fence = null;
  for (let i = 0; i < lines.length; i++) {
    const open = lines[i].match(/^\s{0,3}(`{3,}|~{3,})/);
    if (fence === null) {
      if (open) fence = open[1];
      continue;
    }
    const close = open && open[1][0] === fence[0] && open[1].length >= fence.length
      && lines[i].slice(lines[i].indexOf(open[1]) + open[1].length).trim() === '';
    lines[i] = '';
    if (close) fence = null;
  }
  return lines.join('\n');
}

/** Every relative link in one file's text that does not resolve, as bare target strings. */
export function brokenLinksIn(file, text) {
  const missing = [];
  for (const match of stripFencedCode(text).matchAll(/\[[^\]]*\]\(([^)\s#]+)(?:#[^)]*)?\)/g)) {
    const target = match[1];
    if (/^(https?:|mailto:|tel:)/.test(target)) continue;
    if (existsSync(resolve(dirname(file), decodeURIComponent(target)))) continue;
    missing.push(target);
  }
  return missing;
}

function markdownFiles() {
  const found = [];
  (function walk(dir) {
    for (const entry of readdirSync(dir)) {
      if (SKIP_DIRS.includes(entry)) continue;
      const path = join(dir, entry);
      if (statSync(path).isDirectory()) walk(path);
      else if (entry.endsWith('.md')) found.push(path);
    }
  })('docs');
  found.push('README.md', 'CHANGELOG.md');
  return found;
}

// Only when run as a script; importing this module for tests must not walk the tree or exit.
if (process.argv[1] && resolve(process.argv[1]) === resolve(import.meta.filename)) {
  const files = markdownFiles();
  let broken = 0;

  for (const file of files) {
    for (const target of brokenLinksIn(file, readFileSync(file, 'utf8'))) {
      console.log(`${file} -> ${target}`);
      broken++;
    }
  }

  console.log(`\n${broken} broken link${broken === 1 ? '' : 's'} across ${files.length} files`);
  process.exit(broken > 0 ? 1 : 0);
}
