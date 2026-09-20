// The artist clicked one button, so that button is what goes busy: no overlay, nothing
// covering the page. Only enhanced forms, because their post ends in enhancedload; an island's
// form is handled over the circuit and would leave the button spinning
/** @type {HTMLElement | null} */
let busyButton = null;

function clearBusyButton() {
  busyButton?.removeAttribute("data-busy");
  busyButton?.removeAttribute("aria-busy");
  busyButton = null;
}

document.addEventListener("submit", (event) => {
  const form = event.target;

  if (!(form instanceof HTMLFormElement) || !form.hasAttribute("data-enhance")) {
    return;
  }

  clearBusyButton();

  // no submitter when script submitted the form, as the filter bar does: nothing was clicked
  if (!(event.submitter instanceof HTMLElement)) {
    return;
  }

  busyButton = event.submitter;
  busyButton.setAttribute("data-busy", "");
  busyButton.setAttribute("aria-busy", "true");
});

Blazor.addEventListener("enhancedload", clearBusyButton);
