#!/usr/bin/env python3
import sys
import os
import json
from xml.etree import ElementTree as ET

def main():
    if len(sys.argv) < 2:
        print("Usage: check_coverage.py <cobertura-xml>", file=sys.stderr)
        return 2
    cov = sys.argv[1]
    try:
        tree = ET.parse(cov)
    except Exception as e:
        print(f"Failed to parse coverage XML: {e}", file=sys.stderr)
        return 2

    try:
        with open('.github/coverage-thresholds.json', 'r') as fh:
            thresholds = json.load(fh)
    except Exception as e:
        print(f"Failed to parse thresholds JSON: {e}", file=sys.stderr)
        return 2

    root = tree.getroot()
    packages = []
    for pkg in root.findall('.//package'):
        name = pkg.get('name')
        lr = pkg.get('line-rate')
        try:
            perc = float(lr) * 100.0 if lr is not None else 0.0
        except Exception:
            perc = 0.0
        packages.append((name, perc))

    print('Per-package coverage:')
    for name, perc in packages:
        print(f" - {name}: {perc:.1f}%")

    fail = False
    msgs = []
    default_th = int(os.environ.get('THRESHOLD', '75'))
    for name, perc in packages:
        if name in thresholds:
            th = int(thresholds[name])
        elif 'default' in thresholds:
            th = int(thresholds['default'])
        else:
            th = default_th
        if perc + 0.001 < th:
            fail = True
            msgs.append(f"{name} coverage {perc:.1f}% < threshold {th}%")

    if fail:
        print('\nPer-package threshold violations:', file=sys.stderr)
        for m in msgs:
            print(m, file=sys.stderr)
        return 2
    else:
        print('\nAll per-package thresholds satisfied')
        return 0

if __name__ == '__main__':
    sys.exit(main())
