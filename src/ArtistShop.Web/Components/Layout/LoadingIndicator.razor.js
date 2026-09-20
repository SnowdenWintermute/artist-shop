import { loadingIndicatorDelayMilliseconds } from "/js/app-consts.js";

// Blazor raises these for every enhanced navigation, which is any internal link as well as a
// form marked data-enhance. The flag goes on the document element so one indicator serves the
// whole app, wherever the navigation started
let pending;

Blazor.addEventListener("enhancednavigationstart", () => {
  clearTimeout(pending);

  // a navigation that finishes first never shows it, so a fast page doesn't flash a spinner
  pending = setTimeout(
    () => document.documentElement.setAttribute("data-loading", ""),
    loadingIndicatorDelayMilliseconds
  );
});

Blazor.addEventListener("enhancedload", () => {
  clearTimeout(pending);
  document.documentElement.removeAttribute("data-loading");
});
