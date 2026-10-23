// Where the page is scrolled to after each navigation, the one place that decides it:
// - Back and Forward: where that page was left
// - a link: the top, as Blazor scrolls a new page there
// - a link marked data-restores-scroll, such as the work picker's Back links: where that page was left
// - a page link (Lists/PageLinks.razor), which Blazor leaves where it was, as only the query changes:
//   the top, when it's marked data-scrolls-to-top, as its list is the page's top component. Lower down
//   a page, such as a grid under a heading, where it was
// - a form submit: left where it was
//
// Back and Forward: the browser does this itself for whole pages, but under enhanced navigation it
// restores the position the moment the address changes, while the page being left is still showing,
// so a long post comes back at the top. This takes over: it notes where a page was when it's left,
// and scrolls there once the page is back.
//
// Needed because neither side handles it (checked 2026-09-24, .NET 10.0.11 and 11.0 RC1). Blazor
// closed its issue leaving Back to the browser: https://github.com/dotnet/aspnetcore/issues/51646
// (PR #60296).
// The browser restores once, against the page still showing, and clamps it to that page's height:
// https://bugzilla.mozilla.org/show_bug.cgi?id=1442958. If either is fixed, this can go
//
// A whole page load, a reload or Forward back into the site, is left to the browser, which restores
// before the page is first drawn; this would only manage it after. So the browser's restore is turned
// off for the page's history entry once the page has loaded, and on again as the page goes

// the history entries an enhanced navigation adds take this from the page's
function takeOver() {
  history.scrollRestoration = "manual";
}

// after the browser's own restore, which it may still be making during the load event
window.addEventListener("load", () => setTimeout(takeOver));

// per tab, like the history it follows, so a reload finds it. One key per address rather than one
// shared object, since the work picker's frame writes here too and would overwrite the page's
const storageKey = (/** @type {string} */ address) => `scroll-position:${address}`;

const currentAddress = () => location.pathname + location.search;

// the page shown now, which the address has already moved past when a navigation starts
let shownAddress = currentAddress();

// whether the page being loaded should return to where it was left
let restoresOnLoad = false;

// whether the page being loaded starts at the top, though Blazor leaves it where it was
let scrollsToTopOnLoad = false;

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
window.addEventListener("popstate", () => {
  restoresOnLoad = true;
  scrollsToTopOnLoad = false;
});
document.addEventListener(
  "click",
  (event) => {
    const link = event.target instanceof Element ? event.target.closest("a[href]") : null;

    if (link !== null) {
      restoresOnLoad = link.hasAttribute("data-restores-scroll");
      scrollsToTopOnLoad = link.hasAttribute("data-scrolls-to-top");
      // a link followed before the page finished loading
      takeOver();
    }
  },
  { capture: true }
);
document.addEventListener(
  "submit",
  () => {
    restoresOnLoad = false;
    scrollsToTopOnLoad = false;
    takeOver();
  },
  { capture: true }
);

Blazor.addEventListener("enhancedload", () => {
  shownAddress = currentAddress();

  if (restoresOnLoad) {
    restoresOnLoad = false;
    // Blazor scrolls a page reached by a link to the top just before this event (.NET 11), so
    // this comes after it; the frame's wait also covered .NET 10, which scrolled at the click
    requestAnimationFrame(restore);
  } else if (scrollsToTopOnLoad) {
    scrollsToTopOnLoad = false;
    window.scrollTo(0, 0);
  }
});

// a whole page load: leaving for another site, a reload, or a link Blazor doesn't enhance. Coming
// back to this entry, the browser restores it
window.addEventListener("pagehide", () => {
  remember();
  history.scrollRestoration = "auto";
});

// the page kept whole in the back/forward cache, which runs nothing else again
window.addEventListener("pageshow", (event) => {
  if (event.persisted) {
    takeOver();
  }
});

// A whole page load into an entry the browser was told to leave alone: one an enhanced navigation
// reached before a reload, say. Only this can restore it, once the page is drawn. The page's markup
// is all here by now, as this runs once it's parsed
const [pageLoad] = performance.getEntriesByType("navigation");
if (
  pageLoad instanceof PerformanceNavigationTiming &&
  (pageLoad.type === "reload" || pageLoad.type === "back_forward") &&
  history.scrollRestoration === "manual"
) {
  restore();
}
