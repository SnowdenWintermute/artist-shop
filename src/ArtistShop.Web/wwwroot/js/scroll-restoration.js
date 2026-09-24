// Returns to where each page was scrolled, on Back and Forward. The browser does this itself for
// whole pages, but under enhanced navigation it restores the position the moment the address
// changes, while the page being left is still showing, so a long post comes back at the top. This
// takes over: it notes where a page was when it's left, and scrolls there once the page is back.
// A new visit by an ordinary link still starts at the top; a link marked data-restores-scroll, such
// as the artwork picker's Back links, returns to where that page was left too.
//
// Needed because neither side handles it (checked 2026-09-24, .NET 10.0.11 and 11.0 RC1). Blazor
// closed its issue leaving Back to the browser: https://github.com/dotnet/aspnetcore/issues/51646
// (PR #60296).
// The browser restores once, against the page still showing, and clamps it to that page's height:
// https://bugzilla.mozilla.org/show_bug.cgi?id=1442958. If either is fixed, this can go

history.scrollRestoration = "manual";

// per tab, like the history it follows, so a reload finds it. One key per address rather than one
// shared object, since the artwork picker's frame writes here too and would overwrite the page's
const storageKey = (/** @type {string} */ address) => `scroll-position:${address}`;

const currentAddress = () => location.pathname + location.search;

// the page shown now, which the address has already moved past when a navigation starts
let shownAddress = currentAddress();

// whether the page being loaded should return to where it was left
let restoresOnLoad = false;

function remember() {
  try {
    sessionStorage.setItem(storageKey(shownAddress), String(Math.round(window.scrollY)));
  } catch {
    // storage turned off: pages start at the top, as they would without this
  }
}

function restore() {
  let saved = null;

  try {
    saved = sessionStorage.getItem(storageKey(currentAddress()));
  } catch {
    return;
  }

  if (saved !== null) {
    window.scrollTo(0, Number(saved));
  }
}

// before Blazor scrolls anywhere or swaps anything in, so the position is still the page's own
Blazor.addEventListener("enhancednavigationstart", remember);

// every navigation starts with one of these, so a choice is never left over from the last one
window.addEventListener("popstate", () => (restoresOnLoad = true));
document.addEventListener(
  "click",
  (event) => {
    const link = event.target instanceof Element ? event.target.closest("a[href]") : null;

    if (link !== null) {
      restoresOnLoad = link.hasAttribute("data-restores-scroll");
    }
  },
  { capture: true }
);
document.addEventListener("submit", () => (restoresOnLoad = false), { capture: true });

Blazor.addEventListener("enhancedload", () => {
  shownAddress = currentAddress();

  if (restoresOnLoad) {
    restoresOnLoad = false;
    // Blazor scrolls a page reached by a link to the top just before this event (.NET 11), so
    // this comes after it; the frame's wait also covered .NET 10, which scrolled at the click
    requestAnimationFrame(restore);
  }
});

// a whole page load: leaving for another site, a reload, or a link Blazor doesn't enhance
window.addEventListener("pagehide", remember);

// Back to this site from another, or a reload, which the browser would restore itself if this
// hadn't taken over. The page's markup is all here by now, as this runs once it's parsed
const [pageLoad] = performance.getEntriesByType("navigation");
if (
  pageLoad instanceof PerformanceNavigationTiming &&
  (pageLoad.type === "reload" || pageLoad.type === "back_forward")
) {
  restore();
}
