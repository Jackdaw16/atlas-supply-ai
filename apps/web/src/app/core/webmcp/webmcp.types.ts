export interface WebMcpExecutionOptions {
  signal: AbortSignal;
}

export interface WebMcpTool {
  name: string;
  description: string;
  inputSchema: Record<string, unknown>;
  annotations: {
    readOnlyHint: true;
  };
  execute: (input: Record<string, unknown>, options: WebMcpExecutionOptions) => Promise<unknown>;
}

export interface WebMcpRegisterToolOptions {
  signal?: AbortSignal;
}

export interface WebMcpModelContext {
  registerTool(tool: WebMcpTool, options?: WebMcpRegisterToolOptions): Promise<void>;
}

declare global {
  interface Document {
    modelContext?: WebMcpModelContext;
  }
}
