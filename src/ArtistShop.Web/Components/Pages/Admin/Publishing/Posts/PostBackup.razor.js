// Keeps a copy of the post's title and text in the browser while the artist types, so work isn't
// lost to the Back button, a closed tab, a crash, or a login that ran out before the save.
// Nothing here saves the post: restoring puts the copy back into the form, and the form saves it

const STORAGE_KEY_PREFIX = "artist-shop:post-backup:";
const NEW_POST_BACKUP_KEY = "new";
const WRITE_DELAY_MILLISECONDS = 500;

// the names Blazor gives PostForm's fields
const TITLE_FIELD = "Input.Title";
const BODY_FIELD = "Input.Body";

/**
 * @typedef {object} Backup
 * @property {string} title
 * @property {string} body the Delta JSON, as the editor writes it
 * @property {string} backedUpAt
 * @property {string} savedVersion the post's saved version when the edits began, "" for a new post
 */

// Storage can be switched off or full, and the backup is a safety net, so it never breaks the page
/** @param {string} backupKey */
function readBackup(backupKey) {
  try {
    const stored = localStorage.getItem(STORAGE_KEY_PREFIX + backupKey);
    return stored === null ? null : /** @type {Backup} */ (JSON.parse(stored));
  } catch {
    return null;
  }
}

/**
 * @param {string} backupKey
 * @param {Backup} backup
 */
function writeBackup(backupKey, backup) {
  try {
    localStorage.setItem(STORAGE_KEY_PREFIX + backupKey, JSON.stringify(backup));
  } catch {}
}

/** @param {string} backupKey */
function forgetBackup(backupKey) {
  try {
    localStorage.removeItem(STORAGE_KEY_PREFIX + backupKey);
  } catch {}
}

// The database stores the text as jsonb, which reorders keys and drops spaces, so the saved text
// and the editor's copy of it are rarely the same string. Sorting every object's keys makes equal
// documents write out the same
/** @param {string} json */
function sortedJson(json) {
  return JSON.stringify(JSON.parse(json), (_key, value) =>
    value !== null && typeof value === "object" && !Array.isArray(value)
      ? Object.fromEntries(Object.entries(value).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)))
      : value
  );
}

/**
 * @param {Backup} backup
 * @param {{ title: string, body: string }} form
 */
function holdsTheSame(backup, form) {
  // the title is trimmed when it's saved
  if (backup.title.trim() !== form.title.trim()) {
    return false;
  }

  try {
    return sortedJson(backup.body) === sortedJson(form.body);
  } catch {
    return false;
  }
}

customElements.define(
  "post-backup",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;
    /** @type {ReturnType<typeof setTimeout> | undefined} */
    #pendingWrite;
    // Held here while the notice is up, because typing before answering it writes a new copy over
    // the stored one, and Restore must still bring back the old one
    /** @type {Backup | null} */
    #offered = null;

    connectedCallback() {
      this.#listeners = new AbortController();
      const { signal } = this.#listeners;

      this.addEventListener("input", () => this.#writeSoon(), { signal });
      // so the copy matches what is saved, and the page that comes back can tell and clear it
      this.addEventListener("submit", () => this.#writeNow(), { signal });
      window.addEventListener("pagehide", () => this.#writeNow(), { signal });
      this.#part("restore")?.addEventListener("click", () => this.#restore(), { signal });
      this.#part("discard")?.addEventListener("click", () => this.#discard(), { signal });

      this.refresh();
    }

    disconnectedCallback() {
      this.#writeNow();
      this.#listeners?.abort();
    }

    // on load, and after an enhanced navigation patches the page in place, as a save does
    refresh() {
      this.#offered = null;
      const form = this.#formValues();

      // the page came back from a failed save, so the form holds the newest edits already
      if (form === null || this.hasAttribute("data-holds-unsaved-changes")) {
        return;
      }

      const backupKey = this.#backupKey();

      // a new post's copy, once the post it became has been saved
      const newPostBackup = readBackup(NEW_POST_BACKUP_KEY);
      if (backupKey !== NEW_POST_BACKUP_KEY && newPostBackup !== null && holdsTheSame(newPostBackup, form)) {
        forgetBackup(NEW_POST_BACKUP_KEY);
      }

      const backup = readBackup(backupKey);

      if (backup === null) {
        return;
      }

      // saved, or edited back to what was saved
      if (holdsTheSame(backup, form)) {
        forgetBackup(backupKey);
        return;
      }

      this.#offer(backup);
    }

    /** @param {Backup} backup */
    #offer(backup) {
      this.#offered = backup;

      const backedUpAt = this.#part("backed-up-at");
      if (backedUpAt !== null) {
        backedUpAt.textContent = new Date(backup.backedUpAt).toLocaleString([], {
          day: "numeric",
          month: "short",
          hour: "numeric",
          minute: "2-digit",
        });
      }

      const savedSince = this.#part("saved-since");
      if (savedSince !== null) {
        savedSince.hidden = backup.savedVersion === this.#savedVersion();
      }

      const notice = this.#part("notice");
      if (notice !== null) {
        notice.hidden = false;
      }
    }

    async #restore() {
      const backup = this.#offered;
      const title = this.#field(TITLE_FIELD);
      const editor = this.querySelector("post-body-editor");

      if (backup === null || title === null || editor === null) {
        return;
      }

      this.#closeNotice();
      title.value = backup.title;
      title.dispatchEvent(new Event("input", { bubbles: true }));
      // defined in PostBodyEditor.razor.js
      await /** @type {HTMLElement & { load(json: string): Promise<void> }} */ (editor).load(backup.body);
    }

    #discard() {
      const backupKey = this.#backupKey();

      // only if nothing typed since has written a newer copy over it
      if (readBackup(backupKey)?.backedUpAt === this.#offered?.backedUpAt) {
        forgetBackup(backupKey);
      }

      this.#closeNotice();
    }

    #closeNotice() {
      this.#offered = null;
      const notice = this.#part("notice");
      if (notice !== null) {
        notice.hidden = true;
      }
    }

    #writeSoon() {
      clearTimeout(this.#pendingWrite);
      this.#pendingWrite = setTimeout(() => this.#writeNow(), WRITE_DELAY_MILLISECONDS);
    }

    #writeNow() {
      if (this.#pendingWrite === undefined) {
        return;
      }

      clearTimeout(this.#pendingWrite);
      this.#pendingWrite = undefined;
      const form = this.#formValues();

      if (form !== null) {
        writeBackup(this.#backupKey(), {
          ...form,
          backedUpAt: new Date().toISOString(),
          savedVersion: this.#savedVersion(),
        });
      }
    }

    #formValues() {
      const title = this.#field(TITLE_FIELD);
      const body = this.#field(BODY_FIELD);
      return title === null || body === null ? null : { title: title.value, body: body.value };
    }

    /** @param {string} name */
    #field(name) {
      const field = this.querySelector("form")?.elements.namedItem(name);
      return field instanceof HTMLInputElement ? field : null;
    }

    /** @param {string} name */
    #part(name) {
      return this.querySelector(`[data-part="${name}"]`);
    }

    // read each time: a new post's first save patches this element with the new post's key
    #backupKey() {
      return this.dataset.backupKey ?? NEW_POST_BACKUP_KEY;
    }

    #savedVersion() {
      return this.dataset.savedVersion ?? "";
    }
  }
);

Blazor.addEventListener("enhancedload", () =>
  document.querySelectorAll("post-backup").forEach((element) => element.refresh())
);
