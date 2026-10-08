// Tunable numbers for the browser scripts, the way ArtistShopLimits holds them for the server.
// Imported by an absolute path, so a script can read it whatever page it was loaded from

// how long a navigation has to be taking before the loading indicator appears
export const loadingIndicatorDelayMilliseconds = 250;

// how many works the lightbox fetches ahead of the one showing, each way, so a run of quick
// swipes finds pictures rather than black
export const lightboxWorksFetchedAhead = 2;

// how long the lightbox rests on another work's picture before the page behind it goes there
// too: long enough that a run of swipes is one page load, not one each
export const lightboxPageCatchUpDelayMilliseconds = 400;

// how long the lightbox takes to fade in, and out. PhotoSwipe only puts the full picture in once
// the fade in has ended, so until then its stand-in shows
export const lightboxFadeMilliseconds = 150;

// how long a finger rests on something before it's picked up to drag, as a phone's own long press
export const gestureHoldMilliseconds = 500;

// how far a finger can wander while holding before it counts as scrolling instead
export const gestureHoldTolerancePixels = 10;

// how far a mouse moves with its button down before a drag begins, so a click stays a click
export const gestureMouseDragPixels = 5;

// how long after a gesture ends its release's click is swallowed. One comes at once if it comes at
// all, so this only needs to outlast it, and stay short of a click the artist makes next
export const gestureClickSwallowMilliseconds = 300;

// how near the top or bottom of the window a drag scrolls the page, and how fast at the very edge
export const dragScrollEdgePixels = 60;
export const dragScrollMaxPixelsPerFrame = 16;
