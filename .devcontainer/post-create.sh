#!/usr/bin/env bash
set -u

echo "[post-create] Install Python dependencies"
if ! pip3 install --user -r requirements.txt; then
  echo "[post-create] WARNING: pip install failed; continue and install manually later."
fi

echo "[post-create] Warm up .NET dependencies"
if ! bash .devcontainer/setup-dotnet.sh; then
  echo "[post-create] WARNING: .NET setup failed; continue and run .devcontainer/setup-dotnet.sh manually."
fi

echo "[post-create] Done"
exit 0
