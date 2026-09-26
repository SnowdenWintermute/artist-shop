// One post at a time, and the button that started it goes busy where the artist is looking, for
// every static form: an enhanced one, whose answer Blazor patches in before raising enhancedload,
// which is where the marks come off, and a plain one, which loads a whole new page. An island's
// form is left alone: its buttons go busy through ButtonBasic's Busy, and nothing here would ever
// clear a mark on one
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

/**
 * @param {HTMLFormElement} form
 * @param {HTMLElement} submitter
 */
function mark(form, submitter) {
  clearMarks();

  submittingForm = form;
  busyButton = submitter;
  busyButton.setAttribute("data-busy", "");
  busyButton.setAttribute("aria-busy", "true");
}

document.addEventListener("submit", (event) => {
  const form = event.target;

  if (!(form instanceof HTMLFormElement)) {
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

  if (form.hasAttribute("data-enhance")) {
    mark(form, event.submitter);
    return;
  }

  // a download leaves the page where it is, so nothing would clear the marks
  if (form.hasAttribute("data-downloads")) {
    return;
  }

  // Anything else is an island's form, whose submission Blazor cancels to handle it over the
  // circuit, or a plain one, which the browser goes on to post. The server renders both as
  // method="post", so which it is is only known once every listener, Blazor's among them, has
  // seen the event
  const submitter = event.submitter;

  setTimeout(() => {
    if (!event.defaultPrevented) {
      mark(form, submitter);
    }
  }, 0);
});

Blazor.addEventListener("enhancedload", clearMarks);

// Back can restore a plain form's page from the browser's cache just as it was left, busy
window.addEventListener("pageshow", (event) => {
  if (event.persisted) {
    clearMarks();
  }
});
