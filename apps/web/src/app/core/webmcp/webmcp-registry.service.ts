import { DestroyRef, Injectable, isDevMode, signal } from '@angular/core';
import { WebMcpModelContext, WebMcpTool } from './webmcp.types';

export interface WebMcpRegistration {
  readonly ready: Promise<void>;
  dispose(): void;
}

export interface WebMcpRegistrationError {
  readonly toolName: string;
  readonly message: string;
}

export interface WebMcpDiagnosticsState {
  readonly webMcpApiAvailable: boolean;
  readonly modelContextAvailable: boolean;
  readonly registeredToolNames: readonly string[];
  readonly registrationErrors: readonly WebMcpRegistrationError[];
}

interface ActiveRegistration {
  ready: Promise<void>;
  dispose(): void;
  readonly controller: AbortController;
  readonly registrationErrors: Map<string, string>;
  readonly registeredToolNames: Set<string>;
  readonly toolNames: string[];
}

@Injectable({ providedIn: 'root' })
export class WebMcpRegistryService {
  private readonly registrationsByScope = new Map<string, ActiveRegistration>();
  private readonly registrationsByToolName = new Map<string, ActiveRegistration>();
  private readonly diagnosticsState = signal<WebMcpDiagnosticsState>({
    webMcpApiAvailable: false,
    modelContextAvailable: false,
    registeredToolNames: [],
    registrationErrors: []
  });
  private hasLoggedSupported = false;
  readonly diagnostics = this.diagnosticsState.asReadonly();

  constructor() {
    this.refreshDiagnostics();
  }

  register(scope: string, tools: readonly WebMcpTool[], destroyRef?: DestroyRef): WebMcpRegistration {
    const modelContext = this.refreshDiagnostics();
    if (!modelContext) {
      return { ready: Promise.resolve(), dispose: () => undefined };
    }
    this.logSupportedOnce();

    this.registrationsByScope.get(scope)?.dispose();
    const uniqueTools = this.uniqueTools(tools);
    const controller = new AbortController();
    let registration: ActiveRegistration;
    const dispose = () => this.dispose(scope, registration);

    registration = {
      controller,
      registrationErrors: new Map<string, string>(),
      registeredToolNames: new Set<string>(),
      toolNames: uniqueTools.map((tool) => tool.name),
      ready: Promise.resolve(),
      dispose
    };

    for (const toolName of registration.toolNames) {
      this.registrationsByToolName.get(toolName)?.dispose();
      this.registrationsByToolName.set(toolName, registration);
    }
    this.registrationsByScope.set(scope, registration);
    destroyRef?.onDestroy(dispose);
    this.refreshDiagnostics();

    registration.ready = Promise.all(
      uniqueTools.map((tool) => Promise.resolve()
        .then(() => modelContext.registerTool(tool, { signal: controller.signal }))
        .then(() => this.logRegistration(scope, registration, tool.name))
        .catch((error: unknown) => this.logRegistrationFailure(scope, registration, tool.name, error)))
    ).then(() => undefined);

    return registration;
  }

  private dispose(scope: string, registration: ActiveRegistration): void {
    if (this.registrationsByScope.get(scope) !== registration) {
      return;
    }

    this.registrationsByScope.delete(scope);
    for (const toolName of registration.toolNames) {
      if (this.registrationsByToolName.get(toolName) === registration) {
        this.registrationsByToolName.delete(toolName);
      }
    }
    registration.controller.abort();
    for (const toolName of registration.registeredToolNames) {
      this.logUnregistration(toolName);
    }
    this.refreshDiagnostics();
  }

  private refreshDiagnostics(): WebMcpModelContext | null {
    const modelContextAvailable = typeof document !== 'undefined' && document.modelContext !== undefined;
    const modelContext = this.getModelContext();
    const activeRegistrations = [...this.registrationsByScope.values()];

    this.diagnosticsState.set({
      webMcpApiAvailable: modelContext !== null,
      modelContextAvailable,
      registeredToolNames: activeRegistrations
        .flatMap((registration) => [...registration.registeredToolNames])
        .sort(),
      registrationErrors: activeRegistrations
        .flatMap((registration) => [...registration.registrationErrors.entries()])
        .map(([toolName, message]) => ({ toolName, message }))
        .sort((first, second) => first.toolName.localeCompare(second.toolName))
    });

    return modelContext;
  }

  private getModelContext(): WebMcpModelContext | null {
    if (typeof document === 'undefined') {
      return null;
    }

    const modelContext = document.modelContext;
    return modelContext && typeof modelContext.registerTool === 'function' ? modelContext : null;
  }

  private uniqueTools(tools: readonly WebMcpTool[]): WebMcpTool[] {
    const toolsByName = new Map<string, WebMcpTool>();
    for (const tool of tools) {
      toolsByName.set(tool.name, tool);
    }
    return [...toolsByName.values()];
  }

  private logSupportedOnce(): void {
    if (isDevMode() && !this.hasLoggedSupported) {
      this.hasLoggedSupported = true;
      console.debug('[WebMCP] supported');
    }
  }

  private logRegistration(scope: string, registration: ActiveRegistration, toolName: string): void {
    if (registration.controller.signal.aborted || this.registrationsByScope.get(scope) !== registration) {
      return;
    }

    registration.registeredToolNames.add(toolName);
    registration.registrationErrors.delete(toolName);
    this.refreshDiagnostics();
    if (isDevMode()) {
      console.debug(`[WebMCP] registered: ${toolName}`);
    }
  }

  private logUnregistration(toolName: string): void {
    if (isDevMode()) {
      console.debug(`[WebMCP] unregistered: ${toolName}`);
    }
  }

  private logRegistrationFailure(
    scope: string,
    registration: ActiveRegistration,
    toolName: string,
    error: unknown
  ): void {
    if (registration.controller.signal.aborted || this.registrationsByScope.get(scope) !== registration) {
      return;
    }

    registration.registrationErrors.set(toolName, this.registrationErrorMessage(error));
    this.refreshDiagnostics();
    if (isDevMode()) {
      console.warn(`[WebMCP] Registration failed for ${toolName}.`);
    }
  }

  private registrationErrorMessage(error: unknown): string {
    if (error instanceof Error && error.message.trim()) {
      return error.message.trim();
    }

    return 'Registration failed.';
  }
}
