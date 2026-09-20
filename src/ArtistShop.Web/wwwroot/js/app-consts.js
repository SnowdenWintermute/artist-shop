// Tunable numbers for the browser scripts, the way CatalogLimits holds them for the server.
// Imported by an absolute path, so a script can read it whatever page it was loaded from

// how long a filter bar waits after a control changes before it submits: long enough that
// ticking several checkboxes in a row is one page load, short enough not to feel stuck
export const submitOnChangeDelayMilliseconds = 300;

// how long a navigation has to be taking before the loading indicator appears
export const loadingIndicatorDelayMilliseconds = 250;
