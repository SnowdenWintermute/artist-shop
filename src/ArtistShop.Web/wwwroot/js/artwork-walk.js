// The artworks a lightbox swipes through, in the order Previous and Next walk them, fetched a few
// at a time from each artwork's walk endpoint as the visitor nears them. Every picture has a fixed
// number, its place among all the pictures in the walk, which the endpoint gives as a count of
// those before it, so an artwork that arrives later slots in without renumbering the others. The
// ones fetched are kept while the lightbox is open, so swiping back finds them
import { lightboxArtworksFetchedAhead } from "/js/app-consts.js";

/**
 * One picture, as ArtworkWalkEndpoints writes it
 * @typedef {object} ArtworkWalkImage
 * @property {string} src
 * @property {string} srcset
 * @property {number} width
 * @property {number} height
 * @property {string} alt
 * @property {string | null} blur
 * @property {string} pageUrl
 */

/**
 * An artwork's step in the walk, as ArtworkWalkEndpoints writes it
 * @typedef {object} ArtworkWalkStep
 * @property {ArtworkWalkImage[]} images
 * @property {string | null} previousWalkUrl
 * @property {string | null} nextWalkUrl
 * @property {number} earlierImageCount
 * @property {number} totalImageCount
 */

/**
 * A step with the address it came from and the number of its first picture
 * @typedef {object} PlacedStep
 * @property {string} walkUrl
 * @property {ArtworkWalkStep} step
 * @property {number} start
 */

/**
 * Where a picture is: whose step, and which of its images
 * @typedef {object} WalkPlace
 * @property {string} walkUrl
 * @property {number} imageIndex
 * @property {string} pageUrl
 */

/**
 * @typedef {object} WalkListeners
 * @property {(indexes: number[]) => void} onArrived pictures that were still coming are in
 * @property {(walkUrl: string, imageIndex: number) => void} onOutOfStep an artwork arrived
 * numbered differently from the ones already here, as when the artist has added or removed
 * pictures since. The walk is no use past that, and should be started again from the picture
 * named
 */

/**
 * @param {ArtworkWalkImage} image
 * @returns {PhotoSwipeSlide}
 */
function slideFor(image) {
  return {
    src: image.src,
    srcset: image.srcset,
    width: image.width,
    height: image.height,
    alt: image.alt,
    // the blur, stretched to fill in while the picture loads
    ...(image.blur === null ? {} : { msrc: image.blur }),
  };
}

export class ArtworkWalk {
  // in walk order. They only ever grow at either end, from the links of the first and the last,
  // so they always run on from one another with no gaps between
  /** @type {PlacedStep[]} */
  #steps;
  #itemCount;
  #index;
  /** @type {Set<string>} */
  #fetching = new Set();
  #requests = new AbortController();
  #listeners;

  /**
   * @param {string} walkUrl where step came from
   * @param {ArtworkWalkStep} step
   * @param {number} imageIndex which of its pictures the lightbox opens on
   * @param {WalkListeners} listeners
   */
  constructor(walkUrl, step, imageIndex, listeners) {
    this.#steps = [{ walkUrl, step, start: step.earlierImageCount }];
    this.#itemCount = step.totalImageCount;
    this.#index = step.earlierImageCount + imageIndex;
    this.#listeners = listeners;
  }

  // the picture the lightbox opens on
  get index() {
    return this.#index;
  }

  /** @returns {LightboxSlides} */
  slides() {
    return {
      itemCount: this.#itemCount,
      slideAt: (index) => {
        const found = this.#find(index);
        const image = found?.placed.step.images[found.imageIndex];
        return image === undefined ? null : slideFor(image);
      },
      // which of its artwork's pictures, since a count over the whole walk says little
      counterAt: (index) => {
        const found = this.#find(index);
        const count = found?.placed.step.images.length ?? 0;
        return found === null || count < 2 ? null : `${found.imageIndex + 1} of ${count}`;
      },
    };
  }

