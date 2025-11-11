#!/usr/bin/env python3

"""Determine whether the Python package version changed since the previous release."""

from __future__ import annotations

import os
import pathlib
import json
import subprocess
import sys
from typing import Dict, Optional
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

try:  # Python 3.11+
    import tomllib  # type: ignore[attr-defined]
except ModuleNotFoundError:  # pragma: no cover
    try:
        import tomli as tomllib  # type: ignore[no-redef]
    except ModuleNotFoundError:  # pragma: no cover
        tomllib = None  # type: ignore[assignment]

PYPROJECT_PATH = pathlib.Path("pyproject.toml")


def parse_project_table(toml_text: str) -> Dict[str, object]:
    """Parse [project] table using tomllib when available, otherwise a tiny parser."""
    if tomllib is not None:
        data = tomllib.loads(toml_text)
        project = data.get("project")
        if isinstance(project, dict):
            return project
        raise RuntimeError("Missing [project] table in pyproject.toml")

    in_project = False
    project: Dict[str, object] = {}
    for raw_line in toml_text.splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("[") and line.endswith("]"):
            in_project = line == "[project]"
            continue
        if not in_project or "=" not in line:
            continue
        key, value = line.split("=", 1)
        key = key.strip()
        value = value.strip()
        if value.startswith(("'", '"')) and value.endswith(("'", '"')) and len(value) >= 2:
            value = value[1:-1]
        project[key] = value

    if not project:
        raise RuntimeError("Missing [project] table in pyproject.toml")
    return project


def load_pyproject(path: pathlib.Path) -> tuple[str, str]:
    if not path.exists():
        raise RuntimeError(f"{path} not found")

    project = parse_project_table(path.read_text(encoding="utf-8"))

    try:
        name = project["name"]  # type: ignore[index]
        version = project["version"]  # type: ignore[index]
    except KeyError as exc:  # pragma: no cover
        raise RuntimeError(f"Missing project field: {exc}") from exc

    if not isinstance(name, str) or not isinstance(version, str):
        raise RuntimeError("Project name/version must be strings")

    return name, version


def fetch_pypi_version(package_name: str) -> Optional[str]:
    url = f"https://pypi.org/pypi/{package_name}/json"
    request = Request(url, headers={"Accept": "application/json"})
    try:
        with urlopen(request, timeout=10) as response:
            data = json.load(response)
    except HTTPError as err:
        if err.code == 404:
            return None
        raise RuntimeError(f"PyPI responded with HTTP {err.code}") from err
    except URLError as err:
        raise RuntimeError(f"Unable to reach PyPI: {err}") from err

    info = data.get("info") or {}
    version = info.get("version")
    return version if isinstance(version, str) and version.strip() else None


def read_previous_pyproject_version() -> Optional[str]:
    """Return the previously committed version from git history."""
    try:
        commit = (
            subprocess.check_output(
                ("git", "rev-list", "-n", "1", "HEAD^", "--", "pyproject.toml"),
                text=True,
                stderr=subprocess.DEVNULL,
            )
            .strip()
        )
    except subprocess.CalledProcessError:
        return None

    if not commit:
        return None

    try:
        contents = subprocess.check_output(
            ("git", "show", f"{commit}:pyproject.toml"),
            text=True,
            stderr=subprocess.DEVNULL,
        )
    except subprocess.CalledProcessError:
        return None

    project = parse_project_table(contents)
    version = project.get("version")  # type: ignore[index]
    return version if isinstance(version, str) else None


def determine_reference_version(package_name: str) -> tuple[Optional[str], str]:
    """Return (version, source) for comparison."""
    try:
        pypi_version = fetch_pypi_version(package_name)
    except RuntimeError as err:
        print(f"Warning: {err}. Falling back to git history.", file=sys.stderr)
        pypi_version = None

    if pypi_version:
        return pypi_version, "PyPI"

    git_version = read_previous_pyproject_version()
    if git_version:
        return git_version, "git history"

    return None, "<unknown>"


def main() -> int:
    package_name, local_version = load_pyproject(PYPROJECT_PATH)
    reference_version, reference_source = determine_reference_version(package_name)

    version_changed = (
        True if reference_version is None else reference_version != local_version
    )

    print(
        "Local version:",
        local_version,
        "\nReference source:",
        reference_source,
        "\nReference version:",
        reference_version or "<unknown>",
        "\nVersion changed:",
        version_changed,
        file=sys.stdout,
    )

    github_output = os.environ.get("GITHUB_OUTPUT")
    if github_output:
        with open(github_output, "a", encoding="utf-8") as fh:
            fh.write(f"version_changed={'true' if version_changed else 'false'}\n")
            fh.write(f"current_version={local_version}\n")
            fh.write(f"reference_version={reference_version or ''}\n")
            fh.write(f"reference_source={reference_source}\n")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
