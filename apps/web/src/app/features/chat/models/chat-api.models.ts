export interface ChatRequest {
  message: string;
}

export interface RagSource {
  sourcePath: string;
  heading: string | null;
}

export interface ChatResponse {
  message: string;
  toolsUsed: string[];
  ragSources: RagSource[];
}
