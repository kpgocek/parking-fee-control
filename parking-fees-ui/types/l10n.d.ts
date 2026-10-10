declare module 'cs2/l10n' {
  export interface LocalizationContext {
    translate(key: string, fallback?: string): string | undefined;
  }

  export function useLocalization(): LocalizationContext;
}
