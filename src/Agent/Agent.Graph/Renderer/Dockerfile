FROM mcr.microsoft.com/playwright/python:v1.51.0-noble

# Create app directory and temporary rendering directory
WORKDIR /app
RUN mkdir -p /app/tmp

# Install Node.js with a newer version for mermaid-cli compatibility
RUN apt-get update && apt-get install -y \
    curl \
    && curl -fsSL https://deb.nodesource.com/setup_16.x | bash - \
    && apt-get install -y nodejs \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

RUN apt-get update && apt-get install -y \
    fonts-liberation \
    libnss3 \
    libxss1 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2

RUN apt-get update && apt-get install -y \
    libgbm-dev \
    libxshmfence-dev \
    libxrandr2 \
    libxcomposite1 \
    libxcursor1 \
    libxi6 \
    libxtst6

# Verify Node.js version
RUN node --version && npm --version

# Install mermaid-cli
RUN npm install -g @mermaid-js/mermaid-cli@8.8.0 --unsafe-perm=true

# Verify mermaid-cli installation
RUN mmdc --version || echo "mmdc not installed correctly"

# Configure puppeteer to use system Chrome
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true

# Copy puppeteer configuration
COPY puppeteer-config.json /app/puppeteer-config.json
COPY style.css /app/style.css
COPY mermaid-config.json /app/mermaid-config.json

# Copy and install Python requirements
COPY requirements.txt /app/requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

# Copy the FastAPI application
COPY main.py /app/main.py

RUN playwright install

EXPOSE 3000
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "3000"]
