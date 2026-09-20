// One post at a time, and the button that started it goes busy where the artist is looking.
// Both are about an enhanced form that is posting, so one script owns them, and Blazor raises
// enhancedload once the answer has been patched in, which is where the marks come off
/** @type {HTMLFormElement | null} */
let submittingForm = null;
/** @type {HTMLElement | null} */
let busyButton = null;

function clearMarks() {
  busyButton?.removeAttribute("data-busy");
  busyButton?.removeAttribute("aria-busy");
  submittingForm = null;
  busyButton = null;
}

document.addEventListener("submit", (event) => {
  const form = event.target;

  // an island's form is handled over the circuit and raises no enhancedload, so nothing
  // here would ever clear
  if (!(form instanceof HTMLFormElement) || !form.hasAttribute("data-enhance")) {
    return;
  }

  // nothing was clicked, so a script submitted it, as the filter bar does after a control
  // changes. That post is the page catching up with the controls, so it is never blocked
  if (!(event.submitter instanceof HTMLElement)) {
    clearMarks();
    return;
  }

  // already posting. The busy button is only grayed to the mouse, so Enter on it, or in one of
  // the form's text fields, would post a second time. A post that dies without an enhancedload
  // leaves this set and the page needs reloading
  if (form === submittingForm) {
    event.preventDefault();
    return;
  }

  clearMarks();

  submittingForm = form;
  busyButton = event.submitter;
  busyButton.setAttribute("data-busy", "");
  busyButton.setAttribute("aria-busy", "true");
});

Blazor.addEventListener("enhancedload", clearMarks);
