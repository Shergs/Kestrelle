FROM node:20-alpine AS build
WORKDIR /app
COPY Kestrelle/Kestrelle/Kestrelle.Client/Kestrelle.Client/package*.json ./
RUN npm ci
COPY Kestrelle/Kestrelle/Kestrelle.Client/Kestrelle.Client/ ./
RUN npm run build

FROM nginx:alpine
COPY Kestrelle/Kestrelle/Kestrelle.Client/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80