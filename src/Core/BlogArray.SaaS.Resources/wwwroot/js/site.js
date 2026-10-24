var lowerAlphabets = 'abcdefghijklmnopqrstuvwxyz';
var upperAlphabets = lowerAlphabets.toUpperCase();
var numbers = '0123456789';
var specialCharacters = '-._~';
var alphabets = lowerAlphabets + upperAlphabets;
var alphabetNumbers = alphabets + numbers;
var allCharset = alphabetNumbers + specialCharacters;
var activeAjaxRequests = 0;

// Bootstrap 6 ships as an ES module; bootstrap-global.js (a module script) exposes it as
// window.bootstrap. Module scripts run before DOMContentLoaded, so Bootstrap-dependent setup
// waits for it. Menus (data-bs-toggle="menu") initialize through the data API.
var tooltipTriggerList = [];
var tooltipList = [];

document.addEventListener('DOMContentLoaded', resetTooltips);

// Open dialogs and drawers (<dialog>, showModal) render in the browser's top layer, above any
// z-index: the toast container and the loader would sit under their overlay. Raising them into
// the top layer as manual popovers puts them above whatever is already there; the element
// raised last is on top, so they are re-raised each time they are shown.
var TopLayer = (function () {
    const supported = typeof HTMLElement !== 'undefined' && Object.prototype.hasOwnProperty.call(HTMLElement.prototype, 'popover');

    function raise(element) {
        if (!supported || !element || !element.isConnected) {
            return;
        }

        if (!element.hasAttribute('popover')) {
            element.setAttribute('popover', 'manual');
        }

        if (element.matches(':popover-open')) {
            element.hidePopover();
        }

        element.showPopover();
    }

    function lower(element) {
        if (supported && element && element.matches(':popover-open')) {
            element.hidePopover();
        }
    }

    return {
        raise: raise,
        lower: lower
    };
})();

// Double-submit guard for Unobtrusive Ajax forms (data-ajax="true"): while a form's request is
// running its submit buttons - including buttons outside it bound with form="..." - are
// disabled. Unobtrusive Ajax starts the request synchronously from its submit handler (ajaxSend),
// and only when validation passed and any data-ajax-confirm was accepted. The capture-phase
// listener runs before every submit handler and the microtask after all of them, so a form is
// disabled exactly when its submit started a request, whatever order the scripts loaded in.
var AjaxFormGuard = (function () {
    let sentCount = 0;
    const busyForms = new Set();

    $(document).ajaxSend(function () {
        sentCount++;
    }).ajaxComplete(function () {
        busyForms.forEach(function (form) {
            setSubmitDisabled(form, false);
        });
        busyForms.clear();
    });

    function setSubmitDisabled(form, disabled) {
        Array.prototype.forEach.call(form.elements, function (element) {
            if (element.type === 'submit') {
                element.disabled = disabled;
            }
        });
    }

    document.addEventListener('submit', function (e) {
        const form = e.target;

        if (!(form instanceof HTMLFormElement) || form.getAttribute('data-ajax') !== 'true') {
            return;
        }

        const sentBefore = sentCount;

        queueMicrotask(function () {
            if (sentCount > sentBefore) {
                setSubmitDisabled(form, true);
                busyForms.add(form);
            }
        });
    }, true);

    return {};
})();

function hideGlobalLoader() {
    $('.global-loader').fadeOut(250, function () {
        TopLayer.lower(this);
    });
}

$(document).ajaxSend(function (event, xhr, options) {
    activeAjaxRequests++;
    $('.global-loader').stop(true, true).fadeIn(250).each(function () {
        TopLayer.raise(this);
    });
}).ajaxComplete(function (event, xhr, options) {
    // ajaxComplete fires for failed requests too, so the count is only decremented here.
    activeAjaxRequests--;
    if (activeAjaxRequests <= 0) {
        activeAjaxRequests = 0;
        hideGlobalLoader();
        resetTooltips();
    }
}).ajaxError(function (event, jqxhr, settings, exception) {
    if (jqxhr.status === 401) {
        location.reload();
    }
});

function resetTooltips() {
    tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]')
    tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => bootstrap.Tooltip.getOrCreateInstance(tooltipTriggerEl))
}

