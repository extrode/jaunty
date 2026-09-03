#!/usr/bin/env python3
"""Guards the packed .NET tool against shipping runtime assets it can never load.

Run from the repository root, against the packed CLI:

    python3 scripts/check-tool-package.py packout/Extrode.Jaunty.Scaffolding.Cli.*.nupkg

Exits 1 on any violation, so it can gate a release before the push steps run.

Why this exists. SQLitePCLRaw.bundle_e_sqlite3 carries e_sqlite3 compiled for all 34 runtime
identifiers it knows about, and PackAsTool cannot infer where the tool will be installed, so
every one of them was packed into 1.0.0-rc.2: 83,487,302 bytes compressed and 171.53 MB
unpacked, of which roughly 70 MB was iOS, Android, wasm and Mac Catalyst native code that
`dotnet tool install` can never load. TrimToolRuntimeAssets in Jaunty.Scaffolding.Cli.csproj
filters that down to the desktop and CI identifiers.

That filter is an MSBuild target keyed on an SDK item name, RuntimeTargetsCopyLocalItems, which
is an implementation detail of the .NET SDK rather than documented contract. If a future SDK
renames or reorders it the target silently stops matching, the csproj still reads as though the
trim were in force, and the only symptom is a package three times the size. This script reads
the identifiers the csproj declares and requires the package to contain that exact set - so the
csproj stays the single source of truth, and a filter that stopped working fails the release
instead of shipping.
"""

import glob
import re
import sys
import zipfile
from pathlib import Path

# 27,599,882 bytes at the time of writing. The ceiling is a second net under the identifier
# check, and catches growth the identifier set cannot see - a new managed dependency, or a
# provider adding native assets under an identifier that is already allowed.
SIZE_CEILING_BYTES = 35 * 1024 * 1024

CSPROJ = Path('src/Jaunty.Scaffolding.Cli/Jaunty.Scaffolding.Cli.csproj')

RID_IN_PATH = re.compile(r'^tools/[^/]+/[^/]+/runtimes/([^/]+)/')


def declared_identifiers(csproj: Path) -> set[str]:
    text = csproj.read_text(encoding='utf-8-sig')
    match = re.search(r'<JauntyToolRuntimeIdentifiers>(.*?)</JauntyToolRuntimeIdentifiers>',
                      text, re.DOTALL)
    if match is None:
        sys.exit(f'{csproj}: no <JauntyToolRuntimeIdentifiers> property. The trim was removed, '
                 'or this script is looking at the wrong project.')
    return {rid.strip() for rid in match.group(1).split(';') if rid.strip()}


def packed_identifiers(package: Path) -> set[str]:
    with zipfile.ZipFile(package) as archive:
        names = archive.namelist()
    return {m.group(1) for m in (RID_IN_PATH.match(n) for n in names) if m}


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        sys.exit('usage: check-tool-package.py <path-to-nupkg>')

    # The caller passes a glob so the version number does not have to be repeated in the
    # workflow; resolve it here rather than relying on the shell, which does not expand an
    # unmatched pattern on every platform.
    matches = sorted(glob.glob(argv[1]))
    if len(matches) != 1:
        sys.exit(f'expected exactly one package matching {argv[1]}, found {len(matches)}: '
                 f'{matches}')
    package = Path(matches[0])

    declared = declared_identifiers(CSPROJ)
    packed = packed_identifiers(package)
    size = package.stat().st_size

    failures = []

    unexpected = sorted(packed - declared)
    if unexpected:
        failures.append(
            f'{len(unexpected)} runtime identifier(s) packed that the csproj does not declare: '
            + ', '.join(unexpected)
            + '. TrimToolRuntimeAssets is not filtering - check whether the SDK still populates '
              'RuntimeTargetsCopyLocalItems at ResolvePackageAssets.')

    missing = sorted(declared - packed)
    if missing:
        failures.append(
            f'{len(missing)} declared runtime identifier(s) absent from the package: '
            + ', '.join(missing)
            + '. The tool will throw at connection-open time on those platforms, not at build '
              'time. `win` and `unix` are managed Microsoft.Data.SqlClient assets, not native '
              'ones, and losing either breaks SQL Server scaffolding everywhere.')

    if size > SIZE_CEILING_BYTES:
        failures.append(f'package is {size:,} bytes, over the {SIZE_CEILING_BYTES:,} ceiling.')

    if failures:
        print(f'FAIL {package.name}', file=sys.stderr)
        for failure in failures:
            print(f'  - {failure}', file=sys.stderr)
        return 1

    print(f'OK {package.name}: {size:,} bytes, {len(packed)} runtime identifiers, '
          'all declared.')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
