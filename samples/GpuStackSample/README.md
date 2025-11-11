# GPU-Stack Sample

This sample demonstrates how to use Ironbees with GPU-Stack for local GPU-powered LLM inference.

## Overview

GPU-Stack is an open-source GPU cluster manager for running AI models locally. This sample shows how to integrate Ironbees with GPU-Stack's OpenAI-compatible API.

## Prerequisites

1. **GPU-Stack Server**: Install and run GPU-Stack on your local machine or cluster
   - Installation: https://docs.gpustack.ai/latest/installation/
   - Quick start: `curl -sfL https://get.gpustack.ai | sh -`

2. **Deploy a Model**: Deploy an LLM model on GPU-Stack
   ```bash
   # Example: Deploy Llama 3.2
   gpustack model deploy llama3.2
   ```

3. **Get API Key**: Generate an API key from GPU-Stack dashboard
   - Default endpoint: http://localhost:8080
   - Navigate to Settings → API Keys

## Configuration

1. Copy `.env.example` to `.env` (or use the existing `.env`):
   ```bash
   cp .env.example .env
   ```

2. Edit `.env` and set your GPU-Stack configuration:
   ```env
   GPUSTACK_ENDPOINT=http://localhost:8080
   GPUSTACK_API_KEY=your_api_key_here
   GPUSTACK_MODEL=llama3.2
   ```

## Running the Sample

```bash
# From the sample directory
dotnet run

# Or from the solution root
dotnet run --project samples/GpuStackSample/GpuStackSample.csproj
```

## Features Demonstrated

1. **Agent Loading**: Automatic discovery and loading of agents from filesystem
2. **Explicit Agent Selection**: Direct agent invocation by name
3. **Automatic Agent Selection**: Keyword-based routing to appropriate agents
4. **Streaming Responses**: Real-time token streaming from GPU-Stack
5. **Agent Score Comparison**: View confidence scores for all agents

## Architecture

```
┌─────────────────────────────────────────────────────┐
│   Ironbees Framework                                │
│   • FileSystemAgentLoader                           │
│   • KeywordAgentSelector                            │
│   • AgentOrchestrator                               │
├─────────────────────────────────────────────────────┤
│   GpuStackAdapter (OpenAI-Compatible)               │
│   • Endpoint: /v1-openai                            │
│   • Authentication: Bearer token                    │
│   • Streaming: Server-Sent Events                   │
├─────────────────────────────────────────────────────┤
│   GPU-Stack Server                                  │
│   • Local GPU inference                             │
│   • Model management                                │
│   • OpenAI-compatible API                           │
└─────────────────────────────────────────────────────┘
```

## Supported Models

GPU-Stack supports a wide range of models through various backends:
- **vLLM**: Llama, Mistral, Qwen, Yi, etc.
- **llama.cpp**: GGUF models
- **Ascend MindIE**: Huawei Ascend NPU models

See https://docs.gpustack.ai/latest/user-guide/model-management/ for the full list.

## Troubleshooting

### Connection Error
```
❌ Unexpected error: The HTTP request failed...
```
- Verify GPU-Stack server is running: `curl http://localhost:8080/health`
- Check endpoint in `.env` matches your GPU-Stack server

### Authentication Error
```
❌ Unexpected error: 401 Unauthorized
```
- Verify API key is correct
- Generate new API key from GPU-Stack dashboard

### Model Not Found
```
❌ Unexpected error: Model 'xxx' not found
```
- List deployed models: `gpustack model list`
- Deploy the model: `gpustack model deploy <model-name>`
- Update `GPUSTACK_MODEL` in `.env`

## Benefits of GPU-Stack

- ✅ **Local Inference**: No cloud API costs, complete data privacy
- ✅ **GPU Acceleration**: Leverage local NVIDIA, AMD, or Ascend GPUs
- ✅ **OpenAI Compatible**: Drop-in replacement for OpenAI API
- ✅ **Model Flexibility**: Support for various model formats and backends
- ✅ **Cluster Support**: Scale across multiple GPU nodes

## Learn More

- GPU-Stack Documentation: https://docs.gpustack.ai/
- GPU-Stack GitHub: https://github.com/gpustack/gpustack
- Ironbees Documentation: https://github.com/iyulab/ironbees
