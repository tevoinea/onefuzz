# See here for image contents: https://github.com/microsoft/vscode-dev-containers/tree/v0.224.3/containers/ubuntu/.devcontainer/base.Dockerfile

ARG VARIANT="ubuntu-22.04"
FROM mcr.microsoft.com/vscode/devcontainers/base:0-${VARIANT}

# Setup packages for azure functions core tools 4
RUN curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg && \
    sudo mv microsoft.gpg /etc/apt/trusted.gpg.d/microsoft.gpg && \
    sudo sh -c 'echo "deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-ubuntu-$(lsb_release -cs)-prod $(lsb_release -cs) main" > /etc/apt/sources.list.d/dotnetdev.list'

# Setup MS package signing key:
# NB: needs changing if VARIANT changes
RUN wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb

# Install APT HTTPS transport required for MS packages:
RUN apt-get update && export DEBIAN_FRONTEND=noninteractive && apt-get install -y apt-transport-https

# Install packages required for build:
RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
    && apt-get -y install --no-install-recommends \
    libunwind-dev clang build-essential libssl-dev pkg-config lldb \
    bash-completion npm python-is-python3 azure-functions-core-tools-4 \
    direnv uuid-runtime

# Install azcopy
WORKDIR /tmp
RUN wget https://aka.ms/downloadazcopy-v10-linux && \
    tar -xvf downloadazcopy-v10-linux && \
    sudo cp ./azcopy_linux_amd64_*/azcopy /usr/bin/

# Install Azurite
RUN sudo npm install -g azurite

# Install rust tools
# RUN cargo install cargo-audit

# Checking file structure
WORKDIR /workspaces/onefuzz
RUN ls -lah
