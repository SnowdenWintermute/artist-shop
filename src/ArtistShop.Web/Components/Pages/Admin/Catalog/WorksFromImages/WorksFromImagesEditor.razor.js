// how far the browser's clock is ahead of UTC, so the island can write the time as the artist reads it
export function utcOffsetMinutes() {
  return -new Date().getTimezoneOffset();
}
