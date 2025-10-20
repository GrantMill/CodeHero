FROM mcr.microsoft.com/devcontainers/python:1-3.11-bookworm

# Install dependencies
RUN pip install --no-cache-dir fastapi uvicorn[standard] faster-whisper python-multipart

WORKDIR /app
COPY app.py /app/app.py

EXPOSE 8000
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8000"]
