// Tunable numbers for the browser scripts, the way ArtistShopLimits holds them for the server.
// Imported by an absolute path, so a script can read it whatever page it was loaded from

// how long a filter bar waits after a control changes before it submits: long enough that
// ticking several checkboxes in a row is one page load, short enough not to feel stuck
export const submitOnChangeDelayMilliseconds = 300;

// how long a navigation has to be taking before the loading indicator appears
export const loadingIndicatorDelayMilliseconds = 250;

// how many artworks the lightbox fetches ahead of the one showing, each way, so a run of quick
// swipes finds pictures rather than black
export const lightboxArtworksFetchedAhead = 2;

// how long the lightbox rests on another artwork's picture before the page behind it goes there
// too: long enough that a run of swipes is one page load, not one each
export const lightboxPageCatchUpDelayMilliseconds = 400;
