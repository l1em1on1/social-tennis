# Base image tags are pinned deliberately — see docs/architecture.md "Versions".
FROM node:24-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# MOCK_API=1 bakes the MSW mocks into the build (the e2e web-mock service);
# left empty, next.config.ts inlines "" and the mocks are compiled out (ADR-0009).
ARG MOCK_API=""
ENV MOCK_API=$MOCK_API
RUN npm run build

FROM node:24-alpine
WORKDIR /app
ENV NODE_ENV=production
# The standalone server binds to $HOSTNAME, which Docker would otherwise set to
# the container's own name: reachable from other containers, but not on
# loopback, and not on every host's ingress. Listen on all interfaces.
ENV HOSTNAME=0.0.0.0
COPY --from=build /src/.next/standalone ./
COPY --from=build /src/.next/static ./.next/static
COPY --from=build /src/public ./public
EXPOSE 3000
CMD ["node", "server.js"]
