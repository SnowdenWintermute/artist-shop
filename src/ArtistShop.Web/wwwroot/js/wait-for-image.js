// Waits for an image address that doesn't load yet, such as a size still being made after an upload,
// trying it again with growing waits until it loads. A missing size answers 404 with no cache
// header, so each try asks the server again
import {
  imageWaitFirstMilliseconds,
  imageWaitGiveUpMilliseconds,
  imageWaitLongestMilliseconds,
} from "/js/app-consts.js";

/**
 * onLoaded runs once src loads, so the caller can swap it in. Trying stops once owner has left the
 * page, such as an embed the editor drew again, and after imageWaitGiveUpMilliseconds
 * @param {string} src
 * @param {Element} owner
 * @param {() => void} onLoaded
 */
export function waitForImage(src, owner, onLoaded) {
  const startedAt = Date.now();
  let wait = imageWaitFirstMilliseconds;

  const tryLoading = () => {
    if (!owner.isConnected || Date.now() - startedAt > imageWaitGiveUpMilliseconds) {
      return;
    }

    const probe = new Image();
    probe.addEventListener(
      "load",
      () => {
        if (owner.isConnected) {
          onLoaded();
        }
      },
      { once: true }
    );
    probe.addEventListener(
      "error",
      () => {
        wait = Math.min(wait * 2, imageWaitLongestMilliseconds);
        setTimeout(tryLoading, wait);
      },
      { once: true }
    );
    probe.src = src;
  };

  setTimeout(tryLoading, wait);
}
