#!/usr/bin/env python3
"""
Validate that PR body contains a REQ-### reference when requirement or spec files change.

Usage: validate_req_reference.py <pr_body_file> <changed_files_list>
Exits 0 when OK, 1 when missing required REQ reference.
"""
import re
import sys
from pathlib import Path


def main():
    if len(sys.argv) < 3:
        print("Usage: validate_req_reference.py <pr_body_file> <changed_files_list>")
        return 2
    pr_body_path = Path(sys.argv[1])
    changed_files_path = Path(sys.argv[2])

    pr_body = pr_body_path.read_text(encoding='utf-8') if pr_body_path.exists() else ""
    changed = changed_files_path.read_text(encoding='utf-8').splitlines() if changed_files_path.exists() else []

    # Determine if any changed file is a requirement or spec
    requires_req = False
    for p in changed:
        p = p.strip()
        if not p:
            continue
        if p.startswith('docs/requirements/') or p.startswith('specs/') or p.startswith('specs/'):
            requires_req = True
            break

    if not requires_req:
        print('No requirement/spec files changed; skipping REQ reference validation.')
        return 0

    # Search PR body for REQ-### pattern
    m = re.search(r'REQ-\d{3,}', pr_body)
    if m:
        print(f'Found REQ reference: {m.group(0)}')
        return 0

    print('ERROR: PR modifies requirement/spec files but contains no REQ-### reference in the PR body.', file=sys.stderr)
    print('Please add a Related REQ(s): REQ-### line to your PR description.', file=sys.stderr)
    return 1


if __name__ == '__main__':
    sys.exit(main())
