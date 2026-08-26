#!/usr/bin/env python3
"""Make `import gsclient` work regardless of cwd by exposing webmine/ on sys.path."""

import sys
from pathlib import Path

_WEBMINE_DIR = str(Path(__file__).resolve().parent.parent)
if _WEBMINE_DIR not in sys.path:
    sys.path.insert(0, _WEBMINE_DIR)
