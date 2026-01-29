/**
 * Progress Service - Multi-step progress tracking
 *
 * Used for operations like /init that have multiple steps:
 * 1. Validating server URL format
 * 2. Creating workspace directories
 * 3. Testing server connection
 * 4. Creating example files
 */
import { EventEmitter } from 'events';

export type ProgressStepStatus = 'pending' | 'running' | 'completed' | 'failed';

export interface ProgressStep {
  name: string;
  status: ProgressStepStatus;
  message?: string;
  duration?: number;
  startTime?: number;
}

export interface ProgressState {
  steps: ProgressStep[];
  currentStepIndex: number;
  isComplete: boolean;
  isFailed: boolean;
  error?: string;
}

/**
 * Multi-step progress tracker
 */
export class MultiStepProgress extends EventEmitter {
  private steps: ProgressStep[] = [];
  private currentStepIndex = -1;
  private startTime = 0;

  /**
   * Initialize with step names
   */
  initialize(stepNames: string[]): void {
    this.steps = stepNames.map((name) => ({
      name,
      status: 'pending',
    }));
    this.currentStepIndex = -1;
    this.startTime = Date.now();
    this.emit('update', this.getState());
  }

  /**
   * Move to the next step
   */
  nextStep(message?: string): void {
    // Complete current step if running
    if (this.currentStepIndex >= 0 && this.steps[this.currentStepIndex]) {
      const current = this.steps[this.currentStepIndex];
      current.status = 'completed';
      if (current.startTime) {
        current.duration = Date.now() - current.startTime;
      }
    }

    // Start next step
    this.currentStepIndex++;
    if (this.currentStepIndex < this.steps.length) {
      const next = this.steps[this.currentStepIndex];
      next.status = 'running';
      next.startTime = Date.now();
      if (message) {
        next.message = message;
      }
    }

    this.emit('update', this.getState());
  }

  /**
   * Update current step message
   */
  updateMessage(message: string): void {
    if (this.currentStepIndex >= 0 && this.steps[this.currentStepIndex]) {
      this.steps[this.currentStepIndex].message = message;
      this.emit('update', this.getState());
    }
  }

  /**
   * Mark current step and overall progress as failed
   */
  fail(error: string): void {
    if (this.currentStepIndex >= 0 && this.steps[this.currentStepIndex]) {
      const current = this.steps[this.currentStepIndex];
      current.status = 'failed';
      current.message = error;
      if (current.startTime) {
        current.duration = Date.now() - current.startTime;
      }
    }
    this.emit('update', this.getState());
    this.emit('failed', error);
  }

  /**
   * Complete all remaining steps (for cleanup/success)
   */
  complete(): void {
    // Complete current step
    if (this.currentStepIndex >= 0 && this.steps[this.currentStepIndex]) {
      const current = this.steps[this.currentStepIndex];
      if (current.status === 'running') {
        current.status = 'completed';
        if (current.startTime) {
          current.duration = Date.now() - current.startTime;
        }
      }
    }

    this.emit('update', this.getState());
    this.emit('complete', this.getState());
  }

  /**
   * Get current state
   */
  getState(): ProgressState {
    const isComplete =
      this.steps.length > 0 &&
      this.steps.every((s) => s.status === 'completed');
    const isFailed = this.steps.some((s) => s.status === 'failed');

    return {
      steps: [...this.steps],
      currentStepIndex: this.currentStepIndex,
      isComplete,
      isFailed,
      error: isFailed
        ? this.steps.find((s) => s.status === 'failed')?.message
        : undefined,
    };
  }

  /**
   * Reset the progress tracker
   */
  reset(): void {
    this.steps = [];
    this.currentStepIndex = -1;
    this.startTime = 0;
    this.emit('update', this.getState());
  }

  /**
   * Get total elapsed time
   */
  getElapsedTime(): number {
    return this.startTime ? Date.now() - this.startTime : 0;
  }
}

// Singleton instance for global progress tracking
export const progressService = new MultiStepProgress();

/**
 * React hook for using progress service
 */
import { useState, useEffect } from 'react';

export function useProgress(): ProgressState & {
  initialize: (steps: string[]) => void;
  nextStep: (message?: string) => void;
  updateMessage: (message: string) => void;
  fail: (error: string) => void;
  complete: () => void;
  reset: () => void;
} {
  const [state, setState] = useState<ProgressState>(progressService.getState());

  useEffect(() => {
    const handleUpdate = (newState: ProgressState) => {
      setState(newState);
    };

    progressService.on('update', handleUpdate);

    return () => {
      progressService.off('update', handleUpdate);
    };
  }, []);

  return {
    ...state,
    initialize: (steps) => progressService.initialize(steps),
    nextStep: (message) => progressService.nextStep(message),
    updateMessage: (message) => progressService.updateMessage(message),
    fail: (error) => progressService.fail(error),
    complete: () => progressService.complete(),
    reset: () => progressService.reset(),
  };
}

export default progressService;
