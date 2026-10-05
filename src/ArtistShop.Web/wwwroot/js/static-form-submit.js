// One post at a time, and the button that started it goes busy where the artist is looking, for
// every static form: an enhanced one, whose answer Blazor patches in before raising enhancedload,
// which is where the marks come off, and a plain one, which loads a whole new page. An island's
// form is left alone: its buttons go busy through ButtonBasic's Busy, and nothing here would ever
// clear a mark on one
/** @type {HTMLFormElement | null} */
let submittingForm = null;
/** @type {HTMLElement | null} */
let busyButton = null;
// A form marked data-waits-for-pending posts what the page is still working on: the checks a
// SubmitOnChange form is about to put in the address, or the images an ImagesField is still
// uploading. Clicked while anything carries data-pending, its button goes busy and it posts once
// nothing does, unless an upload has failed, so the artist sees the error before anything is
// saved without that image
/** @type {HTMLElement | null} */
let waitingButton = null;

// a finished upload raises no enhancedload, so while a button waits this watches the marks too
const pendingObserver = new MutationObserver(postWaiting);

/** @param {HTMLElement} button */
function showBusy(button) {
  button.setAttribute("data-busy", "");
  button.setAttribute("aria-busy", "true");
}

/** @param {HTMLElement} button */
function clearBusy(button) {
  button.removeAttribute("data-busy");
  button.removeAttribute("aria-busy");
}

function clearMarks() {
  if (busyButton !== null) {
    clearBusy(busyButton);
  }

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
  showBusy(busyButton);
}

function isPagePending() {
  return document.querySelector("[data-pending]") !== null;
}

/** @param {HTMLElement} button */
function startWaiting(button) {
  waitingButton = button;
  showBusy(button);
  pendingObserver.observe(document.body, {
    subtree: true,
    // a pending element taken off the page stops being pending too
    childList: true,
    attributeFilter: ["data-pending"],
  });
}

function stopWaiting() {
  if (waitingButton !== null) {
    clearBusy(waitingButton);
  }

  waitingButton = null;
  pendingObserver.disconnect();
}

function postWaiting() {
  const button = waitingButton;

  if (button === null) {
    return;
  }

  if (isPagePending()) {
    // patching the page in may have taken the marks off
    showBusy(button);
    return;
  }

  stopWaiting();

  if (button.closest("form")?.querySelector("[data-failed]")) {
    return;
  }

  // one the page answered by disabling, like Add selected once nothing is checked, has nothing to post
  if (button.isConnected && !button.hasAttribute("disabled") && button instanceof HTMLButtonElement) {
    button.form?.requestSubmit(button);
  }
}

// Capture, so this runs before Blazor's own listener on document, which posts an enhanced form at
// once unless the event has already been cancelled. Blazor.web.js loads first, so listening in
// the bubble phase like it would leave the cancelling below too late
document.addEventListener(
  "submit",
  (event) => {
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

    if (form.hasAttribute("data-waits-for-pending") && isPagePending()) {
      event.preventDefault();
      startWaiting(event.submitter);
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
  },
  { capture: true }
);

// SubmitOnChange.razor.js takes its pending marks off on this event too, so the waiting post looks
// once every listener has run, whatever their order
Blazor.addEventListener("enhancedload", () => {
  clearMarks();
  setTimeout(postWaiting, 0);
});

// Back can restore a plain form's page from the browser's cache just as it was left, busy
window.addEventListener("pageshow", (event) => {
  if (event.persisted) {
    clearMarks();
  }
});