function handleAjaxError(e) {
    var res = e.responseJSON;
    if (res && !res.status) {
        $('#globalErrorModal .dialog-body p').text(res.message);
    } else {
        $('#globalErrorModal .dialog-body p').text('Unfortunately, there was a problem with the request. Please try again. It is recommended that you contact your administrator if the issue persists, along with relevant information.');
    }
    bootstrap.Dialog.getOrCreateInstance(document.getElementById('globalErrorModal')).show();
}

function setImage(id, url) {
    document.querySelectorAll('#' + id).forEach(img => {
        img.src = url;
    });
}

function reloadPage() {
    location.reload();
}

var Toast = (function () {
    function success(message, header = null) {
        header = header == null ? 'Success' : header;
        renderToast('success', header, message);
    }

    function error(message, header = null) {
        header = header == null ? 'Error' : header;
        renderToast('error', header, message);
    }

    function warning(message, header = null) {
        header = header == null ? 'Warning' : header;
        renderToast('warning', header, message);
    }

    function info(message, header = null) {
        header = header == null ? 'Info' : header;
        renderToast('info', header, message);
    }

    function renderToast(type, header, message) {
        var toastContainer = document.getElementById('toastContainer');
        var toast = document.createElement('div');
        toast.classList.add('toast');
        //toast.classList.add('show');
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'assertive');
        toast.setAttribute('aria-atomic', 'true');

        var toastHeader = document.createElement('div');
        toastHeader.classList.add('toast-header');

        var icon = document.createElement('i');
        icon.classList.add('bi');

        switch (type) {
            case 'success':
                icon.classList.add('bi-check-circle-fill');
                toastHeader.classList.add('bg-success', 'fg-contrast-success');
                break;
            case 'error':
                icon.classList.add('bi-x-circle-fill');
                toastHeader.classList.add('bg-danger', 'fg-contrast-danger');
                break;
            case 'warning':
                icon.classList.add('bi-exclamation-circle-fill');
                toastHeader.classList.add('bg-warning', 'fg-contrast-warning');
                break;
            default:
                icon.classList.add('bi-info-circle-fill');
                toastHeader.classList.add('bg-info', 'fg-contrast-info');
                break;
        }

        var strong = document.createElement('strong');
        strong.classList.add('me-auto');
        strong.classList.add('ms-3');
        strong.textContent = header;

        var button = document.createElement('button');
        button.classList.add('btn-close');
        button.setAttribute('type', 'button');
        button.setAttribute('data-bs-dismiss', 'toast');
        button.setAttribute('aria-label', 'Close');

        var toastBody = document.createElement('div');
        toastBody.classList.add('toast-body');
        toastBody.textContent = message;

        toastHeader.appendChild(icon);
        toastHeader.appendChild(strong);
        toastHeader.appendChild(button);
        toast.appendChild(toastHeader);
        toast.appendChild(toastBody);

        toastContainer.appendChild(toast);

        // Above any open dialog or drawer (see TopLayer).
        TopLayer.raise(toastContainer);

        var bsToast = new bootstrap.Toast(toast);
        bsToast.show();

        toast.addEventListener('hidden.bs.toast', () => {
            toast.remove();

            if (!toastContainer.querySelector('.toast')) {
                TopLayer.lower(toastContainer);
            }
        })
    }

    return {
        success: success,
        info: info,
        warning: warning,
        error: error,
    };
})();

let Unobtrusive = function () {
    /**
     * Resets and reinitiate unobtrusive form validations
     * @param {any} $form Form id to reset and reinitiate
     * @param {number} waitFor wait time to reset form. Default is 500ms
     */
    function reInit($form, waitFor = 500) {
        setTimeout(function () {
            $form.removeData('validator');
            $form.removeData('unobtrusiveValidation');
            $.validator.unobtrusive.parse($form);
        }, waitFor);
    }
    return {
        reInit: reInit
    }
}();

