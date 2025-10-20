FROM python:3.11-slim
RUN pip install --no-cache-dir fastapi uvicorn[standard] faster-whisper
ENV WHISPER_MODEL=small COMPUTE_TYPE=int8 MODEL_ROOT=/models
WORKDIR /app
COPY app.py /app/app.py
VOLUME ["/models"]
EXPOSE 8000
CMD ["uvicorn","app:app","--host","0.0.0.0","--port","8000"]
