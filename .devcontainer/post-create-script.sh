#!/bin/bash

# Restore rust dependencies
echo "Restoring rust dependencies"
cargo install cargo-audit cargo-license@0.4.2 # requirements if you want to run ci/agent.sh
cd /workspaces/onefuzz/src/agent
cargo fetch

# Restore dotnet dependencies
echo "Restore dotnet dependencies"
cd /workspaces/onefuzz/src/ApiService
dotnet restore

echo "Setting up venv"
cd /workspaces/onefuzz/src
python -m venv venv
. ./venv/bin/activate

echo "Installing pytypes"
cd /workspaces/onefuzz/src/pytypes
echo "layout python3" >> .envrc
direnv allow
python -m pip install -e .

echo "Installing cli"
cd /workspaces/onefuzz/src/cli
echo "layout python3" >> .envrc
direnv allow
pythom -m pip install -e .

echo "Install api-service"
cd /workspaces/onefuzz/src/api-service
echo "layout python3" >> .envrc
direnv allow
python -m pip install -r requirements-dev.txt
cd __app__
python -m pip install -r requirements.txt

cd /workspaces/onefuzz/src/utils
chmod u+x lint.sh
python -m pip install types-six

cp /workspaces/onefuzz/.devcontainer/pre-commit /workspaces/onefuzz/.git/hooks
chmod u+x /workspaces/onefuzz/.git/hooks/pre-commit

# TODO
# WARNING: The script normalizer is installed in '/home/vscode/.local/bin' which is not on PATH.
#   Consider adding this directory to PATH or, if you prefer to suppress this warning, use --no-warn-script-location.
