const browserSupportsPasskeys =
    typeof navigator.credentials !== 'undefined' &&
    typeof window.PublicKeyCredential !== 'undefined' &&
    typeof window.PublicKeyCredential.parseCreationOptionsFromJSON === 'function' &&
    typeof window.PublicKeyCredential.parseRequestOptionsFromJSON === 'function';

/**
 * @param {string} url
 * @param {RequestInit} [options]
 */
async function fetchWithErrorHandling(url, options = {}) {
    const response = await fetch(url, {
        credentials: 'include',
        ...options
    });
    if (!response.ok) {
        const text = await response.text();
        console.error(text);
        throw new Error(`The server responded with status ${response.status}.`);
    }
    return response;
}

/**
 * @param {Record<string, string>} headers
 * @param {AbortSignal} signal
 */
async function createCredential(headers, signal) {
    const optionsResponse = await fetchWithErrorHandling('/Account/PasskeyCreationOptions', {
        method: 'POST',
        headers,
        signal,
    });
    const optionsJson = await optionsResponse.json();
    const options = PublicKeyCredential.parseCreationOptionsFromJSON(optionsJson);
    return await navigator.credentials.create({ publicKey: options, signal });
}

/**
 * @param {string} email
 * @param {CredentialMediationRequirement | undefined} mediation
 * @param {Record<string, string>} headers
 * @param {AbortSignal} signal
 */
async function requestCredential(email, mediation, headers, signal) {
    // encoded, or a + in the address would arrive as a space
    const optionsResponse = await fetchWithErrorHandling(`/Account/PasskeyRequestOptions?username=${encodeURIComponent(email)}`, {
        method: 'POST',
        headers,
        signal,
    });
    const optionsJson = await optionsResponse.json();
    const options = PublicKeyCredential.parseRequestOptionsFromJSON(optionsJson);
    return await navigator.credentials.get({ publicKey: options, mediation, signal });
}

customElements.define('passkey-submit', class extends HTMLElement {
    static formAssociated = true;

    // in the constructor, as attaching twice throws, and an element can be connected again
    internals = this.attachInternals();
    /** @type {AbortController | null} */
    abortController = null;

    connectedCallback() {
        this.form().addEventListener('submit', (event) => {
            if (event.submitter instanceof HTMLButtonElement && event.submitter.name === '__passkeySubmit') {
                event.preventDefault();
                this.obtainAndSubmitCredential();
            }
        });

        this.tryAutofillPasskey();
    }

    disconnectedCallback() {
        this.abortController?.abort();
    }

    form() {
        const form = this.internals.form;
        if (form === null) {
            throw new Error('passkey-submit has to be inside a form.');
        }
        return form;
    }

    // PasskeySubmit.razor renders every attribute, but a token's can be empty
    /** @param {string} name */
    attribute(name) {
        return this.getAttribute(name) ?? '';
    }

    /**
     * @param {boolean} useConditionalMediation
     * @param {AbortSignal} signal
     */
    async obtainCredential(useConditionalMediation, signal) {
        if (!browserSupportsPasskeys) {
            throw new Error('Some passkey features are missing. Please update your browser.');
        }

        const headers = {
            [this.attribute('request-token-name')]: this.attribute('request-token-value'),
        };
        const operation = this.attribute('operation');

        if (operation === 'Create') {
            return await createCredential(headers, signal);
        } else if (operation === 'Request') {
            const email = new FormData(this.form()).get(this.attribute('email-name'));
            const mediation = useConditionalMediation ? 'conditional' : undefined;
            return await requestCredential(typeof email === 'string' ? email : '', mediation, headers, signal);
        } else {
            throw new Error(`Unknown passkey operation '${operation}'.`);
        }
    }

    async obtainAndSubmitCredential(useConditionalMediation = false) {
        this.abortController?.abort();
        this.abortController = new AbortController();
        const signal = this.abortController.signal;
        const formData = new FormData();
        const name = this.attribute('name');
        try {
            const credential = await this.obtainCredential(useConditionalMediation, signal);
            const credentialJson = JSON.stringify(credential);
            formData.append(`${name}.CredentialJson`, credentialJson);
        } catch (error) {
            if (error instanceof Error && error.name === 'AbortError') {
                // The user explicitly canceled the operation - return without error.
                return;
            }
            console.error(error);
            if (useConditionalMediation) {
                // An error occurred during conditional mediation, which is not user-initiated.
                // We log the error in the console but do not relay it to the user.
                return;
            }
            const errorMessage = !(error instanceof Error)
                ? String(error)
                : error.name === 'NotAllowedError'
                    ? 'No passkey was provided by the authenticator.'
                    : error.message;
            formData.append(`${name}.Error`, errorMessage);
        }
        this.internals.setFormValue(formData);
        this.form().submit();
    }

    async tryAutofillPasskey() {
        if (browserSupportsPasskeys && this.attribute('operation') === 'Request' && await PublicKeyCredential.isConditionalMediationAvailable?.()) {
            await this.obtainAndSubmitCredential(/* useConditionalMediation */ true);
        }
    }
});
