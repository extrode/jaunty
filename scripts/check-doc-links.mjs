// Reports relative markdown links in docs/ and README.md that do not resolve to a file on disk.
// Run from the repository root: node scripts/check-doc-links.mjs
// Exits 1 when anything is broken, so it can gate a docs build.
//
// External links (http, mailto, tel) and anchor fragments are not checked. Links inside fenced
// code blocks are not excluded, so an illustrative path in a ```markdown fence reports as broken;
// docs/_assets/README.md has one on purpose.

import { readFileSync, existsSync, readdirSync, statSync } from 'fs';
import { join, dirname, resolve } from 'path';

const SKIP_DIRS = ['node_modules', 'bin', 'obj', '.git', '99-archive'];

const files = [];
(function walk(dir) {
  for (const entry of readdirSync(dir)) {
    if (SKIP_DIRS.includes(entry)) continue;
    const path = join(dir, entry);
    if (statSync(path).isDirectory()) walk(path);
    else if (entry.endsWith('.md')) files.push(path);
  }
})('docs');
files.push('README.md');

let broken = 0;
for (const file of files) {
  const text = readFileSync(file, 'utf8');
  for (const match of text.matchAll(/\[[^\]]*\]\(([^)\s#]+)(?:#[^)]*)?\)/g)) {
    const target = match[1];
    if (/^(https?:|mailto:|tel:)/.test(target)) continue;
    if (existsSync(resolve(dirname(file), decodeURIComponent(target)))) continue;
    console.log(`${file} -> ${target}`);
    broken++;
  }
}

console.log(`\n${broken} broken link${broken === 1 ? '' : 's'} across ${files.length} files`);
process.exit(broken > 0 ? 1 : 0);
