// Shared WebAuthn helpers for passkey sign-in: the login page (button and email-field
// autofill), the passkey sign-in page and the "Confirm it's you" step-up page.
//
// The server issues the assertion options (and keeps the challenge); this script only runs the
// browser ceremony and serializes the authenticator's response in the shape PasskeyService
// expects (base64url fields).
(function () {
    'use strict';

    function base64UrlToArrayBuffer(value) {
        const base64 = value.replace(/-/g, '+').replace(/_/g, '/');
        const padded = base64 + '='.repeat((4 - base64.length % 4) % 4);
        const raw = atob(padded);
        const output = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) output[i] = raw.charCodeAt(i);
        return output.buffer;
    }

    function arrayBufferToBase64Url(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
        return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }

    /**
     * Runs an assertion ceremony with the server-issued options (a JSON string) and returns the
     * serialized response to post back. `mediation` is 'conditional' for email-field autofill
     * (resolves only when the user picks a passkey there); `signal` lets a pending conditional
     * request be cancelled before starting the modal one.
     */
    async function getAssertion(optionsJson, mediation, signal) {
        const options = JSON.parse(optionsJson);

        options.challenge = base64UrlToArrayBuffer(options.challenge);

        if (options.allowCredentials) {
            options.allowCredentials = options.allowCredentials.map(function (item) {
                return { id: base64UrlToArrayBuffer(item.id), type: item.type, transports: item.transports };
            });
        }

        const request = { publicKey: options };

        if (mediation) request.mediation = mediation;
        if (signal) request.signal = signal;

        const credential = await navigator.credentials.get(request);

        return JSON.stringify({
            id: credential.id,
            rawId: arrayBufferToBase64Url(credential.rawId),
            type: credential.type,
            response: {
                authenticatorData: arrayBufferToBase64Url(credential.response.authenticatorData),
                clientDataJSON: arrayBufferToBase64Url(credential.response.clientDataJSON),
                signature: arrayBufferToBase64Url(credential.response.signature),
                userHandle: credential.response.userHandle ? arrayBufferToBase64Url(credential.response.userHandle) : null
            }
        });
    }

    /** True when the browser can offer passkeys in an input's autofill (conditional UI). */
    async function isConditionalMediationAvailable() {
        return Boolean(window.PublicKeyCredential
            && PublicKeyCredential.isConditionalMediationAvailable
            && await PublicKeyCredential.isConditionalMediationAvailable());
    }

    /** True for the errors a user produces by dismissing the prompt or a cancelled request. */
    function isCancellation(error) {
        return error && (error.name === 'NotAllowedError' || error.name === 'AbortError');
    }

    window.blogArrayPasskey = {
        isSupported: Boolean(window.PublicKeyCredential),
        getAssertion: getAssertion,
        isConditionalMediationAvailable: isConditionalMediationAvailable,
        isCancellation: isCancellation
    };
})();
