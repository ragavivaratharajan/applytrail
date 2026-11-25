import { HttpErrorResponse } from '@angular/common/http';

/** A failed API call in a form the screens can show directly. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly title: string,
    readonly detail?: string,
    readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(title);
    this.name = 'ApiError';
  }

  /** Every message worth showing, field errors first. */
  get messages(): string[] {
    const fields = Object.values(this.fieldErrors).flat();
    if (fields.length > 0) return fields;
    return [this.detail ? `${this.title} ${this.detail}` : this.title];
  }

  static from(error: unknown): ApiError {
    if (error instanceof ApiError) return error;
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) {
        return new ApiError(
          0,
          'Cannot reach the server.',
          'Check that the API is running and try again.',
        );
      }
      const body = (error.error ?? {}) as {
        title?: string;
        detail?: string;
        errors?: Record<string, string[]>;
      };
      const fieldErrors: Record<string, string[]> = {};
      for (const [key, value] of Object.entries(body.errors ?? {})) {
        fieldErrors[key.charAt(0).toLowerCase() + key.slice(1)] = Array.isArray(value)
          ? value
          : [String(value)];
      }
      const fallback =
        error.status === 429
          ? 'Too many attempts. Wait a minute and try again.'
          : 'Something went wrong.';
      return new ApiError(error.status, body.title ?? fallback, body.detail, fieldErrors);
    }
    return new ApiError(-1, 'Something went wrong.');
  }
}

export const errorMessage = (error: unknown): string => ApiError.from(error).messages.join(' ');
