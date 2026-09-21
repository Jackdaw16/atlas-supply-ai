import { InjectionToken } from '@angular/core';

export interface DemoContactConfig {
  demoContactEmail: string;
}

export const DEMO_CONTACT_CONFIG = new InjectionToken<DemoContactConfig>('DEMO_CONTACT_CONFIG');
