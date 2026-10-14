// <tile-masonry>: packs the tiles of the grid inside it into columns. The grid keeps its columns (set by
// container queries) and its order; each tile spans as many 1px rows as it is tall, plus the gap, so the
// browser's own placement drops it into the first column with room, left to right. Without this script the
// tiles stay in plain rows
customElements.define(
  "tile-masonry",
  class extends HTMLElement {
    /** @type {ResizeObserver | null} */
    #resizes = null;
    /** @type {MutationObserver | null} */
    #mutations = null;

    connectedCallback() {
      // on the next frame: spanning rows inside the callback would report a ResizeObserver loop error
      this.#resizes = new ResizeObserver(() => requestAnimationFrame(() => this.#lay()));
      // Blazor inserts an island's elements before their children, and a page turned by enhanced navigation
      // can swap the tiles while keeping this element, so the grid and its tiles are looked for on every change
      this.#mutations = new MutationObserver(() => this.#watch());
      this.#mutations.observe(this, { childList: true, subtree: true });
      this.#watch();
    }

    disconnectedCallback() {
      this.#resizes?.disconnect();
      this.#mutations?.disconnect();
    }

    /** @returns {HTMLElement | null} */
    #grid() {
      return this.querySelector("[data-masonry-grid]");
    }

    // a tile's height follows its width, so watching the tiles also covers the grid changing width
    #watch() {
      const grid = this.#grid();

      if (grid === null || this.#resizes === null) {
        return;
      }

      this.#resizes.disconnect();

      for (const tile of grid.children) {
        this.#resizes.observe(tile);
      }

      this.#lay();
    }

    #lay() {
      const grid = this.#grid();

      if (grid === null || grid.offsetWidth === 0) {
        return;
      }

      const style = getComputedStyle(grid);
      // Measured sizes come out shrunk inside a zoomed box, such as the row editor's preview, while the spans are
      // counted in the grid's own pixels. Its drawn width over its own width is that shrinking (1 elsewhere)
      const scale = grid.getBoundingClientRect().width / parseFloat(style.width) || 1;
      const gap = parseFloat(style.columnGap) || 0;
      grid.style.gridAutoRows = "1px";
      grid.style.rowGap = "0px";

      for (const tile of grid.children) {
        if (tile instanceof HTMLElement) {
          tile.style.gridRowEnd = `span ${Math.ceil(tile.getBoundingClientRect().height / scale + gap)}`;
        }
      }
    }
  }
);
