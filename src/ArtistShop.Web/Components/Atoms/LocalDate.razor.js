// The same shape LocalDate.razor writes on the server, "22 Sep 2026", in the browser's time zone. Only
// the zone is the visitor's, not the wording, so the text changes only when the day really differs.
// Built from parts because en-GB now spells September "Sept"
const format = new Intl.DateTimeFormat("en-US", { day: "numeric", month: "short", year: "numeric" });

/** @param {Element} element */
function localize(element) {
  const time = element.querySelector("time");

  if (time === null || time.dateTime === "") {
    return;
  }

  const parts = Object.fromEntries(
    format.formatToParts(new Date(time.dateTime)).map((part) => [part.type, part.value])
  );
  time.textContent = `${parts.day} ${parts.month} ${parts.year}`;
}

customElements.define(
  "local-date",
  class extends HTMLElement {
    connectedCallback() {
      localize(this);
    }
  }
);

// an enhanced navigation patches an element already on the page in place, putting back the
// server's UTC text without connecting it again
Blazor.addEventListener("enhancedload", () => document.querySelectorAll("local-date").forEach(localize));
