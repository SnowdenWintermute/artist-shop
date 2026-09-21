// Which of an artwork's images is showing. They are all in the page already, so this moves a
// marker rather than building any url: the server wrote each picture's srcset and blur once
customElements.define(
  "artwork-gallery",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;

    connectedCallback() {
      this.#listeners = new AbortController();
      this.addEventListener("click", (event) => this.#onClick(event), {
        signal: this.#listeners.signal,
      });
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }

    /** @param {MouseEvent} event */
    #onClick(event) {
      if (!(event.target instanceof Element)) {
        return;
      }

      const thumbnail = event.target.closest("[data-artwork-thumbnail]");

      // a held modifier means the visitor asked for the file itself, in a new tab or window,
      // which is what the link already does. A middle click raises auxclick, never this
      if (!thumbnail || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
        return;
      }

      event.preventDefault();
      this.#show(thumbnail.getAttribute("data-artwork-thumbnail"));

      // the address follows the picture, so it can still be copied and sent, without asking the
      // server for anything. replaceState rather than pushState: five thumbnails should not be
      // five presses of the back button. The link's own href is the address the server already
      // wrote, and the current state is handed back rather than wiped, since Blazor keeps its own
      // navigation state there
      history.replaceState(history.state, "", thumbnail.href);
    }

    /** @param {string | null} index */
    #show(index) {
      for (const image of this.querySelectorAll("[data-artwork-image]")) {
        image.toggleAttribute("hidden", image.getAttribute("data-artwork-image") !== index);
      }

      for (const thumbnail of this.querySelectorAll("[data-artwork-thumbnail]")) {
        // spelled out rather than toggled: an empty aria-current reads as false, so the
        // attribute has to carry the word
        if (thumbnail.getAttribute("data-artwork-thumbnail") === index) {
          thumbnail.setAttribute("aria-current", "true");
        } else {
          thumbnail.removeAttribute("aria-current");
        }
      }
    }
  }
);