// Client-side rule for the server-side RequiredIf attribute. site.js is loaded before the
// jquery-validate scripts (rendered in the Scripts section), so registration waits for ready.
$(function () {
    if (typeof $.validator === 'undefined' || typeof $.validator.unobtrusive === 'undefined') {
        return;
    }

    $.validator.addMethod('requiredif', function (value, element, params) {
        let dependentValue;

        const $dependent = $(element).closest('form')
            .find('[name="' + params.dependentProperty + '"]');

        if ($dependent.length === 0) {
            // Dependent control is not on the page: defer to server-side validation.
            return true;
        }

        if ($dependent.is(':radio')) {
            dependentValue = $dependent.filter(':checked').val();
        } else if ($dependent.is(':checkbox')) {
            dependentValue = $dependent.is(':checked');
        } else {
            dependentValue = $dependent.val();
        }

        const actual = String(dependentValue ?? '').toLowerCase();
        const target = String(params.targetValue ?? '').toLowerCase();

        // Required only when the dependency is met; otherwise the field is optional.
        if (actual !== target) {
            return true;
        }

        return value !== null && value !== undefined && String(value).trim().length > 0;
    }, '');

    $.validator.unobtrusive.adapters.add('requiredif', ['dependentproperty', 'targetvalue'], function (options) {
        options.rules['requiredif'] = {
            dependentProperty: options.params.dependentproperty,
            targetValue: options.params.targetvalue
        };

        if (options.message) {
            options.messages['requiredif'] = options.message;
        }
    });
});

let AppCrypto = function () {
    /**
     * Generates a unique code with a specified length using cryptographically secure randomness.
     * @param {number} length Length of the unique code to generate. Default is 32.
     */
    function generateUniqueCode(length = 32) {
        const charset = lowerAlphabets + numbers;

        // Rejection sampling over crypto.getRandomValues removes modulo bias without
        // relying on Math.random or time-based seeds.
        const maxAccepted = Math.floor(4294967296 / charset.length) * charset.length;
        const buffer = new Uint32Array(length);
        let randomString = '';
        let index = 0;

        while (index < length) {
            window.crypto.getRandomValues(buffer);
            for (let i = 0; i < buffer.length && index < length; i++) {
                if (buffer[i] < maxAccepted) {
                    randomString += charset[buffer[i] % charset.length];
                    index++;
                }
            }
        }

        return randomString;
    }

    /**
     * Generates a unique code with a specified length based on the cryptographic randomness.
     * @param {number} length Length of the unique code to generate. Default is 12.
     */
    function generatePassword(length = 12) {
        // Cryptographically secure random integer in [0, maxExclusive) with rejection sampling.
        const secureRandomInt = (maxExclusive) => {
            const maxAccepted = Math.floor(4294967296 / maxExclusive) * maxExclusive;
            const value = new Uint32Array(1);

            do {
                window.crypto.getRandomValues(value);
            } while (value[0] >= maxAccepted);

            return value[0] % maxExclusive;
        };

        // Ensuring minimum requirement characters, selected with crypto randomness
        let password = '';
        password += lowerAlphabets[secureRandomInt(lowerAlphabets.length)];
        password += upperAlphabets[secureRandomInt(upperAlphabets.length)];
        password += numbers[secureRandomInt(numbers.length)];
        password += specialCharacters[secureRandomInt(specialCharacters.length)];

        // Generate remaining characters randomly
        const remainingLength = length - password.length;
        const values = new Uint32Array(remainingLength);
        window.crypto.getRandomValues(values);

        for (let i = 0; i < remainingLength; i++) {
            password += allCharset[values[i] % allCharset.length];
        }

        // Fisher-Yates shuffle driven by crypto randomness to avoid predictable placement
        // of the required characters.
        const chars = password.split('');
        for (let i = chars.length - 1; i > 0; i--) {
            const j = secureRandomInt(i + 1);
            const temp = chars[i];
            chars[i] = chars[j];
            chars[j] = temp;
        }

        return chars.join('');
    }
    return {
        GenerateUniqueCode: generateUniqueCode,
        GeneratePassword: generatePassword
    }
}();

