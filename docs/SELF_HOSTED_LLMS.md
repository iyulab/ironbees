# Self-Hosted LLM Integration Guide

**Version**: 0.1.6
**Target**: OpenAI-Compatible endpoints (GPUStack, LocalAI, Ollama, vLLM)
**Audience**: DevOps, Platform Engineers, Cost-Conscious Developers

---

## Overview

This guide covers setting up and integrating self-hosted LLM infrastructure with Ironbees. Self-hosting provides:

- 🔐 **Privacy**: Keep sensitive data on-premise
- 💰 **Cost Control**: Eliminate per-token API charges
- 🚀 **Low Latency**: Local inference reduces network overhead
- 🌐 **Offline Operation**: Air-gapped environment support
- 🔧 **Model Flexibility**: Use any open-source model

---

## Supported Platforms

| Platform | Deployment | Difficulty | Best For |
|----------|------------|------------|----------|
| **GPUStack** | Kubernetes | Medium | Production GPU clusters |
| **LocalAI** | Docker/Binary | Easy | Development, testing |
| **Ollama** | Binary | Very Easy | Local development |
| **vLLM** | Docker/Python | Medium | High-throughput inference |

---

## 1. GPUStack - Kubernetes-Native GPU Management

### Overview

GPUStack manages GPU resources across Kubernetes clusters and provides OpenAI-compatible APIs.

**Best For**:
- Production deployments
- Multi-GPU clusters
- Team environments
- Model serving at scale

**Official Documentation**: https://docs.gpustack.ai/

### Prerequisites

```bash
# Required
- Kubernetes cluster (1.20+)
- NVIDIA GPUs with drivers installed
- kubectl configured
- Helm 3.x

# Optional but recommended
- Persistent storage (for model caching)
- Load balancer (for external access)
```

### Installation

#### 1. Install GPUStack via Helm

```bash
# Add GPUStack Helm repository
helm repo add gpustack https://gpustack.github.io/helm-charts
helm repo update

# Install GPUStack
helm install gpustack gpustack/gpustack \
  --namespace gpustack-system \
  --create-namespace \
  --set service.type=LoadBalancer \
  --set persistence.enabled=true
```

#### 2. Get API Credentials

```bash
# Wait for deployment
kubectl wait --for=condition=ready pod -l app=gpustack -n gpustack-system --timeout=300s

# Get API endpoint
export GPUSTACK_ENDPOINT=$(kubectl get svc gpustack -n gpustack-system -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
echo "GPUStack endpoint: http://$GPUSTACK_ENDPOINT:8080"

# Get API key
export GPUSTACK_API_KEY=$(kubectl get secret gpustack-api-key -n gpustack-system -o jsonpath='{.data.api-key}' | base64 -d)
echo "API Key: $GPUSTACK_API_KEY"
```

#### 3. Deploy a Model

```bash
# Example: Deploy Llama 3.1 8B
curl -X POST "http://$GPUSTACK_ENDPOINT:8080/v1/models" \
  -H "Authorization: Bearer $GPUSTACK_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "llama-3.1-8b-instruct",
    "source": "huggingface",
    "model_id": "meta-llama/Meta-Llama-3.1-8B-Instruct",
    "quantization": "q4_k_m",
    "gpu_layers": -1
  }'

# Check deployment status
curl "http://$GPUSTACK_ENDPOINT:8080/v1/models" \
  -H "Authorization: Bearer $GPUSTACK_API_KEY"
```

### Ironbees Integration

```csharp
using Ironbees.AgentMode.Configuration;
using Ironbees.AgentMode.Providers;

var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://172.30.1.53:8080/v1",  // or /v1-openai
    ApiKey = "gpustack_8ef8f2d1e0537fb8_9f99ccb2699267880f8a5787deab1cf1",
    Model = "llama-3.1-8b-instruct",
    Temperature = 0.0f,
    MaxOutputTokens = 4096
};

var factory = new OpenAICompatibleProviderFactory();
var chatClient = factory.CreateChatClient(config);

// Use with any Microsoft.Extensions.AI consumer
var response = await chatClient.CompleteAsync([
    new ChatMessage(ChatRole.User, "Write a C# hello world")
]);
```

