var lowerAlphabets = 'abcdefghijklmnopqrstuvwxyz';
var upperAlphabets = lowerAlphabets.toUpperCase();
var numbers = '0123456789';
var specialCharacters = '-._~';
var alphabets = lowerAlphabets + upperAlphabets;
var alphabetNumbers = alphabets + numbers;
var allCharset = alphabetNumbers + specialCharacters;

var tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]')
var tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl))

document.querySelectorAll('[data-bs-toggle="dropdown"]').forEach((dropdownToggleEl) => {
    bootstrap.Dropdown.getOrCreateInstance(dropdownToggleEl, { popperConfig() { return { strategy: 'fixed' } } });
});

$(document).ajaxSend(function (event, xhr, options) {
    $('.global-loader').fadeIn(250);
}).ajaxComplete(function (event, xhr, options) {
    $('.global-loader').fadeOut(250);
}).ajaxError(function (event, jqxhr, settings, exception) {
    $('.global-loader').fadeOut(250);
    if (jqxhr.status === 401) {
        location.reload();
    }
}).ajaxComplete(function () {
    resetTooltips();
});

function resetTooltips() {
    tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]')
    tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl))
}

function handleAjaxError(e) {
    var res = e.responseJSON;
    if (res && !res.status) {
        $('#globalErrorModal .modal-body p').text(res.message);
    } else {
        $('#globalErrorModal .modal-body p').text('Unfortunately, there was a problem with the request. Please try again. It is recommended that you contact your administrator if the issue persists, along with relevant information.');
    }
    var errMdl = new bootstrap.Modal(document.getElementById('globalErrorModal'))
    errMdl.show();
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
                toastHeader.classList.add('bg-success', 'text-white');
                break;
            case 'error':
                icon.classList.add('bi-x-circle-fill');
                toastHeader.classList.add('bg-danger', 'text-white');
                break;
            case 'warning':
                icon.classList.add('bi-exclamation-circle-fill');
                toastHeader.classList.add('bg-warning', 'text-dark');
                break;
            default:
                icon.classList.add('bi-info-circle-fill');
                toastHeader.classList.add('bg-info', 'text-white');
                break;
        }

        var strong = document.createElement('strong');
        strong.classList.add('me-auto');
        strong.classList.add('ms-2');
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

        var bsToast = new bootstrap.Toast(toast);
        bsToast.show();

        toast.addEventListener('hidden.bs.toast', () => {
            toast.remove();
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

let AppCrypto = function () {
    /**
     * Generates a unique code with a specified length based on the current timestamp and cryptographic randomness.
     * @param {number} length Length of the unique code to generate. Default is 32.
     */
    function generateUniqueCode(length = 32) {
        const charset = lowerAlphabets + numbers;

        // Get the current date and time in milliseconds
        const timestamp = Date.now().toString(36); // Base-36 encoding for shorter string
        const remainingLength = length - timestamp.length;

        let randomString = '';

        for (let i = 0; i < remainingLength; i++) {
            const randomIndex = Math.floor(Math.random() * charset.length);
            randomString += charset[randomIndex];
        }

        // Combine timestamp and random string, trimming to the requested length
        const combinedString = `${randomString}${timestamp}`;
        return combinedString;
    }

    /**
     * Generates a unique code with a specified length based on the cryptographic randomness.
     * @param {number} length Length of the unique code to generate. Default is 12.
     */
    function generatePassword(length = 12) {
        // Ensuring minimum requirement characters
        let password = '';
        password += lowerAlphabets[Math.floor(Math.random() * lowerAlphabets.length)];
        password += upperAlphabets[Math.floor(Math.random() * upperAlphabets.length)];
        password += numbers[Math.floor(Math.random() * numbers.length)];
        password += specialCharacters[Math.floor(Math.random() * specialCharacters.length)];

        // Generate remaining characters randomly
        const remainingLength = length - password.length;
        const values = new Uint32Array(remainingLength);
        window.crypto.getRandomValues(values);

        for (let i = 0; i < remainingLength; i++) {
            password += allCharset[values[i] % allCharset.length];
        }

        // Shuffle the resulting password to avoid predictable placement of required characters
        password = password.split('').sort(() => 0.5 - Math.random()).join('');

        return password;
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