  /**
   * @param {number} index
   * @returns {WalkPlace | null}
   */
  placeAt(index) {
    const found = this.#find(index);
    const image = found?.placed.step.images[found.imageIndex];

    return found === null || image === undefined
      ? null
      : { walkUrl: found.placed.walkUrl, imageIndex: found.imageIndex, pageUrl: image.pageUrl };
  }

  /** @param {number} index the picture the lightbox is on now */
  moveTo(index) {
    this.#index = index;
    this.#fetchAhead();
  }

  // stops what is still being fetched, once the lightbox is done with this walk
  discard() {
    this.#requests.abort();
  }

  /**
   * @param {number} index
   * @returns {{ placed: PlacedStep, imageIndex: number } | null}
   */
  #find(index) {
    for (const placed of this.#steps) {
      const imageIndex = index - placed.start;

      if (imageIndex >= 0 && imageIndex < placed.step.images.length) {
        return { placed, imageIndex };
      }
    }

    return null;
  }

  // enough steps either side of the picture showing, counted in artworks. A picture past either
  // end of those fetched is past that end, with none ahead of it
  #fetchAhead() {
    const first = this.#steps[0];
    const last = this.#steps[this.#steps.length - 1];
    const containing = this.#steps.findIndex(
      (placed) => this.#index >= placed.start && this.#index < placed.start + placed.step.images.length
    );
    const position =
      containing !== -1 ? containing : this.#index < first.start ? -1 : this.#steps.length;

    if (this.#steps.length - 1 - position < lightboxArtworksFetchedAhead) {
      this.#fetch(last, 1);
    }

    if (position < lightboxArtworksFetchedAhead) {
      this.#fetch(first, -1);
    }
  }

  /**
   * @param {PlacedStep} from the step at that end
   * @param {number} direction
   */
  #fetch(from, direction) {
    const walkUrl = direction < 0 ? from.step.previousWalkUrl : from.step.nextWalkUrl;

    if (walkUrl === null || this.#fetching.has(walkUrl)) {
      return;
    }

    this.#fetching.add(walkUrl);

    fetch(walkUrl, { signal: this.#requests.signal, headers: { Accept: "application/json" } })
      .then((response) => (response.ok ? response.json() : null))
      .then((/** @type {ArtworkWalkStep | null} */ step) => {
        if (step !== null) {
          this.#place(from, direction, walkUrl, step);
        }
      })
      // a failed request leaves its pictures black, and the next move asks again. An aborted one
      // belongs to a walk that's over
      .catch(() => {})
      .finally(() => this.#fetching.delete(walkUrl));
  }

  /**
   * @param {PlacedStep} from
   * @param {number} direction
   * @param {string} walkUrl
   * @param {ArtworkWalkStep} step
   */
  #place(from, direction, walkUrl, step) {
    const edge = direction < 0 ? this.#steps[0] : this.#steps[this.#steps.length - 1];

    // only ever asked for from an end, once, so it's still the end it was asked from
    if (edge !== from) {
      return;
    }

    const start = direction < 0 ? from.start - step.images.length : from.start + from.step.images.length;

    if (step.earlierImageCount !== start || step.totalImageCount !== this.#itemCount) {
      const here = this.placeAt(this.#index);

      if (here !== null) {
        this.#listeners.onOutOfStep(here.walkUrl, here.imageIndex);
      } else {
        // the lightbox is on a picture still coming, just past this end, so most likely in the
        // step that's just arrived
        this.#listeners.onOutOfStep(walkUrl, direction < 0 ? step.images.length - 1 : 0);
      }

      return;
    }

    const placed = { walkUrl, step, start };

    if (direction < 0) {
      this.#steps.unshift(placed);
    } else {
      this.#steps.push(placed);
    }

    this.#listeners.onArrived(step.images.map((_, imageIndex) => start + imageIndex));
    this.#fetchAhead();
  }
}