### Environment Variables

```bash
# .env file
GPUSTACK_ENDPOINT=http://172.30.1.53:8080
GPUSTACK_API_KEY=gpustack_xxx
GPUSTACK_MODEL=llama-3.1-8b-instruct
```

### Performance Tuning

```yaml
# values.yaml for Helm
resources:
  requests:
    nvidia.com/gpu: 1
    memory: 16Gi
  limits:
    nvidia.com/gpu: 1
    memory: 16Gi

inference:
  batchSize: 8              # Increase for throughput
  maxConcurrentRequests: 4  # Parallel requests
  timeout: 60s

modelCache:
  size: 50Gi               # Cache multiple models
  storageClass: fast-ssd
```

---

## 2. LocalAI - Self-Hosted OpenAI Alternative

### Overview

LocalAI is a drop-in replacement for OpenAI API, supporting multiple model backends.

**Best For**:
- Development and testing
- Quick prototyping
- CPU-only environments
- Easy Docker deployment

**Official Documentation**: https://localai.io/

### Installation

#### Option 1: Docker (Recommended)

```bash
# Pull and run LocalAI
docker run -d \
  --name localai \
  -p 8080:8080 \
  -v $PWD/models:/models \
  -v $PWD/images:/tmp/generated/images \
  --restart=always \
  localai/localai:latest

# Download a model (example: Llama 2 7B)
docker exec localai \
  wget https://huggingface.co/TheBloke/Llama-2-7B-Chat-GGUF/resolve/main/llama-2-7b-chat.Q4_K_M.gguf \
  -O /models/llama-2-7b-chat.gguf
```

#### Option 2: Docker Compose

```yaml
# docker-compose.yml
version: '3.8'

services:
  localai:
    image: localai/localai:latest
    ports:
      - "8080:8080"
    volumes:
      - ./models:/models
      - ./images:/tmp/generated/images
    environment:
      - THREADS=4
      - CONTEXT_SIZE=4096
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/readiness"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped
```

```bash
# Start LocalAI
docker-compose up -d

# Check status
docker-compose logs -f localai
```

#### Option 3: Binary (Linux/macOS)

```bash
# Download binary
curl -Lo localai https://github.com/go-skynet/LocalAI/releases/latest/download/localai-Linux-x86_64
chmod +x localai

# Run LocalAI
./localai --models-path ./models --threads 4 --context-size 4096
```

### Model Setup

```bash
# Download model via LocalAI API
curl -X POST http://localhost:8080/models/apply \
  -H "Content-Type: application/json" \
  -d '{
    "id": "TheBloke/Llama-2-7B-Chat-GGUF",
    "name": "llama-2-7b"
  }'

# List available models
curl http://localhost:8080/v1/models
```

### Ironbees Integration

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://localhost:8080/v1",
    ApiKey = "optional",  // LocalAI doesn't require auth by default
    Model = "llama-2-7b",
    Temperature = 0.0f,
    MaxOutputTokens = 4096
};

var factory = new OpenAICompatibleProviderFactory();
var chatClient = factory.CreateChatClient(config);
```

### Configuration File

```yaml
# models/llama-2-7b.yaml
name: llama-2-7b
backend: llama
parameters:
  model: llama-2-7b-chat.gguf
  temperature: 0.7
  top_k: 40
  top_p: 0.95
  max_tokens: 4096
  context_size: 4096
  gpu_layers: 0  # Set to -1 for full GPU offloading
```

---

## 3. Ollama - Simplest Local LLM Runner

### Overview

Ollama provides the easiest way to run LLMs locally with a single command.

**Best For**:
- Personal development
- Experimentation
- Minimal setup requirements
- macOS/Linux/Windows

**Official Documentation**: https://ollama.ai/

### Installation

#### macOS

```bash
# Download and install from https://ollama.ai/download
# Or use Homebrew
brew install ollama

