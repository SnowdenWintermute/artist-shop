// How much of an upload run has been sent, as a percentage for a progress bar: each finished file
// whole, and each file in flight as far as it has got

const REPORT_INTERVAL_MILLISECONDS = 250;

/**
 * @param {number} totalBytes every file the run will send
 * @param {(percentComplete: number) => void} report called at most every 250 ms, but for done()
 */
export function createUploadProgress(totalBytes, report) {
  /** bytes of files whose response has arrived */
  let finishedBytes = 0;
  /** bytes sent so far by each upload still in flight */
  const loadedByFile = new Map();
  let reportedAt = 0;

  /** @param {boolean} force sends even inside the interval, for a run's last update */
  function send(force) {
    const now = Date.now();
    if (!force && now - reportedAt < REPORT_INTERVAL_MILLISECONDS) {
      return;
    }

    reportedAt = now;

    let sent = finishedBytes;
    for (const bytes of loadedByFile.values()) {
      sent += bytes;
    }

    report(totalBytes === 0 ? 100 : Math.min(100, Math.round((sent / totalBytes) * 100)));
  }

  return {
    /**
     * @param {string} id
     * @param {number} loaded
     */
    loaded(id, loaded) {
      loadedByFile.set(id, loaded);
      send(false);
    },
    // none of a file's bytes count while it waits to be sent again
    /** @param {string} id */
    retrying(id) {
      loadedByFile.delete(id);
      send(false);
    },
    /**
     * uploaded or turned away, either way it's no longer being sent
     * @param {string} id
     * @param {number} size
     */
    finished(id, size) {
      loadedByFile.delete(id);
      finishedBytes += size;
      send(false);
    },
    done() {
      send(true);
    },
  };
}
