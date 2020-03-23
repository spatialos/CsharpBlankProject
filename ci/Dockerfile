FROM ubuntu:18.04

RUN apt-get update \
    && apt-get install -y \
        ca-certificates \
        gnupg \
        wget \
    && rm -rf /var/lib/apt/lists/**

RUN wget -q https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF \
    && echo "deb https://download.mono-project.com/repo/ubuntu stable-bionic main" | tee /etc/apt/sources.list.d/mono-official-stable.list \
    && apt update \
    && apt install -y mono-devel \
    && rm -rf /var/lib/apt/lists/**

ARG USER_ID
ARG GROUP_ID

RUN addgroup --gid $GROUP_ID user \
    && adduser --disabled-password --uid $USER_ID --gid $GROUP_ID user
USER user
