// the longest each month can be; February's 29 is its leap-year length
const MOST_DAYS_IN_MONTH = [31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
const MOST_DAYS_IN_ANY_MONTH = 31;
const FEBRUARY = 2;

/** @param {number} year */
function isLeapYear(year) {
  return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
}

/** @param {string} text */
function wholeNumberOrNull(text) {
  // Number("") is 0, not NaN, so a blank input has to be ruled out first
  if (text.trim() === "") {
    return null;
  }
  const number = Number(text);
  return Number.isInteger(number) ? number : null;
}
/**
 * Must give the same answers as PartialDate.MaximumDayIn in C#.
 * @param {number | null} year
 * @param {number | null} month
 */
function maximumDayIn(year, month) {
  if (month === null || month < 1 || month > 12) {
    return MOST_DAYS_IN_ANY_MONTH;
  }

  const yearIsKnown = year !== null && year >= 1 && year <= 9999;
  if (month === FEBRUARY && yearIsKnown && !isLeapYear(year)) {
    return 28;
  }

  return MOST_DAYS_IN_MONTH[month - 1];
}

customElements.define(
  "partial-date-field",
  class extends HTMLElement {
    // "#" makes the field private to this class. Aborting the controller removes
    // every listener registered with its signal, in one call.
    /** @type {AbortController | null} */
    #listeners = null;

    // runs whenever a <partial-date-field> is put into the page: first load,
    // enhanced navigation, or the form re-rendering after a failed submit
    connectedCallback() {
      const year = this.querySelector('[data-part="year"]');
      const month = this.querySelector('[data-part="month"]');
      const day = this.querySelector('[data-part="day"]');

      // instanceof narrows the types for the checker, and bails if the markup changed
      if (
        !(year instanceof HTMLInputElement) ||
        !(month instanceof HTMLSelectElement) ||
        !(day instanceof HTMLSelectElement)
      ) {
        return;
      }

      const limitDays = () => {
        const maximumDay = maximumDayIn(
          wholeNumberOrNull(year.value),
          wholeNumberOrNull(month.value)
        );

        for (const option of day.options) {
          option.disabled =
            option.value !== "" && Number(option.value) > maximumDay;
        }

        // a day that just became impossible (31, then February chosen) is cleared
        // instead of being left selected but disabled
        if (day.selectedOptions[0]?.disabled) {
          day.value = "";
        }
      };

      this.#listeners = new AbortController();
      const { signal } = this.#listeners;

      // "input" fires on every keystroke in the year box; "change" when a dropdown choice is made
      year.addEventListener("input", limitDays, { signal });
      month.addEventListener("change", limitDays, { signal });
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }
  }
);