# Start Ollama service
ollama serve
```

#### Linux

```bash
# Install script
curl -fsSL https://ollama.ai/install.sh | sh

# Start service (systemd)
sudo systemctl start ollama
sudo systemctl enable ollama
```

#### Windows

```powershell
# Download installer from https://ollama.ai/download
# Or use winget
winget install Ollama.Ollama

# Ollama starts automatically as a service
```

### Model Management

```bash
# Pull a model
ollama pull llama3.1:8b

# List local models
ollama list

# Run interactive chat (for testing)
ollama run llama3.1:8b

# Remove a model
ollama rm llama3.1:8b
```

### Available Models

```bash
# Popular models
ollama pull llama3.1:8b          # Llama 3.1 8B
ollama pull mistral:7b-instruct  # Mistral 7B Instruct
ollama pull codellama:7b         # Code Llama 7B
ollama pull phi3:mini            # Microsoft Phi-3 Mini
ollama pull gemma:2b             # Google Gemma 2B
```

### Ironbees Integration

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://localhost:11434/v1",  // Ollama's OpenAI-compatible endpoint
    ApiKey = "dummy",  // Ollama doesn't require authentication
    Model = "llama3.1:8b",
    Temperature = 0.0f,
    MaxOutputTokens = 4096
};

var factory = new OpenAICompatibleProviderFactory();
var chatClient = factory.CreateChatClient(config);
```

### Environment Variables

```bash
# .env file
OLLAMA_HOST=http://localhost:11434
OLLAMA_MODEL=llama3.1:8b
```

### Custom Modelfile

```dockerfile
# Modelfile for custom configuration
FROM llama3.1:8b

# Set parameters
PARAMETER temperature 0.0
PARAMETER num_ctx 4096
PARAMETER num_gpu 1

# Set system prompt
SYSTEM You are a helpful C# coding assistant specializing in .NET development.
```

```bash
# Create custom model
ollama create csharp-assistant -f Modelfile

# Use custom model
ollama run csharp-assistant
```

---

## 4. vLLM - High-Throughput Inference

### Overview

vLLM is optimized for high-throughput serving with advanced batching and memory management.

**Best For**:
- Production API servers
- High request volume
- Batched inference
- Maximum GPU utilization

**Official Documentation**: https://docs.vllm.ai/

### Installation

#### Docker (Recommended)

```bash
# Pull vLLM image
docker pull vllm/vllm-openai:latest

# Run vLLM server
docker run -d \
  --name vllm \
  --gpus all \
  -p 8000:8000 \
  -v $HOME/.cache/huggingface:/root/.cache/huggingface \
  vllm/vllm-openai:latest \
  --model meta-llama/Meta-Llama-3.1-8B-Instruct \
  --dtype auto \
  --max-model-len 4096
```

#### Python (Development)

```bash
# Install vLLM
pip install vllm

# Run server
python -m vllm.entrypoints.openai.api_server \
  --model meta-llama/Meta-Llama-3.1-8B-Instruct \
  --dtype auto \
  --max-model-len 4096 \
  --port 8000
```

### Advanced Configuration

```bash
# High-throughput configuration
docker run -d \
  --name vllm-production \
  --gpus all \
  -p 8000:8000 \
  -v $HOME/.cache/huggingface:/root/.cache/huggingface \
  vllm/vllm-openai:latest \
  --model meta-llama/Meta-Llama-3.1-8B-Instruct \
  --dtype bfloat16 \
  --max-model-len 8192 \
  --tensor-parallel-size 2 \      # Multi-GPU
  --max-num-batched-tokens 8192 \ # Batch size
  --max-num-seqs 256 \            # Concurrent requests
  --gpu-memory-utilization 0.95
```

### Ironbees Integration

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://localhost:8000/v1",
    ApiKey = "dummy",  // vLLM doesn't require auth by default
    Model = "meta-llama/Meta-Llama-3.1-8B-Instruct",
    Temperature = 0.0f,
    MaxOutputTokens = 4096
};

