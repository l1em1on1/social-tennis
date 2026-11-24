# The mock API (ADR-0033), for the e2e suite and for running web without the
# real one. Never deployed. Built from web/, since it uses web's src/lib; its
# ignore file is Dockerfile.dockerignore, beside this one.
#
# Base images pinned by tag and digest, as web/Dockerfile's (#25). The first
# four steps are the same as that file's, so the two share `npm ci`.
FROM node:24-alpine@sha256:116b800c3754b440ee0aa3ed2f20f5bf3d99a554bfd9adeea09ea8017f6e85f5 AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# One file, packages included: Node alone can't resolve the handlers' "@/"
# imports. CommonJS, because some of MSW's packages `require` Node's own.
RUN npx esbuild mock-api/server.ts --bundle --packages=bundle --platform=node --target=node24 --format=cjs --outfile=/app/server.cjs

FROM node:24-alpine@sha256:116b800c3754b440ee0aa3ed2f20f5bf3d99a554bfd9adeea09ea8017f6e85f5
WORKDIR /app
ENV NODE_ENV=production
COPY --from=build /app/server.cjs ./
USER node
EXPOSE 8080
CMD ["node", "server.cjs"]