let ClipboardModule = (function () {
    /**
     * Copies text to the clipboard. Accepts either a text string or an element ID.
     * @param {string} input - The text to copy or the ID of the element containing the text.
     * @param {string} successMessage - Message to log on successful copy.
     * @param {string} failMessage - Message to log if copying fails.
     */
    function copyText(input, successMessage = "Text successfully copied to clipboard.", failMessage = "Copying failed. Please try again.") {
        let text;

        const element = document.getElementById(input);
        if (element) {
            text = element.value || element.innerText || element.textContent;
        } else {
            text = input;
        }

        // Use the Clipboard API for modern browsers in secure contexts
        navigator.clipboard.writeText(text)
            .then(() => {
                Toast.success(successMessage);
            })
            .catch(() => {
                Toast.error(failMessage);
            });
    }

    return {
        copyText: copyText
    };
})();

document.addEventListener('DOMContentLoaded', function () {
    // Select all elements with data-confirm="true"
    const confirmLinks = document.querySelectorAll('[data-confirm="true"]');

    confirmLinks.forEach(link => {
        link.addEventListener('click', function (event) {
            // Get the confirmation message from data-confirm-message or use a default message
            const message = link.getAttribute('data-confirm-message') || "Are you sure you want to proceed?";

            // Show confirmation dialog
            const isConfirmed = confirm(message);

            // If the user does not confirm, prevent the link action
            if (!isConfirmed) {
                event.preventDefault();
            }
        });
    });
});

function updateTooltip(tooltipElement, message) {
    // Select the tooltip element by ID
    //const tooltipElement = document.getElementById(id);

    if (!tooltipElement) {
        console.error(`Element with ID "${id}" not found.`);
        return;
    }

    // Update the title attribute with the new message
    tooltipElement.setAttribute('data-bs-title', message);

    // Dispose of any existing tooltip instance to avoid duplication
    const tooltipInstance = bootstrap.Tooltip.getInstance(tooltipElement);
    if (tooltipInstance) {
        tooltipInstance.dispose();
    }

    // Reinitialize the tooltip with the updated message
    new bootstrap.Tooltip(tooltipElement);
}

let DynamicContentLoader = (function () {
    /**
     * Loads content dynamically into an element
     * @param {string} id - The ID of the element to load content into
     */
    function load(id) {
        const $element = $('#' + id);

        if ($element.length === 0) {
            console.error(`Unable to find element with id: ${id}`);
            return;
        }

        const url = $element.data('url');

        if (!url) {
            console.error(`No data-url found for element with id: ${id}`);
            return;
        }

        $element.load(url, function (response, status, xhr) {
            if (status === "error") {
                console.error('Error loading content:', xhr.status, xhr.statusText);
                $element.html(`<div class="error">Failed to load content</div>`);
            }
        });
    }

    return {
        load: load
    };
})();

let ColorMode = (function () {
    // Light / dark / system color mode. "auto" removes data-bs-theme so Bootstrap 6's
    // `color-scheme: light dark` follows the OS preference. _ColorModeHead.cshtml applies
    // the stored mode before first paint; this module handles the picker.
    const storageKey = 'bs-color-mode';
    const icons = { light: 'bi-sun', dark: 'bi-moon-stars', auto: 'bi-circle-half' };

    function get() {
        try {
            const mode = localStorage.getItem(storageKey);
            return mode === 'light' || mode === 'dark' ? mode : 'auto';
        } catch (e) {
            return 'auto';
        }
    }

    function set(mode) {
        try {
            localStorage.setItem(storageKey, mode);
        } catch (e) { }

        if (mode === 'light' || mode === 'dark') {
            document.documentElement.setAttribute('data-bs-theme', mode);
        } else {
            document.documentElement.removeAttribute('data-bs-theme');
        }

        sync(mode);
    }

    function sync(mode) {
        document.querySelectorAll('[data-color-mode-value]').forEach(item => {
            const active = item.getAttribute('data-color-mode-value') === mode;
            item.classList.toggle('active', active);
            item.setAttribute('aria-pressed', active ? 'true' : 'false');
        });

        document.querySelectorAll('[data-color-mode-icon]').forEach(icon => {
            icon.classList.remove(...Object.values(icons));
            icon.classList.add(icons[mode]);
        });
    }

    document.addEventListener('click', function (event) {
        const item = event.target.closest('[data-color-mode-value]');
        if (item) {
            set(item.getAttribute('data-color-mode-value'));
        }
    });

    document.addEventListener('DOMContentLoaded', function () {
        sync(get());
    });

    return {
        get: get,
        set: set
    };
})();