var factory = new OpenAICompatibleProviderFactory();
var chatClient = factory.CreateChatClient(config);
```

### Production Deployment

```yaml
# kubernetes/vllm-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: vllm-server
spec:
  replicas: 1
  selector:
    matchLabels:
      app: vllm
  template:
    metadata:
      labels:
        app: vllm
    spec:
      containers:
      - name: vllm
        image: vllm/vllm-openai:latest
        ports:
        - containerPort: 8000
        resources:
          limits:
            nvidia.com/gpu: 2
        args:
          - --model
          - meta-llama/Meta-Llama-3.1-8B-Instruct
          - --dtype
          - bfloat16
          - --tensor-parallel-size
          - "2"
---
apiVersion: v1
kind: Service
metadata:
  name: vllm-service
spec:
  selector:
    app: vllm
  ports:
  - port: 8000
    targetPort: 8000
  type: LoadBalancer
```

---

## Performance Comparison

### Throughput Benchmarks

| Platform | Requests/sec (single GPU) | Latency (p50) | Memory Usage |
|----------|---------------------------|---------------|--------------|
| GPUStack | ~30 req/s | 100ms | 8-12GB |
| LocalAI | ~10 req/s | 300ms | 6-8GB |
| Ollama | ~15 req/s | 200ms | 6-10GB |
| vLLM | ~50 req/s | 80ms | 10-14GB |

*Tested with Llama 3.1 8B, batch size 1, on NVIDIA A100 40GB*

### Resource Requirements

| Platform | Minimum RAM | Recommended GPU | CPU Cores |
|----------|-------------|-----------------|-----------|
| GPUStack | 16GB | NVIDIA T4+ | 8+ |
| LocalAI | 8GB | Optional (CPU mode available) | 4+ |
| Ollama | 8GB | Optional (CPU mode available) | 4+ |
| vLLM | 16GB | NVIDIA A100/A10 | 8+ |

---

## Cost Analysis

### Hardware Costs (AWS Pricing Example)

| Instance Type | GPU | RAM | On-Demand | Spot | Annual (Reserved) |
|---------------|-----|-----|-----------|------|-------------------|
| g4dn.xlarge | T4 16GB | 16GB | $0.526/hr | ~$0.16/hr | ~$1,800 |
| g5.xlarge | A10G 24GB | 16GB | $1.006/hr | ~$0.30/hr | ~$3,600 |
| p3.2xlarge | V100 16GB | 61GB | $3.06/hr | ~$0.92/hr | ~$10,800 |

### Cost Comparison (1M tokens/day)

| Option | Monthly Cost | Break-even Point |
|--------|--------------|------------------|
| OpenAI API (gpt-4o-mini) | $150 | N/A |
| GPUStack (g5.xlarge spot) | ~$220 | 0.7M tokens/day |
| LocalAI (t3.xlarge CPU) | ~$90 | 0.6M tokens/day |
| Ollama (local workstation) | $0 (electricity) | Immediate |

**Conclusion**: Self-hosting becomes cost-effective above ~500K tokens/day

---

## Monitoring and Observability

### Health Checks

```bash
# GPUStack
curl http://gpustack-endpoint:8080/health

# LocalAI
curl http://localhost:8080/readiness

# Ollama
curl http://localhost:11434/api/tags

# vLLM
curl http://localhost:8000/health
```

### Metrics Collection (Prometheus)

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'gpustack'
    static_configs:
      - targets: ['gpustack:8080']
    metrics_path: '/metrics'

  - job_name: 'vllm'
    static_configs:
      - targets: ['vllm:8000']
    metrics_path: '/metrics'
```

### Logging

```csharp
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<OpenAICompatibleProviderFactory>();

// Logs will include request/response details
var chatClient = factory.CreateChatClient(config);
```

---

## Troubleshooting

### Common Issues

#### 1. Out of Memory

**Symptoms**: GPU OOM, server crashes

