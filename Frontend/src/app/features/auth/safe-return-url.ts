/**
 * returnUrl comes from the query string, so it is untrusted (open-redirect protection).
 * Accepted: an in-app path only – starts with "/", but not "//" (protocol-relative URL), not "/\" (browsers treat
 * it like "//"), and contains no "://" anywhere. The auth screens themselves are not valid targets.
 * Anything else → the home page.
 */
const HOME = '/';
const AUTH_SCREENS = ['/login', '/change-password', '/account/change-password'];

export function safeReturnUrl(value: string | null | undefined): string {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\') || value.includes('://')) {
    return HOME;
  }
  if (AUTH_SCREENS.some((screen) => value.startsWith(screen))) {
    return HOME;
  }
  return value;
}
