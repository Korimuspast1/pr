FROM node:20-alpine
WORKDIR /app
COPY package.json ./
COPY server.js ./
COPY src ./src
COPY public ./public
ENV PORT=3000 HOST=0.0.0.0 NODE_ENV=production
EXPOSE 3000
USER node
CMD ["node", "server.js"]