**Solutions**:
```bash
# Reduce model size (use quantized models)
ollama pull llama3.1:8b-q4_K_M  # 4-bit quantization

# Reduce context length
--max-model-len 2048

# Enable CPU offloading
--gpu-layers 20  # Offload only 20 layers to GPU
```

#### 2. Slow Inference

**Symptoms**: High latency, timeouts

**Solutions**:
```bash
# Increase GPU layers
--gpu-layers -1  # Full GPU offloading

# Enable tensor parallelism (vLLM)
--tensor-parallel-size 2

# Adjust batch size
--max-num-batched-tokens 4096
```

#### 3. Connection Refused

**Symptoms**: Cannot connect to endpoint

**Solutions**:
```bash
# Check server is running
docker ps | grep localai

# Verify port binding
netstat -tulpn | grep 8080

# Test with curl
curl http://localhost:8080/v1/models
```

#### 4. Model Loading Fails

**Symptoms**: Model not found, download errors

**Solutions**:
```bash
# Verify model path
ls -la ./models/

# Check disk space
df -h

# Re-download model
ollama pull llama3.1:8b --verbose
```

---

## Security Considerations

### 1. Network Security

```bash
# Restrict to local network only
docker run -p 127.0.0.1:8080:8080 localai/localai

# Use reverse proxy with authentication
nginx -> LocalAI (with Basic Auth)
```

### 2. API Key Management

```bash
# Generate API key for GPUStack
kubectl create secret generic gpustack-api-key \
  --from-literal=api-key=$(openssl rand -hex 32)

# Use environment variables (not hardcoded)
export LLM_API_KEY=$(cat /secrets/api-key)
```

### 3. Model Validation

```bash
# Verify model checksums
sha256sum llama-2-7b-chat.gguf
# Compare with official checksums
```

---

## Migration Path

### From Cloud API to Self-Hosted

```csharp
// Step 1: Start with OpenAI
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAI,
    Model = "gpt-4o-mini",
    ApiKey = openaiKey
};

// Step 2: Parallel testing with LocalAI
var testConfig = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://localhost:8080/v1",
    Model = "llama-3.1-8b",
    ApiKey = "test"
};

// Step 3: Gradual rollout
var useLocal = DateTime.Now.Hour >= 9 && DateTime.Now.Hour < 17;  // Business hours
var activeConfig = useLocal ? testConfig : config;

// Step 4: Full migration
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://production-vllm:8000/v1",
    Model = "llama-3.1-70b",
    ApiKey = prodApiKey
};
```

---

## Best Practices

### 1. Model Selection

```
Development: Ollama + small models (Llama 3.1 8B, Mistral 7B)
Staging: LocalAI + medium models (Llama 3.1 70B quantized)
Production: GPUStack/vLLM + optimized models
```

### 2. Resource Allocation

```yaml
# Conservative (testing)
GPU: 1x T4 (16GB)
RAM: 16GB
Context: 2048 tokens

# Balanced (production)
GPU: 1x A10G (24GB)
RAM: 32GB
Context: 4096 tokens

# Aggressive (high-throughput)
GPU: 2x A100 (80GB)
RAM: 64GB
Context: 8192 tokens
```

### 3. Caching Strategy

```bash
# Model caching (shared across instances)
volumes:
  - /shared/models:/models:ro

# Context caching (Anthropic-style)
# Not yet widely supported in open models
```

---

## See Also

- [Providers Guide](PROVIDERS.md) - Complete provider configuration reference
- [AgentMode Overview](AGENTMODE.md) - AgentMode architecture and usage
- [Production Deployment](PRODUCTION_DEPLOYMENT.md) - Production deployment best practices

---

## Support

- **Issues**: [GitHub Issues](https://github.com/iyulab/ironbees/issues)
- **Discussions**: [GitHub Discussions](https://github.com/iyulab/ironbees/discussions)

---

**Note**: Self-hosting LLMs requires significant GPU resources. Start with Ollama for testing, then scale to GPUStack or vLLM for production.
