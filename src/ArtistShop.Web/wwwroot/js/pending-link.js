// A clicked link carries data-pending, which app.css fades, until its page arrives, so a slow page
// still shows the click landed. Blazor decides which clicks navigate: it starts the navigation inside
// its own click handler, so a link is marked only if a navigation starts during its click. A link a
// script handles, a download or a new tab never starts one

/** @type {Element | null} */
let clickedLink = null;

// on the way down, so it runs before Blazor's handler on the document
document.addEventListener(
  "click",
  (event) => {
    clickedLink = event.target instanceof Element ? event.target.closest("a[href]") : null;
    // once every handler for this click has run
    setTimeout(() => (clickedLink = null), 0);
  },
  { capture: true }
);

function clearMarks() {
  for (const link of document.querySelectorAll("a[data-pending]")) {
    link.removeAttribute("data-pending");
  }
}

// a second click before the first page arrives cuts the first navigation short
Blazor.addEventListener("enhancednavigationstart", () => {
  clearMarks();
  clickedLink?.setAttribute("data-pending", "");
});

// Blazor keeps the elements the old and new pages share, like the nav bar's links, so a mark would stay
Blazor.addEventListener("enhancednavigationend", clearMarks);

// a page left by a full load while a link was marked comes back from the back-forward cache marked
window.addEventListener("pageshow", (event) => {
  if (event.persisted) {
    clearMarks();
  }
});
