# Base images are pinned by tag and digest (#25); Dependabot's docker updates
# move both together. The bump procedure is in docs/architecture.md ("Versions").
FROM node:24-alpine@sha256:116b800c3754b440ee0aa3ed2f20f5bf3d99a554bfd9adeea09ea8017f6e85f5 AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM node:24-alpine@sha256:116b800c3754b440ee0aa3ed2f20f5bf3d99a554bfd9adeea09ea8017f6e85f5
WORKDIR /app
ENV NODE_ENV=production
# The standalone server binds to $HOSTNAME, which Docker would otherwise set to
# the container's own name: reachable from other containers, but not on
# loopback, and not on every host's ingress. Listen on all interfaces.
ENV HOSTNAME=0.0.0.0
COPY --from=build /src/.next/standalone ./
COPY --from=build /src/.next/static ./.next/static
COPY --from=build /src/public ./public
# The app's files stay root-owned, so the server can't change its own code.
# Next writes only its cache, which the runtime user owns.
RUN mkdir -p .next/cache && chown node:node .next/cache
# Not root (#25): the image's own `node` user (UID 1000). Port 3000 needs no privilege.
USER node
EXPOSE 3000
CMD ["node", "server.js"]
