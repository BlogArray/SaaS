'use strict';

function _toConsumableArray(arr) { if (Array.isArray(arr)) { for (var i = 0, arr2 = Array(arr.length); i < arr.length; i++) arr2[i] = arr[i]; return arr2; } else { return Array.from(arr); } }

var tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
var tooltipList = [].concat(_toConsumableArray(tooltipTriggerList)).map(function (tooltipTriggerEl) {
    return new bootstrap.Tooltip(tooltipTriggerEl);
});
document.querySelectorAll('[data-bs-toggle="dropdown"]').forEach(function (dropdownToggleEl) {
    bootstrap.Dropdown.getOrCreateInstance(dropdownToggleEl, { popperConfig: function popperConfig() {
            return { strategy: 'fixed' };
        } });
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
    tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipList = [].concat(_toConsumableArray(tooltipTriggerList)).map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });
}

function handleAjaxError(e) {
    var res = e.responseJSON;
    if (res && !res.status) {
        $('#globalErrorModal .modal-body p').text(res.message);
    } else {
        $('#globalErrorModal .modal-body p').text('Unfortunately, there was a problem with the request. Please try again. It is recommended that you contact your administrator if the issue persists, along with relevant information.');
    }
    var errMdl = new bootstrap.Modal(document.getElementById('globalErrorModal'));
    errMdl.show();
}

var Unobtrusive = (function () {
    /**
     * Resets and reinitiate unobtrusive form validations
     * @param {any} $form Form id to reset and reinitiate
     * @param {number} waitFor wait time to reset form. Default is 500ms
     */
    function reInit($form) {
        var waitFor = arguments.length <= 1 || arguments[1] === undefined ? 500 : arguments[1];

        setTimeout(function () {
            $form.removeData('validator');
            $form.removeData('unobtrusiveValidation');
            $.validator.unobtrusive.parse($form);
        }, waitFor);
    }
    return {
        reInit: reInit
    };
})();

function setImage(id, url) {
    $('#' + id).attr('src', url);
}

var Toast = (function () {
    function success(message) {
        var header = arguments.length <= 1 || arguments[1] === undefined ? null : arguments[1];

        header = header == null ? 'Success' : header;
        renderToast('success', header, message);
    }

    function error(message) {
        var header = arguments.length <= 1 || arguments[1] === undefined ? null : arguments[1];

        header = header == null ? 'Error' : header;
        renderToast('error', header, message);
    }

    function warning(message) {
        var header = arguments.length <= 1 || arguments[1] === undefined ? null : arguments[1];

        header = header == null ? 'Warning' : header;
        renderToast('warning', header, message);
    }

    function info(message) {
        var header = arguments.length <= 1 || arguments[1] === undefined ? null : arguments[1];

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

        toast.addEventListener('hidden.bs.toast', function () {
            toast.remove();
        });
    }

    return {
        success: success,
        info: info,
        warning: warning,
        error: error
    };
})();

