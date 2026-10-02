// an element ReconnectModal.razor renders, as the type the script uses
/**
 * @template {Element} T
 * @param {string} id
 * @param {new () => T} type
 * @returns {T}
 */
function findById(id, type) {
  const element = document.getElementById(id);

  if (!(element instanceof type)) {
    throw new Error(`ReconnectModal.razor is missing its #${id}.`);
  }

  return element;
}

// Set up event handlers
const reconnectModal = findById("components-reconnect-modal", HTMLDialogElement);
reconnectModal.addEventListener(
  "components-reconnect-state-changed",
  handleReconnectStateChanged
);

const retryButton = findById("components-reconnect-button", HTMLButtonElement);
retryButton.addEventListener("click", retry);

const resumeButton = findById("components-resume-button", HTMLButtonElement);
resumeButton.addEventListener("click", resume);

// Refreshing or leaving the page closes the connection, and Blazor reports that like any other
// disconnect, so showing the dialog at once makes it flash on every refresh. Only a connection that
// is still down after this long is worth interrupting for.
const SHOW_DELAY_MILLISECONDS = 1500;
/** @type {ReturnType<typeof setTimeout> | undefined} */
let showTimeout;

/** @param {CustomEvent<{ state: string }>} event */
function handleReconnectStateChanged(event) {
  if (event.detail.state === "show") {
    clearTimeout(showTimeout);
    showTimeout = setTimeout(
      () => reconnectModal.showModal(),
      SHOW_DELAY_MILLISECONDS
    );
  } else if (event.detail.state === "hide") {
    clearTimeout(showTimeout);
    reconnectModal.close();
  } else if (event.detail.state === "failed") {
    document.addEventListener(
      "visibilitychange",
      retryWhenDocumentBecomesVisible
    );
  } else if (event.detail.state === "rejected") {
    location.reload();
  }
}

async function retry() {
  document.removeEventListener(
    "visibilitychange",
    retryWhenDocumentBecomesVisible
  );

  try {
    // Reconnect will asynchronously return:
    // - true to mean success
    // - false to mean we reached the server, but it rejected the connection (e.g., unknown circuit ID)
    // - exception to mean we didn't reach the server (this can be sync or async)
    const successful = await Blazor.reconnect();
    if (!successful) {
      // We have been able to reach the server, but the circuit is no longer available.
      // We'll reload the page so the user can continue using the app as quickly as possible.
      const resumeSuccessful = await Blazor.resumeCircuit();
      if (!resumeSuccessful) {
        location.reload();
      } else {
        reconnectModal.close();
      }
    }
  } catch (err) {
    // We got an exception, server is currently unavailable
    document.addEventListener(
      "visibilitychange",
      retryWhenDocumentBecomesVisible
    );
  }
}

async function resume() {
  try {
    const successful = await Blazor.resumeCircuit();
    if (!successful) {
      location.reload();
    }
  } catch {
    reconnectModal.classList.replace(
      "components-reconnect-paused",
      "components-reconnect-resume-failed"
    );
  }
}

async function retryWhenDocumentBecomesVisible() {
  if (document.visibilityState === "visible") {
    await retry();
  }
}
