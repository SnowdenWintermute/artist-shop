// The time of day LocalTime.razor writes on the server, in the browser's time zone, and in its language's
// clock, 12 or 24 hours
const format = new Intl.DateTimeFormat(undefined, { hour: "numeric", minute: "2-digit" });

/** @param {Element} element */
function localize(element) {
  const time = element.querySelector("time");

  if (time === null || time.dateTime === "") {
    return;
  }

  time.textContent = format.format(new Date(time.dateTime));
}

customElements.define(
  "local-time",
  class extends HTMLElement {
    connectedCallback() {
      localize(this);
    }
  }
);

// an enhanced navigation patches an element already on the page in place, putting back the
// server's UTC text without connecting it again
Blazor.addEventListener("enhancedload", () => document.querySelectorAll("local-time").forEach(localize));
