/**
 * returnUrl comes from the query string, so only in-app paths are accepted ("/x", never "//host" or "https://…"),
 * and never the auth screens themselves.
 */
export function safeReturnUrl(value: string | null | undefined): string {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\')) {
    return '/';
  }
  if (value.startsWith('/login') || value.startsWith('/change-password')) {
    return '/';
  }
  return value;
}
