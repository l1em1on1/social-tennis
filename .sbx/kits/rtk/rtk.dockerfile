# The RTK binary as a v3 tool mixin (#175). It was baked into the v2 template;
# with no template, the rtk kit ships it. The installer is pinned at the v1.2.0
# commit and RTK_VERSION stops it resolving "latest"; it checks the release's
# checksum, and installs straight to /usr/local/bin, on the claude kit's PATH.
FROM buildpack-deps:trixie-curl@sha256:9e486725325568707dbd794ca9b53cffb7ddee2b8ac1b94941ca4d3d8c6e9df9 AS build
ARG RTK_INSTALLER_SHA=e288a18c04452fd26192f59132e5324a8bb6924d
ARG RTK_VERSION=v1.2.0
RUN curl -fsSL "https://raw.githubusercontent.com/rtk-ai/rtk/${RTK_INSTALLER_SHA}/install.sh" \
  | RTK_VERSION="${RTK_VERSION}" RTK_INSTALL_DIR=/out/usr/local/bin sh

FROM scratch
COPY --from=build /out/ /
